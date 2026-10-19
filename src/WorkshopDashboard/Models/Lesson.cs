using WorkshopCore;

namespace WorkshopDashboard.Models;

public sealed record Lesson
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required int DurationMinutes { get; init; }
    public required string Difficulty { get; init; }
    public required int Order { get; init; }
    public string? Requires { get; init; }

    // True for the labs in lessons/foundation, which every learner does first, in order.
    public bool IsFoundation { get; init; }

    // Path relative to the repository root, with forward slashes. For example: lessons/foundation/00-welcome.md
    public required string SourcePath { get; init; }

    public required string Html { get; init; }
}

public sealed class LessonCollection
{
    private readonly Dictionary<string, int> _indexBySlug;

    public LessonCollection(IEnumerable<Lesson> lessons)
    {
        Lessons = lessons
            .OrderByDescending(lesson => lesson.IsFoundation)
            .ThenBy(lesson => lesson.Order)
            .ThenBy(lesson => lesson.SourcePath, StringComparer.Ordinal)
            .ToList();

        _indexBySlug = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Lessons.Count; i++)
        {
            _indexBySlug[Lessons[i].Slug] = i;
        }

        Foundation = Lessons.Where(lesson => lesson.IsFoundation).ToList();
        Explore = Lessons.Where(lesson => !lesson.IsFoundation).ToList();

        TotalMinutes = Lessons.Aggregate(0, (total, lesson) => Calculator.Add(total, lesson.DurationMinutes));
    }

    public static LessonCollection Empty { get; } = new([]);

    // Every lesson: the foundation labs first, then the explore labs, each sorted by order.
    public IReadOnlyList<Lesson> Lessons { get; }

    public IReadOnlyList<Lesson> Foundation { get; }

    public IReadOnlyList<Lesson> Explore { get; }

    public int TotalMinutes { get; }

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
