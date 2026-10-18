using Microsoft.Extensions.Options;
using WorkshopDashboard.Models;

namespace WorkshopDashboard.Services;

// Gives pages the current set of lessons.
// In Development, it rereads the files on every request so lesson edits show up after a browser refresh.
public sealed class LessonCatalog
{
    private readonly MarkdownRenderer _renderer;
    private readonly SiteInfo _site;
    private readonly ILogger<LessonCatalog> _logger;
    private readonly bool _reloadOnEachRequest;
    private readonly Lazy<LessonCollection> _cached;
    private string? _lastProblemReport;

    public LessonCatalog(
        IOptions<WorkshopOptions> options,
        IHostEnvironment environment,
        MarkdownRenderer renderer,
        SiteInfo site,
        ILogger<LessonCatalog> logger)
    {
        _renderer = renderer;
        _site = site;
        _logger = logger;
        _reloadOnEachRequest = environment.IsDevelopment();
        LessonsPath = ResolveLessonsPath(options.Value.LessonsPath, environment.ContentRootPath, logger);
        _cached = new Lazy<LessonCollection>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string LessonsPath { get; }

    public LessonCollection GetLessons()
    {
        return _reloadOnEachRequest ? Load() : _cached.Value;
    }

    private LessonCollection Load()
    {
        LessonLoadResult result = LessonLoader.Load(LessonsPath, _renderer, _site.RepositoryUrl);

        // Development rereads the files on every request, so only log a problem when it first appears.
        string report = string.Join('\n', result.Errors.Select(error => $"{error.SourcePath}: {error.Message}"))
            + (result.Lessons.Lessons.Count == 0 ? "\n(no lessons)" : string.Empty);
        if (Interlocked.Exchange(ref _lastProblemReport, report) == report)
        {
            return result.Lessons;
        }

        foreach (LessonLoadError error in result.Errors)
        {
            _logger.LogWarning("Skipped lesson {SourcePath}: {Message}", error.SourcePath, error.Message);
        }

        if (result.Lessons.Lessons.Count == 0)
        {
            _logger.LogError("No lessons were loaded from {LessonsPath}.", LessonsPath);
        }

        return result.Lessons;
    }

    private static string ResolveLessonsPath(string? configuredPath, string contentRootPath, ILogger logger)
    {
        string publishedPath = Path.Combine(AppContext.BaseDirectory, "lessons");
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return publishedPath;
        }

        string fullPath = Path.GetFullPath(configuredPath, contentRootPath);
        if (Directory.Exists(fullPath))
        {
            return fullPath;
        }

        logger.LogWarning(
            "The configured lessons folder {ConfiguredPath} doesn't exist at {FullPath}. Using {PublishedPath} instead.",
            configuredPath,
            fullPath,
            publishedPath);
        return publishedPath;
    }
}
