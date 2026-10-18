using WorkshopCore;

namespace WorkshopDashboard.Models;

public sealed record LessonTrack(string Id, string Title, string Label, string Description, bool IsRequired)
{
    public static LessonTrack GettingStarted { get; } = new(
        "foundation",
        "Getting started",
        "Required",
        "Start here. These labs run entirely in your browser.",
        IsRequired: true);

    public static LessonTrack Explore { get; } = new(
        "explore",
        "Explore",
        "Optional",
        "Pick the topics you need. Each lab lists its prerequisites at the top.",
        IsRequired: false);

    public static IReadOnlyList<LessonTrack> All { get; } = [GettingStarted, Explore];

    public static LessonTrack? FromFolder(string folderName)
    {
        return All.FirstOrDefault(track => string.Equals(track.Id, folderName, StringComparison.Ordinal));
    }
}

public sealed record Lesson
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required int DurationMinutes { get; init; }
    public required string Difficulty { get; init; }
    public required int Order { get; init; }
    public string? Requires { get; init; }
    public required LessonTrack Track { get; init; }

    // Path relative to the repository root, with forward slashes. For example: lessons/foundation/00-welcome.md
    public required string SourcePath { get; init; }

    public required string Html { get; init; }
}

public sealed record TrackGroup(LessonTrack Track, IReadOnlyList<Lesson> Lessons)
{
    public int TotalMinutes { get; } = Lessons.Aggregate(0, (total, lesson) => Calculator.Add(total, lesson.DurationMinutes));
}

public sealed class LessonCollection
{
    private readonly Dictionary<string, int> _indexBySlug;

    public LessonCollection(IEnumerable<Lesson> lessons)
    {
        Lessons = lessons
            .OrderBy(lesson => lesson.Order)
            .ThenBy(lesson => lesson.SourcePath, StringComparer.Ordinal)
            .ToList();

        _indexBySlug = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Lessons.Count; i++)
        {
            _indexBySlug[Lessons[i].Slug] = i;
        }

        Tracks = LessonTrack.All
            .Select(track => new TrackGroup(track, Lessons.Where(lesson => lesson.Track == track).ToList()))
            .Where(group => group.Lessons.Count > 0)
            .ToList();
    }

    public static LessonCollection Empty { get; } = new([]);

    public IReadOnlyList<Lesson> Lessons { get; }

    public IReadOnlyList<TrackGroup> Tracks { get; }

    public Lesson? FindBySlug(string slug)
    {
        return _indexBySlug.TryGetValue(slug, out int index) ? Lessons[index] : null;
    }

    public Lesson? Previous(Lesson lesson)
    {
        int index = _indexBySlug[lesson.Slug];
        return index > 0 ? Lessons[index - 1] : null;
    }

    public Lesson? Next(Lesson lesson)
    {
        int index = _indexBySlug[lesson.Slug];
        return index < Lessons.Count - 1 ? Lessons[index + 1] : null;
    }
}
