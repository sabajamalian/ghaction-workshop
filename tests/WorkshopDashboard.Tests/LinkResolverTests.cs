using WorkshopDashboard.Services;
using WorkshopDashboard.Tests.TestSupport;
using Xunit;

namespace WorkshopDashboard.Tests;

public class LinkResolverTests
{
    private const string Repo = DashboardFactory.RepositoryUrl;
    private const string SourcePath = "lessons/foundation/00-welcome.md";

    private static readonly LinkResolver Links = new(
        Repo + "/",
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lessons/foundation/00-welcome.md"] = "welcome",
            ["lessons/foundation/01-first-workflow.md"] = "first-workflow",
            ["lessons/explore/03-triggers.md"] = "triggers",
        });

    [Theory]
    [InlineData("01-first-workflow.md", "/lessons/first-workflow")]
    [InlineData("./01-first-workflow.md#run-it", "/lessons/first-workflow#run-it")]
    [InlineData("../explore/03-triggers.md", "/lessons/triggers")]
    [InlineData("/lessons/explore/03-triggers.md", "/lessons/triggers")]
    [InlineData("01-first-workflow.md?plain=1", "/lessons/first-workflow")]
    public void Links_to_lessons_open_the_lesson_page(string url, string expected)
    {
        Assert.Equal(expected, Links.Resolve(url, SourcePath));
    }

    [Theory]
    [InlineData("../../src/WorkshopCore/Calculator.cs", Repo + "/blob/main/src/WorkshopCore/Calculator.cs")]
    [InlineData("/docs/troubleshooting.md#runner-errors", Repo + "/blob/main/docs/troubleshooting.md#runner-errors")]
    [InlineData("../../README.md?plain=1", Repo + "/blob/main/README.md")]
    [InlineData("../../docs/My%20Notes.md", Repo + "/blob/main/docs/My%20Notes.md")]
    [InlineData("../../.github/workflows/", Repo + "/tree/main/.github/workflows")]
    [InlineData("../../", Repo + "/tree/main/")]
    [InlineData("/", Repo + "/tree/main/")]
    [InlineData("02-not-a-lesson.md", Repo + "/blob/main/lessons/foundation/02-not-a-lesson.md")]
    public void Other_repository_links_open_github(string url, string expected)
    {
        Assert.Equal(expected, Links.Resolve(url, SourcePath));
    }

    [Theory]
    [InlineData("#run-the-workflow")]
    [InlineData("https://docs.github.com/actions")]
    [InlineData("HTTPS://DOCS.GITHUB.COM/")]
    [InlineData("mailto:octo@example.com")]
    public void Absolute_links_and_fragments_are_unchanged(string url)
    {
        Assert.Equal(url, Links.Resolve(url, SourcePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("../../../outside.md")]
    [InlineData("/../outside.md")]
    [InlineData("//evil.example")]
    public void Unsafe_or_empty_links_point_nowhere(string? url)
    {
        Assert.Equal(LinkResolver.BlockedUrl, Links.Resolve(url, SourcePath));
    }

    [Theory]
    [InlineData("#top", LinkKind.Fragment)]
    [InlineData("https://github.com", LinkKind.SafeAbsolute)]
    [InlineData("HTTP://github.com", LinkKind.SafeAbsolute)]
    [InlineData("mailto:octo@example.com", LinkKind.SafeAbsolute)]
    [InlineData("docs/a.md", LinkKind.Relative)]
    [InlineData("../a.md", LinkKind.Relative)]
    [InlineData("docs/a:b.md", LinkKind.Relative)]
    [InlineData("javascript:alert(1)", LinkKind.Unsafe)]
    [InlineData(" javascript:alert(1)", LinkKind.Unsafe)]
    [InlineData("java\tscript:alert(1)", LinkKind.Unsafe)]
    [InlineData("java\nscript:alert(1)", LinkKind.Unsafe)]
    [InlineData("\u0000javascript:alert(1)", LinkKind.Unsafe)]
    [InlineData("data:text/html,x", LinkKind.Unsafe)]
    [InlineData("vbscript:msgbox(1)", LinkKind.Unsafe)]
    [InlineData("file:///etc/passwd", LinkKind.Unsafe)]
    [InlineData("ftp://example.com", LinkKind.Unsafe)]
    [InlineData("//evil.example", LinkKind.Unsafe)]
    [InlineData("/\\evil.example", LinkKind.Unsafe)]
    [InlineData("\\\\evil\\share", LinkKind.Unsafe)]
    [InlineData("", LinkKind.Unsafe)]
    public void Links_are_classified(string url, LinkKind expected)
    {
        Assert.Equal(expected, LinkResolver.Classify(url));
    }

    [Fact]
    public void Repository_paths_resolve_against_the_source_folder()
    {
        RepositoryLink? link = LinkResolver.ResolveRepositoryPath("../../docs/a.md#part", SourcePath);

        Assert.NotNull(link);
        Assert.Equal("docs/a.md", link.Path);
        Assert.Equal("#part", link.Fragment);
        Assert.False(link.IsDirectory);
    }

    [Fact]
    public void Repository_paths_cannot_leave_the_repository()
    {
        Assert.Null(LinkResolver.ResolveRepositoryPath("../../../a.md", SourcePath));
    }
}
