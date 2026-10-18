using WorkshopDashboard.Services;
using WorkshopDashboard.Tests.Content;
using WorkshopDashboard.Tests.TestSupport;
using Xunit;

namespace WorkshopDashboard.Tests;

public class ContentRulesTests
{
    private const string Page = "docs/page.md";
    private const string Lesson = "lessons/foundation/01-first.md";
    private const string Workflow = ".github/workflows/build.yml";

    // Written as escapes, because the punctuation rule reads this file too.
    private const string EmDash = "\u2014";
    private const string EnDash = "\u2013";
    private const string Rocket = "\U0001F680";
    private const string EmDashEntity = "\u0026mdash;";

    private const string Steps = "jobs:\n  build:\n    steps:\n      - run: dotnet build\n      - run: dotnet test\n";
    private const string LessonSections = "\n\n## Check your result\n\nThe run has a green check mark.\n\n## Next step\n\nGo back to the lesson list.";

    [Fact]
    public void Findings_read_as_path_line_and_message()
    {
        Assert.Equal("docs/page.md:3: Fix it.", new Finding(Page, 3, "Fix it.").ToString());
        Assert.Equal("docs/page.md: Fix it.", new Finding(Page, 0, "Fix it.").ToString());
    }

    [Fact]
    public void Ignored_files_and_dot_folders_are_skipped()
    {
        using var repository = new TempRepository()
            .Write(".gitignore", "# Build output\nbin/\nobj/\n*.trx\nscratch.txt\n")
            .Write(".github/workflows/build.yml", "name: Build\n")
            .Write(".git/HEAD", "ref: refs/heads/main\n")
            .Write(".vscode/settings.json", "{}\n")
            .Write("README.md", "# Workshop\n")
            .Write("results/run.trx", "<TestRun />\n")
            .Write("scratch.txt", "notes\n")
            .Write("src/App/bin/Debug/App.dll", "binary")
            .Write("src/App/obj/project.assets.json", "{}\n")
            .Write("src/App/Program.cs", "return;\n");

        Assert.Equal([".github/workflows/build.yml", ".gitignore", "README.md", "src/App/Program.cs"], repository.Load().FileList);
    }

    [Fact]
    public void Gitignore_patterns_the_tests_can_not_read_stop_the_run()
    {
        using var repository = new TempRepository().Write(".gitignore", "**/node_modules\n");

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => repository.Load());
        Assert.Contains("**/node_modules", error.Message);
    }

    [Fact]
    public void A_folder_exists_only_when_it_contains_a_file()
    {
        using var repository = new TempRepository().Write("docs/guide.md", "# Guide\n");
        Directory.CreateDirectory(Path.Combine(repository.Root, "empty"));

        ContentRepository content = repository.Load();

        Assert.True(content.Exists(string.Empty));
        Assert.True(content.Exists("docs"));
        Assert.True(content.Exists("docs/guide.md"));
        Assert.False(content.Exists("docs/Guide.md"));
        Assert.False(content.Exists("empty"));
    }

    [Fact]
    public void Markdown_files_keep_the_line_numbers_of_the_file()
    {
        MarkdownFile file = MarkdownFile.Parse(Page, "\uFEFF---\r\ntitle: Page\r\n---\r\n# Page\r\n\r\nRun `dotnet test` now.\r\n");

        Assert.Equal(3, file.FrontMatterLineCount);
        Assert.Equal(["---", "title: Page", "---", "# Page", "", "Run `dotnet test` now.", ""], file.Lines);
        Assert.Equal(4, file.Document[0].Line + 1);
        Assert.Equal(6, file.LineAt(file.Text.IndexOf("now", StringComparison.Ordinal)));
        Assert.True(file.IsCode(file.Text.IndexOf("dotnet", StringComparison.Ordinal)));
        Assert.False(file.IsCode(file.Text.IndexOf("now", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_checks_parse_markdown_like_the_dashboard()
    {
        Assert.Equal(
            MarkdownRenderer.CreatePipeline().Extensions.Select(extension => extension.GetType().FullName),
            MarkdownFile.Pipeline.Extensions.Select(extension => extension.GetType().FullName));
    }

    [Fact]
    public void Dashes_in_prose_are_reported_on_their_line()
    {
        MarkdownFile file = MarkdownFile.Parse(Page, $"# Page\n\nFirst line.\nSecond {EmDash} line.\n");

        Finding finding = Assert.Single(ContentRules.CheckPunctuation(file));
        Assert.Equal(4, finding.Line);
        Assert.StartsWith("Found an em dash (U+2014). Use a comma, colon, period, or parentheses instead.", finding.Message);
    }

    [Fact]
    public void Code_can_contain_dashes()
    {
        MarkdownFile file = MarkdownFile.Parse(Page, $"Run `a {EmDash} b` now.\n\n```text\nx {EnDash} y\n```\n\n    z {EmDash} w\n");

        Assert.Empty(ContentRules.CheckPunctuation(file));
    }

    [Fact]
    public void A_line_with_code_and_prose_reports_only_the_prose()
    {
        MarkdownFile file = MarkdownFile.Parse(Page, $"Use `a{EnDash}b` and c {EmDash} d.\n");

        Finding finding = Assert.Single(ContentRules.CheckPunctuation(file));
        Assert.Contains("em dash", finding.Message);
        Assert.DoesNotContain("en dash", finding.Message);
    }

    [Fact]
    public void Dash_entities_are_reported_outside_code()
    {
        MarkdownFile file = MarkdownFile.Parse(Page, $"A {EmDashEntity} B\n\nWrite `{EmDashEntity}` to get one.\n");

        Finding finding = Assert.Single(ContentRules.CheckPunctuation(file));
        Assert.Equal(1, finding.Line);
        Assert.Contains($"{EmDashEntity}, which renders as a dash", finding.Message);
    }

    [Fact]
    public void Each_line_lists_every_kind_of_problem_once()
    {
        MarkdownFile file = MarkdownFile.Parse(Page, $"a {EmDash} b {EnDash} c {EmDash} d {Rocket}\n");

        Finding finding = Assert.Single(ContentRules.CheckPunctuation(file));
        Assert.StartsWith("Found an em dash (U+2014), an en dash (U+2013), and an emoji (U+1F680).", finding.Message);
        Assert.EndsWith("Remove the emoji.", finding.Message);
    }

    [Fact]
    public void Front_matter_is_checked()
    {
        MarkdownFile file = MarkdownFile.Parse(Page, $"---\ntitle: \"A {EmDash} B\"\n---\n# Page\n");

        Assert.Equal(2, Assert.Single(ContentRules.CheckPunctuation(file)).Line);
    }

    [Fact]
    public void Text_files_are_checked_and_copied_files_are_not()
    {
        using var repository = new TempRepository()
            .Write(Workflow, $"name: Build\n# Runs on every push {EnDash} fast.\n")
            .Write("LICENSE.txt", $"Copyright {EmDash} someone\n")
            .Write("logo.svg", $"<svg>{EmDash}</svg>\n")
            .Write("src/App/Program.cs", $"// Build {EmDash} then test.\n")
            .Write("src/App/wwwroot/lib/library.js", $"// {EmDash}\n");

        List<Finding> findings = ContentRules.CheckPunctuation(repository.Load());

        Assert.Equal([".github/workflows/build.yml:2", "src/App/Program.cs:1"], findings.Select(finding => $"{finding.Path}:{finding.Line}"));
    }

    [Fact]
    public void Indented_code_is_reported()
    {
        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, "Intro.\n\n    dotnet build\n"));

        Assert.Equal(3, finding.Line);
        Assert.Contains("indented four or more spaces", finding.Message);
    }

    [Fact]
    public void Unclosed_fences_are_reported()
    {
        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, "Intro.\n\n```yaml\nname: Build\n"));

        Assert.Equal(3, finding.Line);
        Assert.Contains("isn't closed", finding.Message);
    }

    [Fact]
    public void Code_blocks_need_a_language()
    {
        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, "```\ndotnet build\n```\n"));

        Assert.Contains("Add a language", finding.Message);
    }

    [Fact]
    public void Lessons_use_a_language_the_dashboard_knows()
    {
        const string markdown = "```rust\nfn main() {}\n```\n";

        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Lesson, markdown));
        Assert.Contains("doesn't recognize the language \"rust\"", finding.Message);
        Assert.Empty(Check(ContentRules.CheckCodeBlocks, Page, markdown));
    }

    [Fact]
    public void Only_source_or_excerpt_can_follow_the_language()
    {
        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, "```yaml title=build.yml\nname: Build\n```\n"));

        Assert.Contains("Remove \"title=build.yml\"", finding.Message);
    }

    [Fact]
    public void Source_blocks_that_match_their_file_pass()
    {
        const string markdown = "```yaml source=.github/workflows/build.yml\nname: Build\non: push\n```\n";

        Assert.Empty(Check(ContentRules.CheckCodeBlocks, Page, markdown, (Workflow, "name: Build\non: push\n")));
    }

    [Fact]
    public void Source_blocks_report_the_first_line_that_differs()
    {
        const string markdown = "Intro.\n\n```yaml source=.github/workflows/build.yml\nname: Build\non: pull_request\n```\n";

        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, markdown, (Workflow, "name: Build\non: push\n")));
        Assert.Equal(5, finding.Line);
        Assert.Contains("line 2 of .github/workflows/build.yml, which is \"on: push\"", finding.Message);
    }

    [Fact]
    public void Source_blocks_show_the_whole_file()
    {
        const string markdown = "```yaml source=.github/workflows/build.yml\nname: Build\n```\n";

        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, markdown, (Workflow, "name: Build\non: push\n")));
        Assert.Equal(1, finding.Line);
        Assert.Contains("stops after 1 line, but .github/workflows/build.yml has 2 lines", finding.Message);
    }

    [Fact]
    public void Source_blocks_can_not_add_lines()
    {
        const string markdown = "```yaml source=.github/workflows/build.yml\nname: Build\non: push\njobs: {}\n```\n";

        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, markdown, (Workflow, "name: Build\non: push\n")));
        Assert.Equal(4, finding.Line);
        Assert.Contains("has 3 lines, but .github/workflows/build.yml only has 2 lines", finding.Message);
    }

    [Theory]
    [InlineData("source=missing.yml", "isn't in the repository")]
    [InlineData("source=.github/workflows/Build.yml", "isn't in the repository")]
    [InlineData("source=../build.yml", "needs a path from the repository root")]
    [InlineData("source=/.github/workflows/build.yml", "needs a path from the repository root")]
    [InlineData("excerpt=", "needs a path from the repository root")]
    public void Snippet_paths_are_checked(string argument, string expected)
    {
        string markdown = $"```yaml {argument}\nname: Build\n```\n";

        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, markdown, (Workflow, "name: Build\n")));
        Assert.Contains(expected, finding.Message);
    }

    [Fact]
    public void Excerpts_match_lines_at_any_indentation()
    {
        const string markdown = "```yaml excerpt=.github/workflows/build.yml\nsteps:\n  - run: dotnet build\n```\n";

        Assert.Empty(Check(ContentRules.CheckCodeBlocks, Page, markdown, (Workflow, Steps)));
    }

    [Theory]
    [InlineData("steps:\n  - run: dotnet publish\n", "The line \"- run: dotnet publish\" isn't in the file.")]
    [InlineData("- run: dotnet test\n- run: dotnet build\n", "Every line is in the file, but not in this order")]
    [InlineData("", "This excerpt= block is empty.")]
    public void Excerpts_that_drift_from_their_file_are_reported(string excerpt, string expected)
    {
        string markdown = $"```yaml excerpt=.github/workflows/build.yml\n{excerpt}```\n";

        Finding finding = Assert.Single(Check(ContentRules.CheckCodeBlocks, Page, markdown, (Workflow, Steps)));
        Assert.Equal(1, finding.Line);
        Assert.Contains(expected, finding.Message);
    }

    [Fact]
    public void Images_are_reported()
    {
        Finding finding = Assert.Single(Check(ContentRules.CheckLinks, Page, "![The Actions tab](actions-tab.png)\n"));

        Assert.Contains("Remove the image", finding.Message);
    }

    [Theory]
    [InlineData("[here](https://example.com)", "doesn't say where the link goes")]
    [InlineData("[Click here.](https://example.com)", "\"click here\"")]
    [InlineData("[Read **more**](https://example.com)", "\"read more\"")]
    [InlineData("[](https://example.com)", "This link has no text")]
    public void Link_text_says_where_the_link_goes(string markdown, string expected)
    {
        Finding finding = Assert.Single(Check(ContentRules.CheckLinks, Page, markdown));

        Assert.Contains(expected, finding.Message);
    }

    [Fact]
    public void Descriptive_links_pass()
    {
        const string markdown = "Read [the workflow syntax reference](https://docs.github.com/actions) or write to <octo@example.com>.\n";

        Assert.Empty(Check(ContentRules.CheckLinks, Page, markdown));
    }

    [Fact]
    public void Relative_links_point_to_files_in_the_repository()
    {
        const string markdown = """
            See [the guide](guide.md).

            See [the docs folder](../docs/).

            See [the missing page](missing.md).

            See [the guide in capitals](Guide.md).

            See [the outside file](../../outside.md).
            """;

        List<Finding> findings = Check(ContentRules.CheckLinks, Page, markdown, ("docs/guide.md", "# Guide\n"));

        Assert.Equal([5, 7, 9], findings.Select(finding => finding.Line));
        Assert.Contains("points to docs/missing.md, which isn't in the repository", findings[0].Message);
        Assert.Contains("points to docs/Guide.md", findings[1].Message);
        Assert.Contains("points outside the repository", findings[2].Message);
    }

    [Fact]
    public void Anchor_links_match_a_heading()
    {
        const string markdown = """
            # Page

            ## Check your result

            See [the check](#check-your-result).
            See [the old section](#old-section).
            See [the placeholder](#).
            """;

        List<Finding> findings = Check(ContentRules.CheckLinks, Page, markdown);

        Assert.Equal([6, 7], findings.Select(finding => finding.Line));
        Assert.Contains("No heading in this file has the anchor #old-section", findings[0].Message);
        Assert.Contains("placeholder", findings[1].Message);
    }

    [Fact]
    public void Anchor_links_to_other_files_match_a_heading()
    {
        const string markdown = "See [the code block rules](guide.md#code-blocks).\nSee [the image rules](guide.md#images).\n";

        Finding finding = Assert.Single(Check(ContentRules.CheckLinks, Page, markdown, ("docs/guide.md", "# Guide\n\n## Code blocks\n")));
        Assert.Equal(2, finding.Line);
        Assert.Contains("docs/guide.md has no heading with the anchor #images", finding.Message);
    }

    [Fact]
    public void A_lessons_title_is_not_an_anchor()
    {
        string lesson = TempLessons.LessonText("Lab 1: First", 1, body: "## Steps\n\nSee [the steps](#steps) and [the title](#lab-1-first).");

        Finding finding = Assert.Single(Check(ContentRules.CheckLinks, Lesson, lesson));
        Assert.Equal(13, finding.Line);
        Assert.Contains("#lab-1-first", finding.Message);
    }

    [Fact]
    public void Links_the_dashboard_blocks_are_reported()
    {
        const string markdown = """
            Run [the script](javascript:alert(1)).

            Open [the mirror](//example.com/page).

            Download <ftp://example.com/file>.
            """;

        List<Finding> findings = Check(ContentRules.CheckLinks, Page, markdown);

        Assert.Equal([1, 3, 5], findings.Select(finding => finding.Line));
        Assert.Contains("blocks the link javascript:alert(1)", findings[0].Message);
        Assert.Contains("blocks the link //example.com/page", findings[1].Message);
        Assert.Contains("doesn't link ftp://example.com/file", findings[2].Message);
    }

    [Fact]
    public void Findings_point_at_the_line_with_the_link()
    {
        const string markdown = """
            # Page

            Some text that wraps
            onto [here](https://example.com).

            - First item
            - Then [here](https://example.com).

            | Name | Link |
            | --- | --- |
            | A | [here](https://example.com) |

            > A quote.
            > With [here](https://example.com).
            """;

        List<Finding> findings = Check(ContentRules.CheckLinks, Page, markdown);

        Assert.Equal([4, 7, 11, 14], findings.Select(finding => finding.Line));
    }

    [Fact]
    public void Hints_and_comments_are_allowed()
    {
        const string markdown = """
            Intro.

            <details>
            <summary>Hint</summary>

            Try this.

            </details>

            1. Run the workflow.

               <details>
               <summary>Hint</summary>

               Check the logs.

               </details>

            <!-- Maintainers: keep this in sync with build.yml. -->
            """;

        Assert.Empty(ContentRules.CheckHtml(MarkdownFile.Parse(Page, markdown)));
    }

    [Fact]
    public void Other_html_is_reported_line_by_line()
    {
        List<Finding> findings = ContentRules.CheckHtml(MarkdownFile.Parse(Page, "<div>\nHello\n</div>\n")).ToList();

        Assert.Equal([1, 2, 3], findings.Select(finding => finding.Line));
    }

    [Fact]
    public void Unclosed_hints_are_reported()
    {
        Finding finding = Assert.Single(ContentRules.CheckHtml(MarkdownFile.Parse(Page, "<details>\n<summary>Hint</summary>\n\nTry this.\n")));

        Assert.Equal(1, finding.Line);
        Assert.Contains("isn't closed", finding.Message);
    }

    [Fact]
    public void Hints_open_and_close_in_the_same_list_item()
    {
        const string markdown = """
            1. Run the workflow.

               <details>
               <summary>Hint</summary>

               Check the logs.

            </details>
            """;

        List<Finding> findings = ContentRules.CheckHtml(MarkdownFile.Parse(Page, markdown)).OrderBy(finding => finding.Line).ToList();

        Assert.Equal([3, 8], findings.Select(finding => finding.Line));
        Assert.Contains("isn't closed", findings[0].Message);
        Assert.Contains("doesn't close a <details>", findings[1].Message);
    }

    [Fact]
    public void Inline_html_is_reported()
    {
        const string markdown = "Replace <path> with your folder.\n\nReplace `<path>` with your folder.\n";

        Finding finding = Assert.Single(ContentRules.CheckHtml(MarkdownFile.Parse(Page, markdown)));
        Assert.Equal(1, finding.Line);
        Assert.Contains("<path>", finding.Message);
    }

    [Fact]
    public void Supported_alerts_are_allowed()
    {
        const string markdown = "> [!NOTE]\n> Text.\n\n> [!CAUTION]\n> Careful.\n\n> A plain quote.\n";

        Assert.Empty(ContentRules.CheckAlerts(MarkdownFile.Parse(Page, markdown)));
    }

    [Theory]
    [InlineData("DANGER")]
    [InlineData("note")]
    public void Alert_types_github_does_not_support_are_reported(string kind)
    {
        Finding finding = Assert.Single(ContentRules.CheckAlerts(MarkdownFile.Parse(Page, $"> [!{kind}]\n> Text.\n")));

        Assert.Contains($"[!{kind}] isn't an alert type", finding.Message);
    }

    [Fact]
    public void Alert_text_starts_on_the_next_line()
    {
        Finding finding = Assert.Single(ContentRules.CheckAlerts(MarkdownFile.Parse(Page, "> [!NOTE] Text on the same line.\n")));

        Assert.Contains("on a line of its own", finding.Message);
    }

    [Theory]
    [InlineData("- Item\n\n  > [!NOTE]\n  > Text.\n", 3)]
    [InlineData("> > [!NOTE]\n> > Text.\n", 1)]
    public void Alerts_only_work_at_the_top_level(string markdown, int line)
    {
        Finding finding = Assert.Single(ContentRules.CheckAlerts(MarkdownFile.Parse(Page, markdown)));

        Assert.Equal(line, finding.Line);
        Assert.Contains("only work at the top level", finding.Message);
    }

    [Theory]
    [InlineData("**There's no Run workflow button.** Check the name.\n", 1)]
    [InlineData("Intro.\n\n- **Why it ran again:** a new commit.\n", 3)]
    [InlineData("Intro.\n\n__Is it done?__\n", 3)]
    [InlineData("Select **Commit changes.**\n", 1)]
    public void Bold_sentences_and_run_in_headings_are_reported(string markdown, int line)
    {
        Finding finding = Assert.Single(ContentRules.CheckBold(MarkdownFile.Parse(Page, markdown)));

        Assert.Equal(line, finding.Line);
        Assert.Contains("Don't bold a whole sentence", finding.Message);
    }

    [Fact]
    public void Bold_ui_labels_are_allowed()
    {
        const string markdown = "Select **Commit changes**.\n\nIn the **Find or create a branch...** box, type `add-tests`.\n\n*Optional.* Code such as `**Done.**` isn't bold.\n";

        Assert.Empty(ContentRules.CheckBold(MarkdownFile.Parse(Page, markdown)));
    }

    [Fact]
    public void Lessons_that_follow_the_guide_pass()
    {
        using var repository = new TempRepository()
            .Write("lessons/README.md", "# Lessons\n")
            .Write(Lesson, CompleteLesson("Lab 1: First", 1))
            .Write("lessons/explore/03-more.md", CompleteLesson("Lab 3: More", 3));

        Assert.Empty(ContentRules.CheckLessons(repository.Load()));
    }

    [Fact]
    public void A_repository_without_lessons_has_none_to_check()
    {
        using var repository = new TempRepository().Write("README.md", "# Workshop\n");

        Assert.Empty(ContentRules.CheckLessons(repository.Load()));
    }

    [Fact]
    public void Titles_start_with_the_lab_number()
    {
        Finding finding = Assert.Single(LessonFindings(CompleteLesson("First workflow", 1)));

        Assert.Equal(2, finding.Line);
        Assert.Contains("must start with \"Lab 1: \"", finding.Message);
    }

    [Theory]
    [InlineData("# Lab 1: The first one", "has to match the title")]
    [InlineData("# Lab 1: *First*", "has to match the title")]
    [InlineData("## Lab 1: First", "Start the lesson with an H1")]
    [InlineData("Some intro.", "Start the lesson with an H1")]
    public void Lessons_start_with_their_title(string firstLine, string expected)
    {
        string lesson = CompleteLesson("Lab 1: First", 1).Replace("# Lab 1: First", firstLine, StringComparison.Ordinal);

        Finding finding = Assert.Single(LessonFindings(lesson));
        Assert.Equal(9, finding.Line);
        Assert.Contains(expected, finding.Message);
    }

    [Fact]
    public void Lessons_have_one_h1()
    {
        Finding finding = Assert.Single(LessonFindings(CompleteLesson("Lab 1: First", 1, body: "Intro.\n\n# Steps")));

        Assert.Equal(13, finding.Line);
        Assert.Contains("Use ## or ### here", finding.Message);
    }

    [Fact]
    public void Lessons_have_a_result_section_and_a_next_step()
    {
        List<Finding> findings = LessonFindings(TempLessons.LessonText("Lab 1: First", 1));

        Assert.Equal(2, findings.Count);
        Assert.All(findings, finding => Assert.Equal(0, finding.Line));
        Assert.Contains("Add a \"## Check your result\" section", findings[0].Message);
        Assert.Contains("Add a \"## Next step\" section", findings[1].Message);
    }

    [Theory]
    [InlineData("## Check the result")]
    [InlineData("## Check your result.")]
    [InlineData("## Check *your* result")]
    [InlineData("### Check your result")]
    [InlineData("> ## Check your result")]
    public void The_result_section_uses_the_exact_heading(string heading)
    {
        string lesson = TempLessons.LessonText("Lab 1: First", 1, body: $"{heading}\n\nIt worked.\n\n## Next step\n\nGo back.");

        Finding finding = Assert.Single(LessonFindings(lesson));
        Assert.Contains("Add a \"## Check your result\" section", finding.Message);
    }

    [Fact]
    public void Lessons_have_one_of_each_section()
    {
        string lesson = CompleteLesson("Lab 1: First", 1, body: "## Next step\n\nOne.\n\n## Check your result\n\nTwo.");

        List<Finding> findings = LessonFindings(lesson);

        Assert.Equal([19, 23], findings.Select(finding => finding.Line));
        Assert.Contains("only one \"## Check your result\"", findings[0].Message);
        Assert.Contains("only one \"## Next step\"", findings[1].Message);
    }

    [Fact]
    public void Lessons_the_dashboard_can_not_load_are_reported()
    {
        using var repository = new TempRepository()
            .Write(Lesson, CompleteLesson("Lab 1: First", 1))
            .Write("lessons/foundation/02-second.md", CompleteLesson("Lab 1: Second", 1))
            .Write("lessons/foundation/03-third.md", CompleteLesson("Lab 3: Third", 3, difficulty: "Easy"));

        List<Finding> findings = ContentRules.CheckLessons(repository.Load());

        Assert.Equal(["lessons/foundation/02-second.md", "lessons/foundation/03-third.md"], findings.Select(finding => finding.Path));
        Assert.All(findings, finding => Assert.Equal(0, finding.Line));
        Assert.Contains("already uses order 1", findings[0].Message);
        Assert.Contains("\"difficulty\"", findings[1].Message);
    }

    [Fact]
    public void Lesson_files_outside_the_track_folders_are_reported()
    {
        using var repository = new TempRepository()
            .Write("lessons/README.md", "# Lessons\n")
            .Write("lessons/notes.md", "# Notes\n")
            .Write("lessons/advanced/01-extra.md", "# Extra\n")
            .Write("lessons/foundation/extra/01-more.md", "# More\n");

        List<Finding> findings = ContentRules.CheckLessons(repository.Load());

        Assert.Equal(["lessons/advanced/01-extra.md", "lessons/foundation/extra/01-more.md", "lessons/notes.md"], findings.Select(finding => finding.Path));
        Assert.All(findings, finding => Assert.Contains("lessons/foundation and lessons/explore", finding.Message));
    }

    // Runs a rule that needs the repository, for one Markdown file written next to the other files.
    private static List<Finding> Check(
        Func<MarkdownFile, ContentRepository, IEnumerable<Finding>> rule,
        string path,
        string markdown,
        params (string Path, string Content)[] otherFiles)
    {
        using var repository = new TempRepository();
        foreach ((string otherPath, string otherContent) in otherFiles)
        {
            repository.Write(otherPath, otherContent);
        }

        repository.Write(path, markdown);
        (ContentRepository content, MarkdownFile file) = repository.LoadMarkdown(path);
        return rule(file, content).ToList();
    }

    private static List<Finding> LessonFindings(string lesson)
    {
        using var repository = new TempRepository().Write(Lesson, lesson);
        return ContentRules.CheckLessons(repository.Load());
    }

    // A lesson with the sections every lesson needs, so a test only sees the finding it's about.
    private static string CompleteLesson(string title, int order, string body = "Some lesson text.", string difficulty = "Beginner")
    {
        return TempLessons.LessonText(title, order, body + LessonSections, difficulty: difficulty);
    }
}
