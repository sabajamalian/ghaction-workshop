using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Markdig.Helpers;
using Markdig.Syntax;

namespace WorkshopDashboard.Tests.Content;

// One "uses:" line in a workflow file, or in a YAML block or front matter in a Markdown file.
internal sealed record ActionReference(string Path, int Line, string Target, string? Comment)
{
    public string Location => $"{Path}:{Line}";
}

// Third-party code runs with the workflow's permissions, so every action is pinned to a full commit SHA,
// with the release it came from in a comment. The lessons copy the workflows, so each action uses one pinned release everywhere.
internal static partial class ActionPins
{
    public static List<ActionReference> FindReferences(ContentRepository repository)
    {
        var references = new List<ActionReference>();
        foreach (string path in repository.FileList)
        {
            if (IsYamlFile(path))
            {
                string[] lines = repository.ReadText(path).Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (TryParse(path, i + 1, lines[i], out ActionReference? reference))
                    {
                        references.Add(reference);
                    }
                }
            }
            else if (ContentRepository.IsMarkdown(path))
            {
                references.AddRange(FindReferences(repository.Markdown(path)));
            }
        }

        return references;
    }

    public static IEnumerable<ActionReference> FindReferences(MarkdownFile file)
    {
        // Front matter can hold workflow YAML too, such as the top of an agentic workflow file.
        for (int line = 2; line < file.FrontMatterLineCount; line++)
        {
            if (TryParse(file.Path, line, file.LineText(line), out ActionReference? reference))
            {
                yield return reference;
            }
        }

        foreach (FencedCodeBlock block in file.Document.Descendants<FencedCodeBlock>())
        {
            if (!string.Equals(block.Info, "yaml", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(block.Info, "yml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (int i = 0; i < block.Lines.Count; i++)
            {
                StringLine line = block.Lines.Lines[i];
                if (TryParse(file.Path, line.Line + 1, line.Slice.ToString(), out ActionReference? reference))
                {
                    yield return reference;
                }
            }
        }
    }

    public static bool TryParse(string path, int line, string text, [NotNullWhen(true)] out ActionReference? reference)
    {
        Match match = UsesLine().Match(text);
        if (!match.Success)
        {
            reference = null;
            return false;
        }

        string value = match.Groups["value"].Value.Trim();
        string? comment = null;
        int hash = value.IndexOf(" #", StringComparison.Ordinal);
        if (hash >= 0)
        {
            comment = value[(hash + 2)..].Trim();
            value = value[..hash].Trim();
        }

        reference = new ActionReference(path, line, value.Trim('"', '\''), comment);
        return true;
    }

    public static List<Finding> CheckPins(ContentRepository repository)
    {
        return CheckPins(FindReferences(repository)).ToList();
    }

    public static IEnumerable<Finding> CheckPins(IEnumerable<ActionReference> references)
    {
        foreach (ActionReference reference in references)
        {
            string target = reference.Target;
            if (IsLocal(target))
            {
                continue;
            }

            if (target.StartsWith("docker://", StringComparison.Ordinal))
            {
                if (!target.Contains("@sha256:", StringComparison.Ordinal))
                {
                    yield return new Finding(reference.Path, reference.Line, $"Pin {target} to an image digest, such as docker://alpine@sha256:DIGEST, so the image can't change under the workflow.");
                }

                continue;
            }

            int at = target.LastIndexOf('@');
            if (at <= 0)
            {
                yield return new Finding(reference.Path, reference.Line, $"{target} isn't pinned. Add @ and the full commit SHA, then the release in a comment, such as actions/checkout@SHA # v7.0.1.");
                continue;
            }

            string action = target[..at];
            string version = target[(at + 1)..];
            if (!FullSha().IsMatch(version))
            {
                yield return new Finding(reference.Path, reference.Line, $"{action} is pinned to {version}, which can be moved to different code. Pin it to the release's full 40-character commit SHA and put the release in a comment: {action}@SHA # {(VersionComment().IsMatch(version) ? version : "v1.2.3")}");
            }
            else if (reference.Comment is null || !VersionComment().IsMatch(reference.Comment))
            {
                yield return new Finding(reference.Path, reference.Line, $"Add the release after the SHA as a comment, such as # v1.2.3, so readers can tell which version {action} is pinned to.");
            }
        }
    }

    public static List<Finding> CheckConsistency(ContentRepository repository)
    {
        return CheckConsistency(FindReferences(repository)).ToList();
    }

    // Actions from the same repository, such as github/codeql-action/init and github/codeql-action/analyze, share one release.
    public static IEnumerable<Finding> CheckConsistency(IEnumerable<ActionReference> references)
    {
        var pinned = references
            .Select(reference => (Reference: reference, Pin: SplitPin(reference)))
            .Where(item => item.Pin is not null)
            .GroupBy(item => item.Pin!.Value.Repository, StringComparer.OrdinalIgnoreCase);

        foreach (var group in pinned.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var versions = group
                .GroupBy(item => $"{item.Pin!.Value.Sha} # {item.Reference.Comment}", StringComparer.Ordinal)
                .OrderBy(version => version.Key, StringComparer.Ordinal)
                .ToList();

            if (versions.Count < 2)
            {
                continue;
            }

            ActionReference first = group
                .Select(item => item.Reference)
                .OrderBy(reference => reference.Path, StringComparer.Ordinal)
                .ThenBy(reference => reference.Line)
                .First();

            IEnumerable<string> details = versions.Select(version =>
            {
                List<string> locations = version.Select(item => item.Reference.Location).Order(StringComparer.Ordinal).ToList();
                string shown = string.Join(", ", locations.Take(5)) + (locations.Count > 5 ? $", and {locations.Count - 5} more" : string.Empty);
                return $"{version.Key} ({shown})";
            });

            yield return new Finding(first.Path, first.Line, $"{group.Key} is pinned to {versions.Count} different versions: {string.Join("; ", details)}. Use the same commit everywhere, so the lessons match the workflows.");
        }
    }

    // ./ is relative to the checked-out workspace; $/ is GitHub's self-repository syntax for the running commit.
    private static bool IsLocal(string target) =>
        target.StartsWith("./", StringComparison.Ordinal) || target.StartsWith("$/", StringComparison.Ordinal);

    private static (string Repository, string Sha)? SplitPin(ActionReference reference)
    {
        int at = reference.Target.LastIndexOf('@');
        if (at <= 0 || IsLocal(reference.Target) || reference.Target.StartsWith("docker://", StringComparison.Ordinal))
        {
            return null;
        }

        string sha = reference.Target[(at + 1)..];
        if (!FullSha().IsMatch(sha))
        {
            return null;
        }

        string[] parts = reference.Target[..at].Split('/');
        string repository = parts.Length >= 2 ? parts[0] + "/" + parts[1] : parts[0];
        return (repository, sha);
    }

    private static bool IsYamlFile(string path)
    {
        return path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^\s*(?:-\s+)?uses:\s*(?<value>\S.*)$")]
    private static partial Regex UsesLine();

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex FullSha();

    [GeneratedRegex(@"^v\d+(?:\.\d+){0,2}$")]
    private static partial Regex VersionComment();
}
