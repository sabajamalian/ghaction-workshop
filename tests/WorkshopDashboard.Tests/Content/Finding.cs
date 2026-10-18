namespace WorkshopDashboard.Tests.Content;

// One problem in one file. Line is 1-based, or 0 when the problem is about the whole file.
internal sealed record Finding(string Path, int Line, string Message)
{
    public override string ToString()
    {
        return Line > 0 ? $"{Path}:{Line}: {Message}" : $"{Path}: {Message}";
    }
}
