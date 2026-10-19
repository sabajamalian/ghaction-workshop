using WorkshopDashboard.Tests.Content;
using Xunit;

namespace WorkshopDashboard.Tests;

// Runs the editorial rules over every file a contributor would commit.
// A failure lists each problem as path:line: message. See "What the tests check" in maintainers/editorial-guide.md.
public class ContentTests
{
    private static readonly Lazy<ContentRepository> Repository = new(() => ContentRepository.Load(ContentRepository.FindRoot()));

    [Fact]
    public void Text_has_no_long_dashes_or_emojis()
    {
        AssertNoFindings(ContentRules.CheckPunctuation(Repository.Value));
    }

    [Fact]
    public void Code_blocks_have_a_language_and_match_their_files()
    {
        AssertNoFindings(ContentRules.CheckCodeBlocks(Repository.Value));
    }

    [Fact]
    public void Links_resolve_and_say_where_they_go()
    {
        AssertNoFindings(ContentRules.CheckLinks(Repository.Value));
    }

    [Fact]
    public void Html_is_limited_to_hints()
    {
        AssertNoFindings(ContentRules.CheckHtml(Repository.Value));
    }

    [Fact]
    public void Alerts_use_a_type_github_supports()
    {
        AssertNoFindings(ContentRules.CheckAlerts(Repository.Value));
    }

    [Fact]
    public void Bold_text_is_not_a_whole_sentence()
    {
        AssertNoFindings(ContentRules.CheckBold(Repository.Value));
    }

    [Fact]
    public void Lessons_load_and_their_titles_match()
    {
        AssertNoFindings(ContentRules.CheckLessons(Repository.Value));
    }

    [Fact]
    public void Copies_match_their_originals()
    {
        AssertNoFindings(ContentRules.CheckCopies(Repository.Value));
    }

    [Fact]
    public void Actions_are_pinned_to_a_full_commit_sha()
    {
        AssertNoFindings(ActionPins.CheckPins(Repository.Value));
    }

    [Fact]
    public void Each_action_is_pinned_to_one_release_everywhere()
    {
        AssertNoFindings(ActionPins.CheckConsistency(Repository.Value));
    }

    [Fact]
    public void The_checks_see_the_files_that_matter()
    {
        ContentRepository repository = Repository.Value;

        Assert.Contains("maintainers/AGENTS.md", repository.Files);
        Assert.Contains("maintainers/editorial-guide.md", repository.Files);
        Assert.Contains(".github/workflows/build.yml", repository.Files);
        Assert.DoesNotContain(repository.FileList, path => path.Contains("/bin/", StringComparison.Ordinal) || path.Contains("/obj/", StringComparison.Ordinal));
        Assert.DoesNotContain(repository.FileList, path => path.StartsWith(".git/", StringComparison.Ordinal));
    }

    private static void AssertNoFindings(IEnumerable<Finding> findings)
    {
        List<Finding> sorted = findings
            .OrderBy(finding => finding.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.Line)
            .ToList();

        if (sorted.Count > 0)
        {
            string noun = sorted.Count == 1 ? "problem" : "problems";
            Assert.Fail($"Found {sorted.Count} {noun}:{Environment.NewLine}{string.Join(Environment.NewLine, sorted)}");
        }
    }
}
