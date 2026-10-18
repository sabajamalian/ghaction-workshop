using WorkshopDashboard.Tests.Content;

namespace WorkshopDashboard.Tests.TestSupport;

// A small repository in the system temp directory, for testing the content rules. It's deleted when the test finishes.
internal sealed class TempRepository : IDisposable
{
    public TempRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "workshop-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public TempRepository Write(string path, string content)
    {
        string fullPath = Path.Combine(Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return this;
    }

    public ContentRepository Load()
    {
        return ContentRepository.Load(Root);
    }

    // Loads the repository and parses one of its Markdown files.
    public (ContentRepository Repository, MarkdownFile File) LoadMarkdown(string path)
    {
        ContentRepository repository = Load();
        return (repository, repository.Markdown(path));
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
