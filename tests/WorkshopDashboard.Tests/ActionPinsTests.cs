using WorkshopDashboard.Tests.Content;
using WorkshopDashboard.Tests.TestSupport;
using Xunit;

namespace WorkshopDashboard.Tests;

public class ActionPinsTests
{
    // Any two 40-character commit SHAs work here.
    private const string Sha = "3d3c42e5aac5ba805825da76410c181273ba90b1";
    private const string OtherSha = "a98b56852c35b8e3190ac28c8c2271da59106c68";

    [Theory]
    [InlineData($"      - uses: actions/checkout@{Sha} # v7.0.1")]
    [InlineData($"uses: actions/checkout@{Sha} # v7")]
    [InlineData($"  - uses: \"actions/checkout@{Sha}\" # v7.0")]
    [InlineData("      - uses: ./.github/actions/setup")]
    [InlineData("      - uses: docker://alpine@sha256:4bcff63911fcb4448bd4fdacec207030997caf25e9bea4045fa6c8c44de311d1")]
    public void Pinned_and_local_actions_pass(string line)
    {
        Assert.True(ActionPins.TryParse("build.yml", 1, line, out ActionReference? reference));
        Assert.Empty(ActionPins.CheckPins([reference]));
    }

    [Theory]
    [InlineData("uses: actions/checkout@v4", "is pinned to v4, which can be moved")]
    [InlineData("uses: actions/checkout@main", "is pinned to main")]
    [InlineData("uses: actions/checkout@3d3c42e", "is pinned to 3d3c42e")]
    [InlineData("uses: actions/checkout", "isn't pinned")]
    [InlineData("uses: octo/workflows/.github/workflows/build.yml@v1", "is pinned to v1")]
    [InlineData("uses: docker://alpine:3.20", "image digest")]
    public void Actions_that_can_change_are_reported(string line, string expected)
    {
        Assert.True(ActionPins.TryParse("build.yml", 7, line, out ActionReference? reference));

        Finding finding = Assert.Single(ActionPins.CheckPins([reference]));
        Assert.Equal(7, finding.Line);
        Assert.Contains(expected, finding.Message);
    }

    [Theory]
    [InlineData($"uses: actions/checkout@{Sha}")]
    [InlineData($"uses: actions/checkout@{Sha} # latest")]
    [InlineData($"uses: actions/checkout@{Sha} # 7.0.1")]
    public void Pins_name_their_release_in_a_comment(string line)
    {
        Assert.True(ActionPins.TryParse("build.yml", 1, line, out ActionReference? reference));

        Finding finding = Assert.Single(ActionPins.CheckPins([reference]));
        Assert.Contains("Add the release after the SHA as a comment", finding.Message);
    }

    [Theory]
    [InlineData("name: Build")]
    [InlineData("# uses: actions/checkout@v4")]
    [InlineData("        run: echo \"uses: actions/checkout@v4\"")]
    public void Other_lines_are_ignored(string line)
    {
        Assert.False(ActionPins.TryParse("build.yml", 1, line, out _));
    }

    [Fact]
    public void Yaml_blocks_in_markdown_are_checked()
    {
        const string markdown = """
            Add this step:

            ```yaml
            steps:
              - uses: actions/checkout@v4
            ```

            1. Then this one:

               ```yml
               - uses: actions/setup-dotnet@v5
               ```

            ```text
            uses: actions/checkout@v4
            ```

            Don't write `uses: actions/checkout@v4`.
            """;

        List<ActionReference> references = ActionPins.FindReferences(MarkdownFile.Parse("docs/page.md", markdown)).ToList();

        Assert.Equal([5, 11], references.Select(reference => reference.Line));
        Assert.Equal(["actions/checkout@v4", "actions/setup-dotnet@v5"], references.Select(reference => reference.Target));
    }

    [Fact]
    public void Front_matter_in_markdown_is_checked()
    {
        const string markdown = """
            ---
            on: workflow_dispatch
            steps:
              - uses: actions/checkout@v4
            ---

            # Triage new issues
            """;

        ActionReference reference = Assert.Single(ActionPins.FindReferences(MarkdownFile.Parse(".github/workflows/triage.md", markdown)));
        Assert.Equal(4, reference.Line);
    }

    [Fact]
    public void Workflow_files_are_checked_line_by_line()
    {
        using var repository = new TempRepository()
            .Write(".github/workflows/build.yml", $"name: Build\non: push\njobs:\n  build:\n    runs-on: ubuntu-latest\n    steps:\n      - uses: actions/checkout@{Sha} # v7.0.1\n      - uses: actions/setup-dotnet@v5\n")
            .Write("docs/notes.txt", "uses: actions/checkout@v4\n")
            .Write("solutions/tests/tests.yaml", "jobs:\n  test:\n    steps:\n      - uses: actions/checkout@main\n");

        List<Finding> findings = ActionPins.CheckPins(repository.Load());

        Assert.Equal([".github/workflows/build.yml:8", "solutions/tests/tests.yaml:4"], findings.Select(finding => $"{finding.Path}:{finding.Line}"));
    }

    [Fact]
    public void Each_action_is_pinned_to_one_release()
    {
        ActionReference[] references =
        [
            new("b.yml", 4, $"actions/checkout@{Sha}", "v7.0.1"),
            new("a.yml", 9, $"actions/checkout@{OtherSha}", "v6.0.0"),
            new("c.md", 2, $"actions/checkout@{Sha}", "v7.0.1"),
            new("a.yml", 3, $"actions/setup-dotnet@{OtherSha}", "v6.0.0"),
        ];

        Finding finding = Assert.Single(ActionPins.CheckConsistency(references));

        Assert.Equal("a.yml", finding.Path);
        Assert.Equal(9, finding.Line);
        Assert.Contains("actions/checkout is pinned to 2 different versions", finding.Message);
        Assert.Contains($"{Sha} # v7.0.1 (b.yml:4, c.md:2)", finding.Message);
        Assert.Contains($"{OtherSha} # v6.0.0 (a.yml:9)", finding.Message);
    }

    [Fact]
    public void Actions_from_one_repository_share_a_release()
    {
        var init = new ActionReference("a.yml", 5, $"github/codeql-action/init@{Sha}", "v4.31.0");
        var analyze = new ActionReference("a.yml", 9, $"github/codeql-action/analyze@{OtherSha}", "v4.30.0");

        Finding finding = Assert.Single(ActionPins.CheckConsistency([init, analyze]));
        Assert.Equal(5, finding.Line);
        Assert.Contains("github/codeql-action is pinned to 2 different versions", finding.Message);

        ActionReference sameRelease = analyze with { Target = $"github/codeql-action/analyze@{Sha}", Comment = "v4.31.0" };
        Assert.Empty(ActionPins.CheckConsistency([init, sameRelease]));
    }

    [Fact]
    public void A_commit_has_the_same_release_comment_everywhere()
    {
        ActionReference[] references =
        [
            new("a.yml", 1, $"actions/checkout@{Sha}", "v7.0.1"),
            new("b.yml", 1, $"actions/checkout@{Sha}", "v7"),
        ];

        Assert.Single(ActionPins.CheckConsistency(references));
    }

    [Fact]
    public void Long_lists_of_places_are_shortened()
    {
        List<ActionReference> references = Enumerable.Range(0, 7)
            .Select(i => new ActionReference($"lesson-{i}.md", 1, $"actions/checkout@{Sha}", "v7.0.1"))
            .Append(new ActionReference("build.yml", 1, $"actions/checkout@{OtherSha}", "v6.0.0"))
            .ToList();

        Finding finding = Assert.Single(ActionPins.CheckConsistency(references));
        Assert.Contains("lesson-4.md:1, and 2 more)", finding.Message);
        Assert.DoesNotContain("lesson-5.md", finding.Message);
    }
}
