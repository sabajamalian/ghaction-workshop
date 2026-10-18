namespace WorkshopDashboard.Tests.TestSupport;

// A lessons folder in the system temp directory that is deleted when the test finishes.
internal sealed class TempLessons : IDisposable
{
    public TempLessons()
    {
        Root = Path.Combine(Path.GetTempPath(), "workshop-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public static string LessonText(
        string title,
        int order,
        string body = "Some lesson text.",
        string summary = "A short summary.",
        int duration = 10,
        string difficulty = "Beginner",
        string? extraFields = null)
    {
        string extra = extraFields is null ? string.Empty : extraFields.TrimEnd('\n') + "\n";
        return "---\n"
            + $"title: \"{title}\"\n"
            + $"summary: \"{summary}\"\n"
            + $"duration: {duration}\n"
            + $"difficulty: {difficulty}\n"
            + $"order: {order}\n"
            + extra
            + "---\n\n"
            + $"# {title}\n\n"
            + body + "\n";
    }

    public string Write(string track, string fileName, string content)
    {
        string folder = Path.Combine(Root, track);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    public string WriteLesson(string track, string fileName, string title, int order, string body = "Some lesson text.")
    {
        return Write(track, fileName, LessonText(title, order, body));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Another process may still hold a file open. The OS cleans the temp folder eventually.
        }
    }
}
