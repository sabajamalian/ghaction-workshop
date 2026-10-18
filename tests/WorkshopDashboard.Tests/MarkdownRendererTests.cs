using WorkshopDashboard.Services;
using WorkshopDashboard.Tests.TestSupport;
using Xunit;

namespace WorkshopDashboard.Tests;

public class MarkdownRendererTests
{
    private const string SourcePath = "lessons/foundation/00-welcome.md";

    private static readonly LinkResolver Links = new(
        DashboardFactory.RepositoryUrl,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lessons/foundation/00-welcome.md"] = "welcome",
            ["lessons/foundation/01-first-workflow.md"] = "first-workflow",
        });

    private static string Render(string markdown) => new MarkdownRenderer().RenderLesson(markdown, SourcePath, Links);

    public static TheoryData<string> HostileMarkdown => new()
    {
        "<script>alert(1)</script>",
        "<script>\nalert(1)\n</script>",
        "<img src=x onerror=alert(1)>",
        "<iframe src=\"https://evil.example\"></iframe>",
        "<style>body { display: none }</style>",
        "<div onclick=\"alert(1)\">Click me</div>",
        "<a href=\"javascript:alert(1)\">Click me</a>",
        "Text <span onclick=\"alert(1)\">here</span> and more.",
        "Text <img src=x onerror=alert(1)> inline.",
        "<details onclick=\"alert(1)\">\n<summary>Hint</summary>\n\nBody\n\n</details>",
        "<details>\n<summary onclick=\"alert(1)\">Hint</summary>\n\nBody\n\n</details>",
        "<details>\n<summary><img src=x onerror=alert(1)></summary>\n\nBody\n\n</details>",
        "<details>\n<summary>&lt;script&gt;alert(1)&lt;/script&gt;</summary>\n\nBody\n\n</details>",
        "<details>\n<summary>Hint</summary>\n<script>alert(1)</script>\n</details>",
        "[link](javascript:alert(1))",
        "[link](JaVaScRiPt:alert(1))",
        "[link](javascript&#58;alert(1))",
        "[link](&#x6A;avascript:alert(1))",
        "[link](<java\tscript:alert(1)>)",
        "[link](java&#9;script:alert(1))",
        "[link](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)",
        "[link](vbscript:msgbox(1))",
        "[link](//evil.example/path)",
        "[link](/\\evil.example/path)",
        "[link][ref]\n\n[ref]: javascript:alert(1)",
        "<javascript:alert(1)>",
        "<data:text/html;base64,PHNjcmlwdD4=>",
        "[link](#a\"onmouseover=\"alert(1))",
        "[link](https://example.com \"a\\\" onmouseover=\\\"alert(1)\")",
        "![image](javascript:alert(1))",
        "```\"><script>alert(1)</script>\nbody\n```",
        "```html\n<script>alert(1)</script>\n```",
        "| A | B |\n| :--- | ---: |\n| <script>x</script> | 2 |",
        "> [!NOTE]\n> <script>alert(1)</script>",
        "<!-- <script>alert(1)</script> -->",
        "Text <!-- comment --> more.",
        "<?php echo 1; ?>",
        "<![CDATA[<script>alert(1)</script>]]>",
        "<!DOCTYPE html>",
    };

    [Theory]
    [MemberData(nameof(HostileMarkdown))]
    public void Hostile_markdown_renders_only_allowed_markup(string markdown)
    {
        SafeHtml.AssertSafe(Render(markdown));
    }

    [Fact]
    public void Script_blocks_are_shown_as_text()
    {
        string html = Render("<script>alert(1)</script>");

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Inline_html_is_shown_as_text()
    {
        string html = Render("Text <span onclick=\"alert(1)\">here</span> and more.");

        Assert.Contains("&lt;span onclick=&quot;alert(1)&quot;&gt;here&lt;/span&gt;", html);
        Assert.DoesNotContain("<span", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("javascript&#58;alert(1)")]
    [InlineData("&#x6A;avascript:alert(1)")]
    [InlineData("<java\tscript:alert(1)>")]
    [InlineData("java&#9;script:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("//evil.example/path")]
    [InlineData("/\\evil.example/path")]
    [InlineData("../../../outside.md")]
    public void Unsafe_links_point_nowhere(string url)
    {
        string html = Render($"[link]({url})");

        Assert.Contains("<a href=\"#\">link</a>", html);
    }

    [Fact]
    public void Unsafe_reference_links_point_nowhere()
    {
        string html = Render("[link][ref]\n\n[ref]: javascript:alert(1)");

        Assert.Contains("<a href=\"#\">link</a>", html);
    }

    [Theory]
    [InlineData("https://docs.github.com/actions")]
    [InlineData("http://example.com/")]
    [InlineData("mailto:octo@example.com")]
    [InlineData("#run-the-workflow")]
    public void Safe_links_keep_their_target(string url)
    {
        string html = Render($"[link]({url})");

        Assert.Contains($"<a href=\"{url}\">link</a>", html);
    }

    [Fact]
    public void Lesson_links_point_to_dashboard_pages()
    {
        string html = Render("[the next lab](01-first-workflow.md#run-it)");

        Assert.Contains("<a href=\"/lessons/first-workflow#run-it\">the next lab</a>", html);
    }

    [Fact]
    public void Repository_links_point_to_github()
    {
        string html = Render("[the calculator](../../src/WorkshopCore/Calculator.cs)");

        Assert.Contains($"<a href=\"{DashboardFactory.RepositoryUrl}/blob/main/src/WorkshopCore/Calculator.cs\">the calculator</a>", html);
    }

    [Fact]
    public void A_fragment_cannot_add_attributes()
    {
        string html = Render("[link](#a\"onmouseover=\"alert(1))");

        Assert.DoesNotContain("onmouseover=\"", html);
        SafeHtml.AssertSafe(html);
    }

    [Fact]
    public void Unsafe_image_sources_point_nowhere()
    {
        string html = Render("![diagram](javascript:alert(1))");

        Assert.Contains("src=\"#\"", html);
    }

    [Fact]
    public void Unsafe_autolinks_become_plain_text()
    {
        string html = Render("<javascript:alert(1)>");

        Assert.DoesNotContain("<a", html);
        Assert.Contains("javascript:alert(1)", html);
    }

    [Fact]
    public void Safe_autolinks_stay_links()
    {
        string html = Render("Visit <https://github.com> or email <octo@example.com>.");

        Assert.Contains("<a href=\"https://github.com\">https://github.com</a>", html);
        Assert.Contains("<a href=\"mailto:octo@example.com\">octo@example.com</a>", html);
    }

    [Fact]
    public void Html_comments_are_removed()
    {
        string html = Render("Before\n\n<!-- maintainer note -->\n\nAfter");

        Assert.DoesNotContain("maintainer note", html);
        Assert.Contains("<p>Before</p>", html);
        Assert.Contains("<p>After</p>", html);
    }

    [Fact]
    public void Hint_blocks_keep_details_and_summary()
    {
        string html = Render("<details>\n<summary>Hint</summary>\n\nRun `dotnet test`.\n\n</details>");

        int details = html.IndexOf("<details>", StringComparison.Ordinal);
        int summary = html.IndexOf("<summary>Hint</summary>", StringComparison.Ordinal);
        int body = html.IndexOf("<p>Run <code>dotnet test</code>.</p>", StringComparison.Ordinal);
        int end = html.IndexOf("</details>", StringComparison.Ordinal);

        Assert.True(details >= 0 && details < summary && summary < body && body < end, html);
    }

    [Fact]
    public void Open_details_blocks_are_allowed()
    {
        string html = Render("<details open>\n<summary>Hint</summary>\n\nText\n\n</details>");

        Assert.Contains("<details open>", html);
    }

    [Fact]
    public void Details_with_attributes_are_shown_as_text()
    {
        string html = Render("<details onclick=\"alert(1)\">\n<summary>Hint</summary>\n\nText\n\n</details>");

        Assert.Contains("&lt;details onclick=&quot;alert(1)&quot;&gt;", html);
        Assert.DoesNotContain("<details", html);
    }

    [Fact]
    public void Summary_text_is_encoded()
    {
        string html = Render("<details>\n<summary>Why doesn't &lt;script&gt; run?</summary>\n\nText\n\n</details>");

        Assert.Contains("<summary>Why doesn&#39;t &lt;script&gt; run?</summary>", html);
    }

    [Fact]
    public void Summary_with_markup_is_shown_as_text()
    {
        string html = Render("<details>\n<summary><b>Hint</b></summary>\n\nText\n\n</details>");

        Assert.Contains("&lt;summary&gt;&lt;b&gt;Hint&lt;/b&gt;&lt;/summary&gt;", html);
        Assert.DoesNotContain("<b>", html);
    }

    [Theory]
    [InlineData("<details>", "<details>")]
    [InlineData("  <details open>  ", "<details open>")]
    [InlineData("</details>", "</details>")]
    [InlineData("<summary>Hint</summary>", "<summary>Hint</summary>")]
    [InlineData("<summary>A &amp; B</summary>", "<summary>A &amp; B</summary>")]
    public void Allowed_html_lines_are_kept(string line, string expected)
    {
        Assert.True(AllowedHtml.TryGetSafeLine(line, out string? safe));
        Assert.Equal(expected, safe);
    }

    [Theory]
    [InlineData("<details class=\"hint\">")]
    [InlineData("<DETAILS>")]
    [InlineData("<summary>Hint")]
    [InlineData("<summary><b>Hint</b></summary>")]
    [InlineData("<summary></summary>")]
    [InlineData("<script>")]
    [InlineData("<div>")]
    public void Other_html_lines_are_rejected(string line)
    {
        Assert.False(AllowedHtml.TryGetSafeLine(line, out _));
    }

    [Fact]
    public void Code_blocks_keep_their_formatting_and_language()
    {
        string html = Render("```yaml\non:\n  workflow_dispatch:\n\njobs: {}\n```");

        Assert.Contains("<span class=\"code-block-language\">YAML</span>", html);
        Assert.Contains("<code class=\"language-yaml\">on:\n  workflow_dispatch:\n\njobs: {}\n</code>", html);
    }

    [Fact]
    public void Code_without_a_language_is_labeled_code()
    {
        string html = Render("```\nplain\n```");

        Assert.Contains("<span class=\"code-block-language\">Code</span>", html);
        Assert.Contains("<pre><code>plain\n</code></pre>", html);
    }

    [Fact]
    public void Indented_code_is_labeled_code()
    {
        string html = Render("Run this:\n\n    dotnet test\n");

        Assert.Contains("<span class=\"code-block-language\">Code</span>", html);
        Assert.Contains("<pre><code>dotnet test\n</code></pre>", html);
    }

    [Fact]
    public void Code_block_content_is_escaped()
    {
        string html = Render("```html\n<script>alert(1)</script>\n```");

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Code_block_info_strings_are_escaped()
    {
        string html = Render("```\"><script>alert(1)</script>\nbody\n```");

        Assert.DoesNotContain("<script", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData(null, "Code")]
    [InlineData("", "Code")]
    [InlineData("yaml", "YAML")]
    [InlineData("YML", "YAML")]
    [InlineData("bash", "Bash")]
    [InlineData("pwsh", "PowerShell")]
    [InlineData("csharp", "C#")]
    [InlineData("console", "Output")]
    [InlineData("hcl", "hcl")]
    public void Code_language_names_are_readable(string? language, string expected)
    {
        Assert.Equal(expected, CodeLanguages.DisplayName(language));
    }

    [Theory]
    [InlineData("NOTE", "note", "Note")]
    [InlineData("TIP", "tip", "Tip")]
    [InlineData("IMPORTANT", "important", "Important")]
    [InlineData("WARNING", "warning", "Warning")]
    [InlineData("CAUTION", "caution", "Caution")]
    public void Alerts_have_a_title(string kind, string cssName, string title)
    {
        string html = Render($"> [!{kind}]\n> Read this first.");

        Assert.Contains($"markdown-alert-{cssName}", html);
        Assert.Contains($"<p class=\"markdown-alert-title\">{title}</p>", html);
        Assert.Contains("<p>Read this first.</p>", html);
    }

    [Fact]
    public void The_leading_heading_is_removed()
    {
        string html = Render("# Lab 0: Welcome\n\nIntro text.");

        Assert.DoesNotContain("<h1", html);
        Assert.Contains("<p>Intro text.</p>", html);
    }

    [Fact]
    public void Headings_get_github_style_ids()
    {
        string html = Render("## Run the workflow\n\n### Check the `build` job");

        Assert.Contains("<h2 id=\"run-the-workflow\">Run the workflow</h2>", html);
        Assert.Contains("id=\"check-the-build-job\"", html);
    }

    [Fact]
    public void Tables_render_without_inline_styles()
    {
        string html = Render("| Step | Result |\n| :--- | ---: |\n| Build | Passed |");

        Assert.Contains("<table>", html);
        Assert.Contains("<th>Step</th>", html);
        Assert.Contains("<td>Passed</td>", html);
        Assert.DoesNotContain("style=", html);
    }
}
