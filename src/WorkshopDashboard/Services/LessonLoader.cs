using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using WorkshopDashboard.Models;

namespace WorkshopDashboard.Services;

public sealed record LessonLoadError(string SourcePath, string Message);

public sealed record LessonLoadResult(LessonCollection Lessons, IReadOnlyList<LessonLoadError> Errors);

// Reads every lesson file under the lesson folders and renders it as one list, sorted by order.
// A lesson with invalid front matter is skipped and reported, so one bad file can't take the site down.
public static partial class LessonLoader
{
    // Labs 0 to 2 are in foundation and the rest are in explore. The folder doesn't change how a lab is shown.
    public static readonly IReadOnlyList<string> Folders = ["foundation", "explore"];

    public static readonly IReadOnlySet<string> Difficulties = new HashSet<string>(StringComparer.Ordinal)
    {
        "Beginner",
        "Intermediate",
        "Advanced",
    };

    public static readonly IReadOnlySet<string> KnownFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "title",
        "summary",
        "duration",
        "difficulty",
        "order",
        "requires",
    };

    public static LessonLoadResult Load(string lessonsPath, MarkdownRenderer renderer, string repositoryUrl)
    {
        var errors = new List<LessonLoadError>();
        if (!Directory.Exists(lessonsPath))
        {
            errors.Add(new LessonLoadError("lessons", $"The lessons folder doesn't exist: {lessonsPath}"));
            return new LessonLoadResult(LessonCollection.Empty, errors);
        }

        var parsed = new List<(Lesson Lesson, string Body)>();
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orders = new HashSet<int>();

        foreach (string folder in Folders)
        {
            string folderPath = Path.Combine(lessonsPath, folder);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(folderPath, "*.md").Order(StringComparer.Ordinal))
            {
                string sourcePath = $"lessons/{folder}/{Path.GetFileName(file)}";
                if (!TryReadLesson(file, sourcePath, out Lesson? lesson, out string? body, out string? error))
                {
                    errors.Add(new LessonLoadError(sourcePath, error));
                    continue;
                }

                if (!slugs.Add(lesson.Slug))
                {
                    errors.Add(new LessonLoadError(sourcePath, $"Another lesson already uses the address /lessons/{lesson.Slug}. Rename one of the files."));
                    continue;
                }

                if (!orders.Add(lesson.Order))
                {
                    errors.Add(new LessonLoadError(sourcePath, $"Another lesson already uses order {lesson.Order}. Each lesson needs a unique order."));
                    continue;
                }

                parsed.Add((lesson, body));
            }
        }

        var slugsByPath = parsed.ToDictionary(item => item.Lesson.SourcePath, item => item.Lesson.Slug, StringComparer.Ordinal);
        var links = new LinkResolver(repositoryUrl, slugsByPath);

        IEnumerable<Lesson> rendered = parsed.Select(item => item.Lesson with
        {
            Html = renderer.RenderLesson(item.Body, item.Lesson.SourcePath, links),
        });

        return new LessonLoadResult(new LessonCollection(rendered), errors);
    }

    // 01-first-workflow.md becomes first-workflow. Uppercase names are rejected later instead of being changed.
    public static string SlugFromFileName(string fileName)
    {
        return OrderPrefix().Replace(Path.GetFileNameWithoutExtension(fileName), string.Empty);
    }

    private static bool TryReadLesson(
        string file,
        string sourcePath,
        [NotNullWhen(true)] out Lesson? lesson,
        [NotNullWhen(true)] out string? body,
        [NotNullWhen(false)] out string? error)
    {
        lesson = null;
        body = null;

        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"The file couldn't be read: {exception.Message}";
            return false;
        }

        if (!FrontMatter.TryParse(text, out FrontMatterDocument? document, out string? parseError))
        {
            error = parseError ?? "The front matter couldn't be read.";
            return false;
        }

        IReadOnlyDictionary<string, string> values = document!.Values;
        string? unknown = values.Keys.FirstOrDefault(key => !KnownFields.Contains(key));
        if (unknown is not null)
        {
            error = $"The front matter field \"{unknown}\" isn't recognized. Use only: {string.Join(", ", KnownFields)}.";
            return false;
        }

        string slug = SlugFromFileName(file);
        if (!SlugPattern().IsMatch(slug))
        {
            error = "The file name must use lowercase letters, numbers, and hyphens, such as 01-first-workflow.md.";
            return false;
        }

        if (!TryGetText(values, "title", out string? title, out error)
            || !TryGetText(values, "summary", out string? summary, out error)
            || !TryGetNumber(values, "duration", 1, 240, out int duration, out error)
            || !TryGetNumber(values, "order", 0, 999, out int order, out error))
        {
            return false;
        }

        string difficulty = values.GetValueOrDefault("difficulty", string.Empty);
        if (!Difficulties.Contains(difficulty))
        {
            error = "The \"difficulty\" field must be Beginner, Intermediate, or Advanced.";
            return false;
        }

        string? requires = values.GetValueOrDefault("requires");

        lesson = new Lesson
        {
            Slug = slug,
            Title = title,
            Summary = summary,
            DurationMinutes = duration,
            Difficulty = difficulty,
            Order = order,
            Requires = string.IsNullOrWhiteSpace(requires) ? null : requires.Trim(),
            SourcePath = sourcePath,
            Html = string.Empty,
        };
        body = document.Body;
        error = null;
        return true;
    }

    private static bool TryGetText(
        IReadOnlyDictionary<string, string> values,
        string key,
        [NotNullWhen(true)] out string? text,
        [NotNullWhen(false)] out string? error)
    {
        if (values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            text = value.Trim();
            error = null;
            return true;
        }

        text = null;
        error = $"The \"{key}\" field is missing or empty.";
        return false;
    }

    private static bool TryGetNumber(
        IReadOnlyDictionary<string, string> values,
        string key,
        int min,
        int max,
        out int number,
        [NotNullWhen(false)] out string? error)
    {
        if (values.TryGetValue(key, out string? value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && number >= min
            && number <= max)
        {
            error = null;
            return true;
        }

        number = 0;
        error = $"The \"{key}\" field must be a whole number from {min} to {max}.";
        return false;
    }

    [GeneratedRegex("^[0-9]+-")]
    private static partial Regex OrderPrefix();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
