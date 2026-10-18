namespace WorkshopDashboard.Models;

public sealed class WorkshopOptions
{
    public const string SectionName = "Workshop";

    // Folder that contains the lesson Markdown files. Relative paths resolve against the content root.
    // When this is empty, the app reads the copy of the lessons that the build places next to the app.
    public string? LessonsPath { get; set; }

    // Name shown in the footer and on /health, such as "production".
    // When this is empty, the app uses the ASP.NET Core environment name.
    public string? EnvironmentName { get; set; }

    // Repository that links to non-lesson files point to.
    // When this is empty, the app uses the repository recorded at build time.
    public string? RepositoryUrl { get; set; }
}
