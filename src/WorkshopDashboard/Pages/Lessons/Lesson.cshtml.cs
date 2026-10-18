using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkshopDashboard.Models;
using WorkshopDashboard.Services;

namespace WorkshopDashboard.Pages.Lessons;

public sealed class LessonModel(LessonCatalog catalog) : PageModel
{
    public Lesson Lesson { get; private set; } = null!;

    public Lesson? Previous { get; private set; }

    public Lesson? Next { get; private set; }

    public IActionResult OnGet(string slug)
    {
        LessonCollection lessons = catalog.GetLessons();
        Lesson? lesson = lessons.FindBySlug(slug);
        if (lesson is null)
        {
            return NotFound();
        }

        if (!string.Equals(slug, lesson.Slug, StringComparison.Ordinal))
        {
            return RedirectPermanent($"/lessons/{lesson.Slug}");
        }

        Lesson = lesson;
        Previous = lessons.Previous(lesson);
        Next = lessons.Next(lesson);
        return Page();
    }
}
