using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.Tables;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace WorkshopDashboard.Services;

// Turns lesson Markdown into HTML.
// Raw HTML is never passed through, except the <details> and <summary> lines that lessons use for hints.
public sealed class MarkdownRenderer
{
    // The section that tells learners how to confirm they finished the lesson. The lesson page sets it apart.
    public const string ResultHeading = "Check your result";

    private readonly MarkdownPipeline _pipeline = CreatePipeline();

    public static MarkdownPipeline CreatePipeline()
    {
        return new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .UseAlertBlocks(WriteAlertTitle)
            .Build();
    }

    public MarkdownDocument Parse(string markdown)
    {
        return Markdown.Parse(markdown, _pipeline);
    }

    public string RenderLesson(string markdown, string sourcePath, LinkResolver links)
    {
        MarkdownDocument document = Parse(markdown);

        // The page layout shows the title, so drop the H1 that makes the file readable on GitHub.
        if (document.Count > 0 && document[0] is HeadingBlock { Level: 1 })
        {
            document.RemoveAt(0);
        }

        RewriteLinks(document, sourcePath, links);
        RemoveColumnAlignment(document);
        WrapResultSections(document);
        return Render(document);
    }

    // True for a top-level ## heading whose whole text is the given plain text, such as "## Check your result".
    public static bool IsSectionHeading(Block block, string text)
    {
        return block is HeadingBlock { Level: 2, Parent: MarkdownDocument, Inline.FirstChild: LiteralInline { NextSibling: null } literal }
            && literal.Content.ToString() == text;
    }

    public string Render(MarkdownDocument document)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        _pipeline.Setup(renderer);

        renderer.ObjectRenderers.ReplaceOrAdd<HtmlBlockRenderer>(new AllowlistHtmlBlockRenderer());
        renderer.ObjectRenderers.ReplaceOrAdd<HtmlInlineRenderer>(new EscapedHtmlInlineRenderer());
        renderer.ObjectRenderers.ReplaceOrAdd<CodeBlockRenderer>(new LabeledCodeBlockRenderer());
        renderer.ObjectRenderers.Insert(0, new ResultSectionRenderer());

        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    private static void RewriteLinks(MarkdownDocument document, string sourcePath, LinkResolver links)
    {
        foreach (LinkInline link in document.Descendants<LinkInline>().ToList())
        {
            link.Url = links.Resolve(link.Url, sourcePath);
            link.GetDynamicUrl = null;
        }

        foreach (AutolinkInline autolink in document.Descendants<AutolinkInline>().ToList())
        {
            if (!autolink.IsEmail && LinkResolver.Classify(autolink.Url) != LinkKind.SafeAbsolute)
            {
                autolink.ReplaceBy(new LiteralInline(autolink.Url));
            }
        }
    }

    // Moves each "Check your result" heading, and everything up to the next ## heading, into one block.
    private static void WrapResultSections(MarkdownDocument document)
    {
        for (int i = 0; i < document.Count; i++)
        {
            if (!IsSectionHeading(document[i], ResultHeading))
            {
                continue;
            }

            var section = new ResultSectionBlock();
            do
            {
                Block block = document[i];
                document.RemoveAt(i);
                section.Add(block);
            }
            while (i < document.Count && document[i] is not HeadingBlock { Level: <= 2 });

            document.Insert(i, section);
        }
    }

    // Markdig writes column alignment as a style attribute, which the Content Security Policy blocks.
    private static void RemoveColumnAlignment(MarkdownDocument document)
    {
        foreach (Table table in document.Descendants<Table>())
        {
            foreach (TableColumnDefinition column in table.ColumnDefinitions)
            {
                column.Alignment = null;
            }
        }
    }

    private static void WriteAlertTitle(HtmlRenderer renderer, StringSlice kind)
    {
        string text = kind.ToString().ToUpperInvariant() switch
        {
            "NOTE" => "Note",
            "TIP" => "Tip",
            "IMPORTANT" => "Important",
            "WARNING" => "Warning",
            "CAUTION" => "Caution",
            _ => kind.ToString(),
        };

        renderer.Write("<p class=\"markdown-alert-title\">");
        renderer.WriteEscape(text);
        renderer.WriteLine("</p>");
    }
}

public static partial class AllowedHtml
{
    // Returns the HTML to emit for one line of a raw HTML block, or false when the line isn't allowed.
    public static bool TryGetSafeLine(string line, [NotNullWhen(true)] out string? safe)
    {
        string trimmed = line.Trim();
        if (trimmed is "<details>" or "<details open>" or "</details>")
        {
            safe = trimmed;
            return true;
        }

        Match summary = SummaryPattern().Match(trimmed);
        if (summary.Success)
        {
            string text = WebUtility.HtmlDecode(summary.Groups[1].Value);
            safe = "<summary>" + WebUtility.HtmlEncode(text) + "</summary>";
            return true;
        }

        safe = null;
        return false;
    }

    [GeneratedRegex("^<summary>([^<>]+)</summary>$")]
    private static partial Regex SummaryPattern();
}

internal sealed class AllowlistHtmlBlockRenderer : HtmlObjectRenderer<HtmlBlock>
{
    protected override void Write(HtmlRenderer renderer, HtmlBlock obj)
    {
        if (obj.Type == HtmlBlockType.Comment)
        {
            return;
        }

        StringLineGroup lines = obj.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines.Lines[i].Slice.ToString();
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (AllowedHtml.TryGetSafeLine(line, out string? safe))
            {
                renderer.WriteLine(safe);
            }
            else
            {
                renderer.Write("<p>");
                renderer.WriteEscape(line.Trim());
                renderer.WriteLine("</p>");
            }
        }
    }
}

internal sealed class EscapedHtmlInlineRenderer : HtmlObjectRenderer<HtmlInline>
{
    protected override void Write(HtmlRenderer renderer, HtmlInline obj)
    {
        renderer.WriteEscape(obj.Tag);
    }
}

internal sealed class ResultSectionBlock : ContainerBlock
{
    public ResultSectionBlock()
        : base(null)
    {
    }
}

internal sealed class ResultSectionRenderer : HtmlObjectRenderer<ResultSectionBlock>
{
    protected override void Write(HtmlRenderer renderer, ResultSectionBlock obj)
    {
        renderer.EnsureLine();
        renderer.WriteLine("<section class=\"lesson-check\">");
        renderer.WriteChildren(obj);
        renderer.EnsureLine();
        renderer.WriteLine("</section>");
    }
}

// Wraps each code block with a header that names the language. The copy button is added by site.js.
internal sealed class LabeledCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
{
    private readonly CodeBlockRenderer _inner = new();

    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        string? language = obj is FencedCodeBlock fenced ? fenced.Info : null;

        renderer.Write("<div class=\"code-block\">");
        renderer.Write("<div class=\"code-block-header\"><span class=\"code-block-language\">");
        renderer.WriteEscape(CodeLanguages.DisplayName(language));
        renderer.WriteLine("</span></div>");
        ((IMarkdownObjectRenderer)_inner).Write(renderer, obj);
        renderer.WriteLine("</div>");
    }
}

public static class CodeLanguages
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yaml"] = "YAML",
        ["yml"] = "YAML",
        ["bash"] = "Bash",
        ["sh"] = "Shell",
        ["shell"] = "Shell",
        ["powershell"] = "PowerShell",
        ["pwsh"] = "PowerShell",
        ["csharp"] = "C#",
        ["cs"] = "C#",
        ["javascript"] = "JavaScript",
        ["js"] = "JavaScript",
        ["json"] = "JSON",
        ["xml"] = "XML",
        ["text"] = "Text",
        ["plaintext"] = "Text",
        ["console"] = "Output",
        ["markdown"] = "Markdown",
        ["md"] = "Markdown",
        ["diff"] = "Diff",
        ["ini"] = "INI",
    };

    public static IEnumerable<string> Known => Names.Keys.Order(StringComparer.Ordinal);

    public static bool IsKnown(string? language)
    {
        return language is not null && Names.ContainsKey(language);
    }

    public static string DisplayName(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "Code";
        }

        return Names.TryGetValue(language, out string? name) ? name : language;
    }
}
