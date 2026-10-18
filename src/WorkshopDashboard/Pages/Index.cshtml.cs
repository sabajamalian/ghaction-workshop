using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkshopDashboard.Models;
using WorkshopDashboard.Services;

namespace WorkshopDashboard.Pages;

public sealed class IndexModel(LessonCatalog catalog) : PageModel
{
    public LessonCollection Lessons { get; private set; } = LessonCollection.Empty;

    public Lesson? FirstLesson => Lessons.Lessons.Count > 0 ? Lessons.Lessons[0] : null;

    public void OnGet()
    {
        Lessons = catalog.GetLessons();
    }
}
