using System.Collections.Concurrent;

namespace WorkshopDashboard.Tests.Content;

// The files a contributor would commit, indexed by their path in the repository.
// Paths use "/" and are compared case-sensitively, because paths on GitHub are case-sensitive
// even when the local file system isn't.
internal sealed class ContentRepository
{
    public const string SolutionFile = "GitHubActionsWorkshop.slnx";

    private readonly ConcurrentDictionary<string, string> _text = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MarkdownFile> _markdown = new(StringComparer.Ordinal);

    private ContentRepository(string root, List<string> files)
    {
        Root = root;
        FileList = files;
        Files = files.ToHashSet(StringComparer.Ordinal);

        // Git doesn't store empty folders, so a folder only exists if it contains a file.
        var directories = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
        foreach (string file in files)
        {
            for (int slash = file.IndexOf('/'); slash >= 0; slash = file.IndexOf('/', slash + 1))
            {
                directories.Add(file[..slash]);
            }
        }

        Directories = directories;
    }

    public string Root { get; }

    // Every file, sorted by path.
    public IReadOnlyList<string> FileList { get; }

    public IReadOnlySet<string> Files { get; }

    public IReadOnlySet<string> Directories { get; }

    public IEnumerable<MarkdownFile> MarkdownFiles => FileList.Where(IsMarkdown).Select(Markdown);

    public static ContentRepository Load(string root)
    {
        GitIgnore ignore = GitIgnore.Read(root);
        var files = new List<string>();
        AddFiles(root, string.Empty, ignore, files);
        files.Sort(StringComparer.Ordinal);
        return new ContentRepository(root, files);
    }

    public static string FindRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Couldn't find {SolutionFile} in {AppContext.BaseDirectory} or any folder above it.");
    }

    public static bool IsMarkdown(string path)
    {
        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string text)
    {
        string withoutMark = text.StartsWith('\uFEFF') ? text[1..] : text;
        return withoutMark.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public bool Exists(string path)
    {
        return Files.Contains(path) || Directories.Contains(path);
    }

    public string ReadText(string path)
    {
        return _text.GetOrAdd(path, key => Normalize(File.ReadAllText(Path.Combine(Root, key))));
    }

    public MarkdownFile Markdown(string path)
    {
        return _markdown.GetOrAdd(path, key => MarkdownFile.Parse(key, ReadText(key)));
    }

    private static void AddFiles(string folder, string relative, GitIgnore ignore, List<string> files)
    {
        foreach (string directory in Directory.EnumerateDirectories(folder))
        {
            string name = Path.GetFileName(directory);

            // .github holds the workflows. Other dot folders hold Git's own data or local tool caches.
            if ((name.StartsWith('.') && name != ".github") || ignore.IgnoresFolder(name))
            {
                continue;
            }

            AddFiles(directory, Join(relative, name), ignore, files);
        }

        foreach (string file in Directory.EnumerateFiles(folder))
        {
            string name = Path.GetFileName(file);
            if (!ignore.IgnoresFile(name))
            {
                files.Add(Join(relative, name));
            }
        }
    }

    private static string Join(string folder, string name)
    {
        return folder.Length == 0 ? name : folder + "/" + name;
    }
}

// Reads the kinds of pattern this repository's .gitignore uses: folder names ("bin/"), file names, and extensions ("*.trx").
// Any other pattern stops the tests with an error, so a new pattern can't be misread without anyone noticing.
internal sealed class GitIgnore
{
    private readonly HashSet<string> _folders = new(StringComparer.Ordinal);
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly List<string> _extensions = [];

    public static GitIgnore Read(string root)
    {
        var ignore = new GitIgnore();
        string path = Path.Combine(root, ".gitignore");
        if (!File.Exists(path))
        {
            return ignore;
        }

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            bool folderOnly = line.EndsWith('/');
            string name = folderOnly ? line[..^1] : line;

            if (!folderOnly && name.StartsWith("*.", StringComparison.Ordinal) && IsPlainName(name[2..]))
            {
                ignore._extensions.Add(name[1..]);
            }
            else if (IsPlainName(name))
            {
                (folderOnly ? ignore._folders : ignore._names).Add(name);
            }
            else
            {
                throw new NotSupportedException(
                    $"The content tests can't read the .gitignore pattern \"{line}\". Add support for it in {nameof(GitIgnore)}.");
            }
        }

        return ignore;
    }

    public bool IgnoresFolder(string name)
    {
        return _folders.Contains(name) || _names.Contains(name);
    }

    public bool IgnoresFile(string name)
    {
        return _names.Contains(name) || _extensions.Exists(extension => name.EndsWith(extension, StringComparison.Ordinal));
    }

    private static bool IsPlainName(string name)
    {
        return name.Length > 0 && name.IndexOfAny(['/', '\\', '*', '?', '[', '!']) < 0;
    }
}
