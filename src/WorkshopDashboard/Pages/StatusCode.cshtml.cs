using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WorkshopDashboard.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class StatusCodeModel : PageModel
{
    public int Code { get; private set; }

    public bool IsNotFound => Code == StatusCodes.Status404NotFound;

    public IActionResult OnGet(int code)
    {
        // This page is only shown by the status code pages middleware. Visiting /status/500 directly returns "Page not found".
        if (HttpContext.Features.Get<IStatusCodeReExecuteFeature>() is null)
        {
            return NotFound();
        }

        Code = code;
        return Page();
    }

    public IActionResult OnPost(int code) => OnGet(code);
}
