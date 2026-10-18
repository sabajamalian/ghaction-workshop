using WorkshopDashboard.Services;
using Xunit;

namespace WorkshopDashboard.Tests;

public class FrontMatterTests
{
    [Fact]
    public void Valid_front_matter_is_read()
    {
        const string text = "---\ntitle: \"Lab 1: Run your first workflow\"\nsummary: Run a workflow by hand.\nduration: 20\n---\n# Lab 1\n\nBody";

        Assert.True(FrontMatter.TryParse(text, out FrontMatterDocument? document, out string? error), error);

        Assert.Equal("Lab 1: Run your first workflow", document!.Values["title"]);
        Assert.Equal("Run a workflow by hand.", document.Values["summary"]);
        Assert.Equal("20", document.Values["duration"]);
        Assert.Equal("# Lab 1\n\nBody", document.Body);
    }

    [Fact]
    public void Byte_order_mark_and_windows_line_endings_are_accepted()
    {
        const string text = "\uFEFF---\r\ntitle: Welcome\r\n---\r\nBody\r\n";

        Assert.True(FrontMatter.TryParse(text, out FrontMatterDocument? document, out string? error), error);

        Assert.Equal("Welcome", document!.Values["title"]);
        Assert.Equal("Body\n", document.Body);
    }

    [Fact]
    public void Blank_lines_comments_and_empty_values_are_allowed()
    {
        const string text = "---\n# A comment\n\ntitle: Welcome\nrequires:\n---\n";

        Assert.True(FrontMatter.TryParse(text, out FrontMatterDocument? document, out string? error), error);

        Assert.Equal("Welcome", document!.Values["title"]);
        Assert.Equal(string.Empty, document.Values["requires"]);
        Assert.Equal(2, document.Values.Count);
    }

    [Theory]
    [InlineData("\"Say \\\"hi\\\" \\\\ there\"", "Say \"hi\" \\ there")]
    [InlineData("'It''s fine'", "It's fine")]
    [InlineData("\"Lab 3: Triggers # and more\"", "Lab 3: Triggers # and more")]
    [InlineData("  plain text  ", "plain text")]
    public void Quoted_values_are_unescaped(string raw, string expected)
    {
        Assert.True(FrontMatter.TryParse($"---\ntitle: {raw}\n---\n", out FrontMatterDocument? document, out string? error), error);

        Assert.Equal(expected, document!.Values["title"]);
    }

    [Theory]
    [InlineData("title: x\n", "doesn't start with a front matter block")]
    [InlineData("---\ntitle: x\n", "isn't closed")]
    [InlineData("---\ntitle: a\ntitle: b\n---\n", "appears more than once")]
    [InlineData("---\ntitle: Lab 1: Run\n---\n", "Wrap the value in double quotes")]
    [InlineData("---\nsummary: Run it #now\n---\n", "Wrap the value in double quotes")]
    [InlineData("---\nsummary: *bold*\n---\n", "Wrap the value in double quotes")]
    [InlineData("---\nsummary: - item\n---\n", "Wrap the value in double quotes")]
    [InlineData("---\nnot a pair\n---\n", "Line 2 isn't a \"key: value\" pair")]
    [InlineData("---\nTitle: x\n---\n", "Line 2 isn't a \"key: value\" pair")]
    [InlineData("---\ntitle: \"x\" y\n---\n", "Remove the text after the closing quote")]
    [InlineData("---\ntitle: \"x\n---\n", "The closing double quote is missing")]
    [InlineData("---\ntitle: 'x\n---\n", "The closing single quote is missing")]
    [InlineData("---\ntitle: \"\\x\"\n---\n", "isn't supported")]
    public void Invalid_front_matter_explains_the_problem(string text, string expectedMessage)
    {
        Assert.False(FrontMatter.TryParse(text, out FrontMatterDocument? document, out string? error));

        Assert.Null(document);
        Assert.Contains(expectedMessage, error);
    }
}
