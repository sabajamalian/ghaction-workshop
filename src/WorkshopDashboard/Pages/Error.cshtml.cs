using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WorkshopDashboard.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErrorModel : PageModel
{
    public string? RequestId { get; private set; }

    public IActionResult OnGet()
    {
        // This page is only shown by the exception handler. Visiting /Error directly returns "Page not found".
        if (HttpContext.Features.Get<IExceptionHandlerPathFeature>() is null)
        {
            return NotFound();
        }

        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return Page();
    }

    public IActionResult OnPost() => OnGet();
}
