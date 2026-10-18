using Microsoft.Extensions.Options;
using WorkshopDashboard.Models;
using WorkshopDashboard.Services;
using WorkshopDashboard.Tests.TestSupport;
using Xunit;

namespace WorkshopDashboard.Tests;

public sealed class LessonCatalogTests : IDisposable
{
    private readonly TempLessons _lessons = new();
    private readonly ListLogger<LessonCatalog> _logger = new();

    public void Dispose() => _lessons.Dispose();

    private LessonCatalog CreateCatalog(string environmentName, string? lessonsPath, string? contentRoot = null)
    {
        var options = new WorkshopOptions { LessonsPath = lessonsPath, RepositoryUrl = DashboardFactory.RepositoryUrl };
        SiteInfo site = SiteInfo.Create(new BuildMetadata(null, null, null, null), options, environmentName);
        var environment = new FakeHostEnvironment(environmentName, contentRoot ?? Path.GetTempPath());

        return new LessonCatalog(Options.Create(options), environment, new MarkdownRenderer(), site, _logger);
    }

    private int CountLogs(LogLevel level) => _logger.Entries.Count(entry => entry.Level == level);

    [Fact]
    public void Development_rereads_the_lessons_on_every_request()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        LessonCatalog catalog = CreateCatalog("Development", _lessons.Root);
        Assert.Single(catalog.GetLessons().Lessons);

        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 1);

        Assert.Equal(2, catalog.GetLessons().Lessons.Count);
    }

    [Fact]
    public void Other_environments_read_the_lessons_once()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        LessonCatalog catalog = CreateCatalog("Production", _lessons.Root);
        LessonCollection first = catalog.GetLessons();

        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 1);

        Assert.Same(first, catalog.GetLessons());
        Assert.Single(catalog.GetLessons().Lessons);
    }

    [Fact]
    public void A_problem_is_logged_once_until_it_changes()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        _lessons.Write("foundation", "01-first-workflow.md", "---\ntitle: Lab 1: First workflow\n---\n");
        LessonCatalog catalog = CreateCatalog("Development", _lessons.Root);

        catalog.GetLessons();
        catalog.GetLessons();
        Assert.Equal(1, CountLogs(LogLevel.Warning));
        Assert.Contains("lessons/foundation/01-first-workflow.md", _logger.Entries[0].Message);

        _lessons.Write("foundation", "01-first-workflow.md", TempLessons.LessonText("Lab 1: First workflow", 1, difficulty: "Expert"));
        catalog.GetLessons();
        catalog.GetLessons();
        Assert.Equal(2, CountLogs(LogLevel.Warning));

        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 1);
        Assert.Equal(2, catalog.GetLessons().Lessons.Count);
        Assert.Equal(2, CountLogs(LogLevel.Warning));
    }

    [Fact]
    public void An_empty_lessons_folder_is_logged_as_an_error()
    {
        LessonCatalog catalog = CreateCatalog("Production", _lessons.Root);

        Assert.Empty(catalog.GetLessons().Lessons);
        (LogLevel level, string message) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Contains(_lessons.Root, message);
    }

    [Fact]
    public void A_relative_path_is_resolved_from_the_content_root()
    {
        string contentRoot = Path.GetDirectoryName(_lessons.Root)!;

        LessonCatalog catalog = CreateCatalog("Production", Path.GetFileName(_lessons.Root), contentRoot);

        Assert.Equal(_lessons.Root, catalog.LessonsPath);
    }

    [Fact]
    public void A_missing_configured_folder_falls_back_to_the_published_lessons()
    {
        string missing = Path.Combine(_lessons.Root, "missing");

        LessonCatalog catalog = CreateCatalog("Production", missing);

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "lessons"), catalog.LessonsPath);
        (LogLevel level, string message) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains(missing, message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void No_configured_folder_uses_the_published_lessons(string? lessonsPath)
    {
        LessonCatalog catalog = CreateCatalog("Production", lessonsPath);

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "lessons"), catalog.LessonsPath);
        Assert.Empty(_logger.Entries);
    }
}
