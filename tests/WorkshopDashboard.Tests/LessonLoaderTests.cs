using WorkshopDashboard.Models;
using WorkshopDashboard.Services;
using WorkshopDashboard.Tests.TestSupport;
using Xunit;

namespace WorkshopDashboard.Tests;

public sealed class LessonLoaderTests : IDisposable
{
    private readonly TempLessons _lessons = new();

    public void Dispose() => _lessons.Dispose();

    private LessonLoadResult Load() => LessonLoader.Load(_lessons.Root, new MarkdownRenderer(), DashboardFactory.RepositoryUrl);

    [Fact]
    public void Lessons_from_both_folders_are_loaded_into_one_ordered_list()
    {
        _lessons.WriteLesson("explore", "03-triggers.md", "Lab 3: Triggers", 3);
        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 1);
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0, "Go to [Lab 1](01-first-workflow.md).");

        LessonLoadResult result = Load();

        Assert.Empty(result.Errors);
        Assert.Equal(["welcome", "first-workflow", "triggers"], result.Lessons.Lessons.Select(lesson => lesson.Slug));
        Assert.Equal(30, result.Lessons.TotalMinutes);

        Lesson welcome = result.Lessons.Lessons[0];
        Assert.Equal("Lab 0: Welcome", welcome.Title);
        Assert.Equal("lessons/foundation/00-welcome.md", welcome.SourcePath);
        Assert.DoesNotContain("<h1", welcome.Html);
        Assert.Contains("<a href=\"/lessons/first-workflow\">Lab 1</a>", welcome.Html);
    }

    [Fact]
    public void Previous_and_next_follow_the_lesson_order()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 1);
        _lessons.WriteLesson("explore", "03-triggers.md", "Lab 3: Triggers", 3);

        LessonCollection lessons = Load().Lessons;
        Lesson middle = lessons.FindBySlug("FIRST-WORKFLOW")!;

        Assert.Equal("welcome", lessons.Previous(middle)?.Slug);
        Assert.Equal("triggers", lessons.Next(middle)?.Slug);
        Assert.Null(lessons.Previous(lessons.Lessons[0]));
        Assert.Null(lessons.Next(lessons.Lessons[^1]));
    }

    [Fact]
    public void Optional_fields_are_read()
    {
        _lessons.Write(
            "explore",
            "08-environments.md",
            TempLessons.LessonText("Lab 8: Environments", 8, difficulty: "Intermediate", duration: 45, extraFields: "requires: \"A second GitHub account\""));

        Lesson lesson = Assert.Single(Load().Lessons.Lessons);

        Assert.Equal("Intermediate", lesson.Difficulty);
        Assert.Equal(45, lesson.DurationMinutes);
        Assert.Equal("A second GitHub account", lesson.Requires);
    }

    [Theory]
    [InlineData("title: \"\"", "The \"title\" field is missing or empty.")]
    [InlineData("difficulty: Expert", "The \"difficulty\" field must be Beginner, Intermediate, or Advanced.")]
    [InlineData("duration: 0", "The \"duration\" field must be a whole number from 1 to 240.")]
    [InlineData("duration: 241", "The \"duration\" field must be a whole number from 1 to 240.")]
    [InlineData("duration: twenty", "The \"duration\" field must be a whole number from 1 to 240.")]
    [InlineData("duration: -5", "The \"duration\" field must be a whole number from 1 to 240.")]
    [InlineData("order: 1000", "The \"order\" field must be a whole number from 0 to 999.")]
    public void Invalid_fields_skip_the_lesson_with_a_clear_message(string field, string expectedMessage)
    {
        string key = field[..field.IndexOf(':')];
        string valid = TempLessons.LessonText("Lab 1: First workflow", 1);
        string broken = string.Join('\n', valid.Split('\n').Select(line => line.StartsWith(key + ":", StringComparison.Ordinal) ? field : line));
        _lessons.Write("foundation", "01-first-workflow.md", broken);
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);

        LessonLoadResult result = Load();

        LessonLoadError error = Assert.Single(result.Errors);
        Assert.Equal("lessons/foundation/01-first-workflow.md", error.SourcePath);
        Assert.Equal(expectedMessage, error.Message);
        Assert.Equal("welcome", Assert.Single(result.Lessons.Lessons).Slug);
    }

    [Fact]
    public void Missing_fields_are_reported()
    {
        _lessons.Write("foundation", "00-welcome.md", "---\ntitle: Welcome\n---\n");

        LessonLoadError error = Assert.Single(Load().Errors);

        Assert.Equal("The \"summary\" field is missing or empty.", error.Message);
    }

    [Fact]
    public void Unknown_fields_are_reported()
    {
        _lessons.Write("foundation", "00-welcome.md", TempLessons.LessonText("Lab 0: Welcome", 0, extraFields: "author: Octo"));

        LessonLoadError error = Assert.Single(Load().Errors);

        Assert.StartsWith("The front matter field \"author\" isn't recognized.", error.Message);
    }

    [Fact]
    public void Invalid_front_matter_is_reported()
    {
        _lessons.Write("foundation", "00-welcome.md", "---\ntitle: Lab 0: Welcome\n---\n");

        LessonLoadError error = Assert.Single(Load().Errors);

        Assert.Contains("Wrap the value in double quotes.", error.Message);
    }

    [Theory]
    [InlineData("01-First-Workflow.md")]
    [InlineData("01-first_workflow.md")]
    [InlineData("01-first--workflow.md")]
    public void File_names_must_be_lowercase_words_and_hyphens(string fileName)
    {
        _lessons.WriteLesson("foundation", fileName, "Lab 1: First workflow", 1);

        LessonLoadError error = Assert.Single(Load().Errors);

        Assert.Contains("must use lowercase letters, numbers, and hyphens", error.Message);
    }

    [Fact]
    public void Duplicate_orders_keep_the_first_lesson()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 0);

        LessonLoadResult result = Load();

        Assert.Equal("welcome", Assert.Single(result.Lessons.Lessons).Slug);
        LessonLoadError error = Assert.Single(result.Errors);
        Assert.Equal("lessons/foundation/01-first-workflow.md", error.SourcePath);
        Assert.Equal("Another lesson already uses order 0. Each lesson needs a unique order.", error.Message);
    }

    [Fact]
    public void Duplicate_addresses_across_tracks_keep_the_first_lesson()
    {
        _lessons.WriteLesson("foundation", "01-intro.md", "Lab 1: Intro", 1);
        _lessons.WriteLesson("explore", "05-intro.md", "Lab 5: Intro", 5);

        LessonLoadResult result = Load();

        Assert.Equal("lessons/foundation/01-intro.md", Assert.Single(result.Lessons.Lessons).SourcePath);
        Assert.Equal("Another lesson already uses the address /lessons/intro. Rename one of the files.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Files_outside_the_track_folders_are_ignored()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        File.WriteAllText(Path.Combine(_lessons.Root, "README.md"), "# Not a lesson");
        _lessons.Write("drafts", "09-draft.md", TempLessons.LessonText("Draft", 9));
        _lessons.Write("foundation", "notes.txt", "Not Markdown");

        LessonLoadResult result = Load();

        Assert.Empty(result.Errors);
        Assert.Equal("welcome", Assert.Single(result.Lessons.Lessons).Slug);
    }

    [Fact]
    public void A_missing_folder_is_reported()
    {
        string missing = Path.Combine(_lessons.Root, "does-not-exist");

        LessonLoadResult result = LessonLoader.Load(missing, new MarkdownRenderer(), DashboardFactory.RepositoryUrl);

        Assert.Empty(result.Lessons.Lessons);
        Assert.Equal($"The lessons folder doesn't exist: {missing}", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("01-first-workflow.md", "first-workflow")]
    [InlineData("first-workflow.md", "first-workflow")]
    [InlineData("12-ship-it.md", "ship-it")]
    public void Addresses_come_from_the_file_name(string fileName, string expected)
    {
        Assert.Equal(expected, LessonLoader.SlugFromFileName(fileName));
    }
}
