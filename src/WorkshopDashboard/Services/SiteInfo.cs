using System.Globalization;
using System.Text.RegularExpressions;
using WorkshopDashboard.Models;

namespace WorkshopDashboard.Services;

// What the footer and /health report about this copy of the app.
public sealed partial class SiteInfo
{
    public const string DefaultRepositoryUrl = "https://github.com/sabajamalian/ghaction-workshop";
    public const string Unknown = "unknown";
    public const string LocalBuild = "local build";

    public required string Version { get; init; }
    public required string Commit { get; init; }
    public required string BuildTimestamp { get; init; }
    public required string EnvironmentName { get; init; }
    public required string RepositoryUrl { get; init; }

    // False when RepositoryUrl is only the fallback, which may not be the repository the build came from.
    public bool RepositoryUrlIsKnown { get; init; }

    public bool HasCommitSha => CommitShaPattern().IsMatch(Commit);

    public string ShortCommit => HasCommitSha ? Commit[..7] : Commit;

    public string? CommitUrl => HasCommitSha && RepositoryUrlIsKnown ? $"{RepositoryUrl}/commit/{Commit}" : null;

    public string BuildTimestampDisplay
    {
        get
        {
            if (DateTimeOffset.TryParse(BuildTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset value))
            {
                return value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            }

            return BuildTimestamp;
        }
    }

    public static SiteInfo Create(BuildMetadata build, WorkshopOptions options, string hostEnvironmentName)
    {
        string? informationalVersion = build.InformationalVersion;
        string version = Unknown;
        string? commitFromVersion = null;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // The .NET SDK appends "+<commit SHA>" to the version when it builds inside a Git repository.
            int plus = informationalVersion.IndexOf('+');
            version = plus >= 0 ? informationalVersion[..plus] : informationalVersion;
            commitFromVersion = plus >= 0 ? informationalVersion[(plus + 1)..] : null;
        }

        string commit = FirstValid(build.CommitSha, commitFromVersion) ?? Unknown;
        string? repositoryUrl = NormalizeRepositoryUrl(options.RepositoryUrl) ?? NormalizeRepositoryUrl(build.RepositoryUrl);

        return new SiteInfo
        {
            Version = version,
            Commit = commit,
            BuildTimestamp = string.IsNullOrWhiteSpace(build.BuildTimestamp) ? LocalBuild : build.BuildTimestamp,
            EnvironmentName = string.IsNullOrWhiteSpace(options.EnvironmentName) ? hostEnvironmentName : options.EnvironmentName.Trim(),
            RepositoryUrl = repositoryUrl ?? DefaultRepositoryUrl,
            RepositoryUrlIsKnown = repositoryUrl is not null,
        };
    }

    // Accepts only plain https URLs, so a Git remote such as git@github.com:owner/repo.git
    // or a URL with credentials in it is ignored.
    public static string? NormalizeRepositoryUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim().TrimEnd('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.UserInfo.Length == 0
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && uri.AbsolutePath.Trim('/').Length > 0
            ? trimmed
            : null;
    }

    private static string? FirstValid(params string?[] candidates)
    {
        return candidates.FirstOrDefault(candidate => candidate is not null && CommitShaPattern().IsMatch(candidate));
    }

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex CommitShaPattern();
}
