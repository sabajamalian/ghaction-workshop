using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkshopDashboard.Models;
using WorkshopDashboard.Services;

namespace WorkshopDashboard.Pages;

public sealed record LessonGroup(string Id, string Title, string Description, IReadOnlyList<Lesson> Lessons);

public sealed class IndexModel(LessonCatalog catalog) : PageModel
{
    public LessonCollection Lessons { get; private set; } = LessonCollection.Empty;

    public Lesson? FirstLesson => Lessons.Lessons.Count > 0 ? Lessons.Lessons[0] : null;

    // The groups that have at least one lesson, foundation labs first.
    public IReadOnlyList<LessonGroup> Groups { get; private set; } = [];

    public void OnGet()
    {
        Lessons = catalog.GetLessons();

        var groups = new List<LessonGroup>();
        if (Lessons.Foundation.Count > 0)
        {
            groups.Add(new LessonGroup(
                "foundation",
                "Foundation labs (required)",
                "Do these first, in order. Every other lab uses the repository, the workflow, and the tests they set up.",
                Lessons.Foundation));
        }

        if (Lessons.Explore.Count > 0)
        {
            groups.Add(new LessonGroup(
                "explore",
                "Explore labs",
                "After the foundation labs, do these in any order. Each lab lists what it needs at the top.",
                Lessons.Explore));
        }

        Groups = groups;
    }
}
