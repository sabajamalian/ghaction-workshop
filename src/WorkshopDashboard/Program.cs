using Microsoft.Extensions.Options;
using WorkshopDashboard.Models;
using WorkshopDashboard.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WorkshopOptions>(builder.Configuration.GetSection(WorkshopOptions.SectionName));
builder.Services.AddSingleton(services => SiteInfo.Create(
    BuildMetadata.FromAssembly(typeof(Program).Assembly),
    services.GetRequiredService<IOptions<WorkshopOptions>>().Value,
    builder.Environment.EnvironmentName));
builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddSingleton<LessonCatalog>();
builder.Services.AddRazorPages();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/status/{0}");
app.UseSecurityHeaders();
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/health", (HttpContext context, LessonCatalog catalog, SiteInfo site) =>
{
    HealthReport report = HealthReport.Create(site, catalog.GetLessons().Lessons.Count);
    context.Response.Headers.CacheControl = "no-store";
    return Results.Json(
        report,
        statusCode: report.IsHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapRazorPages();

app.Run();
