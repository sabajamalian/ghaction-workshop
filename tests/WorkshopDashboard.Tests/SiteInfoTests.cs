using WorkshopDashboard.Models;
using WorkshopDashboard.Services;
using Xunit;

namespace WorkshopDashboard.Tests;

public class SiteInfoTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private const string Repo = "https://github.com/octo/workshop";

    [Fact]
    public void A_local_build_without_metadata_uses_fallbacks()
    {
        SiteInfo site = SiteInfo.Create(new BuildMetadata(null, null, null, null), new WorkshopOptions(), "Development");

        Assert.Equal(SiteInfo.Unknown, site.Version);
        Assert.Equal(SiteInfo.Unknown, site.Commit);
        Assert.Equal(SiteInfo.LocalBuild, site.BuildTimestamp);
        Assert.Equal(SiteInfo.LocalBuild, site.BuildTimestampDisplay);
        Assert.Equal("Development", site.EnvironmentName);
        Assert.Equal(SiteInfo.DefaultRepositoryUrl, site.RepositoryUrl);
        Assert.False(site.RepositoryUrlIsKnown);
        Assert.False(site.HasCommitSha);
        Assert.Equal(SiteInfo.Unknown, site.ShortCommit);
        Assert.Null(site.CommitUrl);
    }

    [Fact]
    public void A_github_actions_build_links_to_its_commit()
    {
        var build = new BuildMetadata("1.0.0+" + Sha, Sha, "2026-10-17T21:05:09.1234567Z", Repo);

        SiteInfo site = SiteInfo.Create(build, new WorkshopOptions(), "Production");

        Assert.Equal("1.0.0", site.Version);
        Assert.Equal(Sha, site.Commit);
        Assert.Equal("0123456", site.ShortCommit);
        Assert.Equal(Repo, site.RepositoryUrl);
        Assert.True(site.RepositoryUrlIsKnown);
        Assert.Equal($"{Repo}/commit/{Sha}", site.CommitUrl);
        Assert.Equal("2026-10-17 21:05 UTC", site.BuildTimestampDisplay);
    }

    [Fact]
    public void A_local_build_shows_the_commit_without_linking_to_the_fallback_repository()
    {
        SiteInfo site = SiteInfo.Create(new BuildMetadata("1.0.0+" + Sha, null, null, null), new WorkshopOptions(), "Development");

        Assert.Equal("1.0.0", site.Version);
        Assert.Equal(Sha, site.Commit);
        Assert.Null(site.CommitUrl);
    }

    [Theory]
    [InlineData("not-a-sha")]
    [InlineData("0123456")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567")]
    public void Values_that_are_not_full_commit_shas_are_ignored(string commit)
    {
        SiteInfo site = SiteInfo.Create(new BuildMetadata("1.0.0+" + commit, commit, null, Repo), new WorkshopOptions(), "Production");

        Assert.Equal(SiteInfo.Unknown, site.Commit);
        Assert.Null(site.CommitUrl);
    }

    [Fact]
    public void Configuration_overrides_build_metadata()
    {
        var build = new BuildMetadata("1.0.0", Sha, null, Repo);
        var options = new WorkshopOptions { RepositoryUrl = "https://github.com/learner/fork.git", EnvironmentName = " production " };

        SiteInfo site = SiteInfo.Create(build, options, "Staging");

        Assert.Equal("https://github.com/learner/fork", site.RepositoryUrl);
        Assert.Equal("production", site.EnvironmentName);
        Assert.Equal($"https://github.com/learner/fork/commit/{Sha}", site.CommitUrl);
    }

    [Fact]
    public void An_invalid_configured_repository_falls_back_to_build_metadata()
    {
        var options = new WorkshopOptions { RepositoryUrl = "git@github.com:learner/fork.git" };

        SiteInfo site = SiteInfo.Create(new BuildMetadata(null, null, null, Repo), options, "Production");

        Assert.Equal(Repo, site.RepositoryUrl);
    }

    [Theory]
    [InlineData("not a date", "not a date")]
    [InlineData("2026-01-02T03:04:05+02:00", "2026-01-02 01:04 UTC")]
    public void Build_timestamps_are_shown_in_utc_when_possible(string timestamp, string expected)
    {
        SiteInfo site = SiteInfo.Create(new BuildMetadata(null, null, timestamp, null), new WorkshopOptions(), "Production");

        Assert.Equal(expected, site.BuildTimestampDisplay);
    }

    [Theory]
    [InlineData("https://github.com/octo/workshop", Repo)]
    [InlineData("https://github.com/octo/workshop/", Repo)]
    [InlineData("https://github.com/octo/workshop.git", Repo)]
    [InlineData(" https://github.com/octo/workshop.git/ ", Repo)]
    [InlineData("https://ghe.example.com/octo/workshop", "https://ghe.example.com/octo/workshop")]
    public void Repository_urls_are_normalized(string value, string expected)
    {
        Assert.Equal(expected, SiteInfo.NormalizeRepositoryUrl(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://github.com/octo/workshop")]
    [InlineData("git@github.com:octo/workshop.git")]
    [InlineData("ssh://git@github.com/octo/workshop.git")]
    [InlineData("https://user:token@github.com/octo/workshop")]
    [InlineData("https://github.com/octo/workshop?tab=readme")]
    [InlineData("https://github.com/octo/workshop#readme")]
    [InlineData("https://github.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/octo/workshop")]
    public void Unusable_repository_urls_are_rejected(string? value)
    {
        Assert.Null(SiteInfo.NormalizeRepositoryUrl(value));
    }

    [Theory]
    [InlineData(1, "1 minute")]
    [InlineData(15, "15 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(61, "1 hour 1 minute")]
    [InlineData(135, "2 hours 15 minutes")]
    public void Durations_are_written_out(int minutes, string expected)
    {
        Assert.Equal(expected, DisplayFormat.Duration(minutes));
    }

    [Theory]
    [InlineData(1, "1 lab")]
    [InlineData(3, "3 labs")]
    public void Lab_counts_are_written_out(int count, string expected)
    {
        Assert.Equal(expected, DisplayFormat.LabCount(count));
    }

    [Theory]
    [InlineData(0, "unhealthy", false)]
    [InlineData(3, "healthy", true)]
    public void Health_depends_on_having_lessons(int lessons, string status, bool healthy)
    {
        SiteInfo site = SiteInfo.Create(new BuildMetadata(null, null, null, null), new WorkshopOptions(), "Production");

        HealthReport report = HealthReport.Create(site, lessons);

        Assert.Equal(status, report.Status);
        Assert.Equal(healthy, report.IsHealthy);
        Assert.Equal(lessons, report.Lessons);
    }
}
