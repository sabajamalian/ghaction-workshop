using System.Text.RegularExpressions;

namespace WorkshopDashboard.Services;

public sealed record RepositoryLink(string Path, string Fragment, bool IsDirectory);

// Decides where each Markdown link points when a lesson is shown on the dashboard.
// Lessons are written so their links also work when someone reads the file on GitHub.
public sealed partial class LinkResolver
{
    public const string BlockedUrl = "#";

    private static readonly HashSet<string> SafeSchemes = new(StringComparer.Ordinal) { "http", "https", "mailto" };

    private readonly string _repositoryUrl;
    private readonly IReadOnlyDictionary<string, string> _lessonSlugsByPath;

    public LinkResolver(string repositoryUrl, IReadOnlyDictionary<string, string> lessonSlugsByPath)
    {
        _repositoryUrl = repositoryUrl.TrimEnd('/');
        _lessonSlugsByPath = lessonSlugsByPath;
    }

    public string Resolve(string? url, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BlockedUrl;
        }

        string trimmed = url.Trim();
        LinkKind kind = Classify(trimmed);

        switch (kind)
        {
            case LinkKind.Fragment:
            case LinkKind.SafeAbsolute:
                return trimmed;
            case LinkKind.Unsafe:
                return BlockedUrl;
        }

        RepositoryLink? target = ResolveRepositoryPath(trimmed, sourcePath);
        if (target is null)
        {
            return BlockedUrl;
        }

        if (_lessonSlugsByPath.TryGetValue(target.Path, out string? slug))
        {
            return "/lessons/" + slug + target.Fragment;
        }

        // On GitHub the README is the workshop's front page and lists every lab, so on the dashboard
        // it opens the lesson list. The README's headings don't exist there, so the fragment is dropped.
        if (target.Path == "README.md")
        {
            return "/";
        }

        string view = target.IsDirectory ? "tree" : "blob";
        string path = string.Join('/', target.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return $"{_repositoryUrl}/{view}/main/{path}{target.Fragment}";
    }

    public static LinkKind Classify(string url)
    {
        // Browsers ignore tabs, line breaks, and control characters in URLs, so "java\tscript:" still runs script.
        string probe = new(url.Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c)).ToArray());

        if (probe.Length == 0)
        {
            return LinkKind.Unsafe;
        }

        if (probe.StartsWith('#'))
        {
            return LinkKind.Fragment;
        }

        Match scheme = SchemePattern().Match(probe);
        if (scheme.Success)
        {
            return SafeSchemes.Contains(scheme.Groups[1].Value.ToLowerInvariant()) ? LinkKind.SafeAbsolute : LinkKind.Unsafe;
        }

        if (probe.StartsWith("//", StringComparison.Ordinal) || probe.Contains('\\'))
        {
            return LinkKind.Unsafe;
        }

        return LinkKind.Relative;
    }

    // Resolves a relative link the way GitHub does: relative to the file that contains it,
    // or relative to the repository root when the link starts with "/".
    public static RepositoryLink? ResolveRepositoryPath(string url, string sourcePath)
    {
        string fragment = string.Empty;
        int hash = url.IndexOf('#');
        if (hash >= 0)
        {
            fragment = url[hash..];
            url = url[..hash];
        }

        int query = url.IndexOf('?');
        if (query >= 0)
        {
            url = url[..query];
        }

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(url);
        }
        catch (UriFormatException)
        {
            return null;
        }

        var segments = new List<string>();
        if (!decoded.StartsWith('/'))
        {
            int lastSlash = sourcePath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                segments.AddRange(sourcePath[..lastSlash].Split('/', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        foreach (string part in decoded.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(part);
        }

        bool isDirectory = decoded.Length == 0 || decoded.EndsWith('/') || segments.Count == 0;
        return new RepositoryLink(string.Join('/', segments), fragment, isDirectory);
    }

    [GeneratedRegex("^([A-Za-z][A-Za-z0-9+.-]*):")]
    private static partial Regex SchemePattern();
}

public enum LinkKind
{
    Relative,
    Fragment,
    SafeAbsolute,
    Unsafe,
}
