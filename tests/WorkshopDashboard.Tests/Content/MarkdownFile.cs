using System.Text;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using WorkshopDashboard.Services;

namespace WorkshopDashboard.Tests.Content;

// One Markdown file, parsed the way the dashboard parses it.
internal sealed class MarkdownFile
{
    private const string FrontMatterDelimiter = "---";

    private readonly int[] _lineStarts;
    private readonly Lazy<bool[]> _codeMask;
    private readonly Lazy<IReadOnlySet<string>> _anchors;

    private MarkdownFile(string path, string text, int frontMatterLineCount, MarkdownDocument document)
    {
        Path = path;
        Text = text;
        Lines = text.Split('\n');
        FrontMatterLineCount = frontMatterLineCount;
        Document = document;
        IsLesson = IsLessonPath(path);

        _lineStarts = new int[Lines.Length];
        for (int i = 1; i < Lines.Length; i++)
        {
            _lineStarts[i] = _lineStarts[i - 1] + Lines[i - 1].Length + 1;
        }

        _codeMask = new Lazy<bool[]>(BuildCodeMask);
        _anchors = new Lazy<IReadOnlySet<string>>(FindAnchors);
    }

    // The dashboard's extensions, plus exact positions for inline elements so findings point at the right line.
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAlertBlocks()
        .UsePreciseSourceLocation()
        .Build();

    public string Path { get; }

    // The whole file with "\n" line endings, front matter included.
    public string Text { get; }

    public string[] Lines { get; }

    // The number of lines the front matter uses, including both --- lines. Zero when there isn't any.
    public int FrontMatterLineCount { get; }

    public MarkdownDocument Document { get; }

    public bool IsLesson { get; }

    // The heading anchors the page has once it's rendered, such as "check-your-result".
    public IReadOnlySet<string> Anchors => _anchors.Value;

    public static MarkdownFile Parse(string path, string text)
    {
        string normalized = ContentRepository.Normalize(text);
        int frontMatterLines = CountFrontMatterLines(normalized);

        // Blank out the front matter instead of removing it, so offsets and line numbers still match the file.
        string markdown = frontMatterLines == 0 ? normalized : MaskLines(normalized, frontMatterLines);
        return new MarkdownFile(path, normalized, frontMatterLines, Markdown.Parse(markdown, Pipeline));
    }

    public static bool IsLessonPath(string path)
    {
        string[] parts = path.Split('/');
        return parts.Length == 3
            && parts[0] == "lessons"
            && LessonLoader.Folders.Contains(parts[1])
            && ContentRepository.IsMarkdown(parts[2]);
    }

    // The 1-based line that contains the character at this offset.
    public int LineAt(int offset)
    {
        int index = Array.BinarySearch(_lineStarts, Math.Clamp(offset, 0, Math.Max(Text.Length - 1, 0)));
        return (index >= 0 ? index : ~index - 1) + 1;
    }

    public string LineText(int line)
    {
        return Lines[line - 1];
    }

    public int LineStart(int line)
    {
        return _lineStarts[line - 1];
    }

    // True when the character at this offset is inside a code block or a code span.
    public bool IsCode(int offset)
    {
        return _codeMask.Value[offset];
    }

    // The text a reader sees, without Markdown syntax: "Use **the** `uses` key" becomes "Use the uses key".
    public static string PlainText(ContainerInline? container, bool firstLineOnly = false)
    {
        var builder = new StringBuilder();
        AppendPlainText(container, builder, firstLineOnly);
        return builder.ToString();
    }

    private static int CountFrontMatterLines(string text)
    {
        if (!text.StartsWith(FrontMatterDelimiter + "\n", StringComparison.Ordinal))
        {
            return 0;
        }

        string[] lines = text.Split('\n');
        int closing = Array.IndexOf(lines, FrontMatterDelimiter, 1);
        return closing < 0 ? 0 : closing + 1;
    }

    private static string MaskLines(string text, int lineCount)
    {
        var builder = new StringBuilder(text);
        int line = 0;
        for (int i = 0; i < builder.Length && line < lineCount; i++)
        {
            if (builder[i] == '\n')
            {
                line++;
            }
            else
            {
                builder[i] = ' ';
            }
        }

        return builder.ToString();
    }

    // Returns false once it reaches a line break and firstLineOnly is set.
    private static bool AppendPlainText(ContainerInline? container, StringBuilder builder, bool firstLineOnly)
    {
        for (Inline? inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
                case LineBreakInline when firstLineOnly:
                    return false;
                case LineBreakInline:
                    builder.Append(' ');
                    break;
                case HtmlEntityInline entity:
                    builder.Append(entity.Transcoded.ToString());
                    break;
                case AutolinkInline autolink:
                    builder.Append(autolink.Url);
                    break;
                case HtmlInline html:
                    builder.Append(html.Tag);
                    break;
                case ContainerInline nested:
                    if (!AppendPlainText(nested, builder, firstLineOnly))
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    private bool[] BuildCodeMask()
    {
        var mask = new bool[Text.Length + 1];

        // A code block covers whole lines, from the opening fence to the closing one.
        foreach (CodeBlock block in Document.Descendants<CodeBlock>())
        {
            int first = LineAt(block.Span.Start);
            int last = LineAt(block.Span.End);
            int start = LineStart(first);
            int end = last < Lines.Length ? LineStart(last + 1) : Text.Length;
            Array.Fill(mask, true, start, end - start);
        }

        foreach (CodeInline code in Document.Descendants<CodeInline>())
        {
            int start = Math.Clamp(code.Span.Start, 0, Text.Length);
            int end = Math.Clamp(code.Span.End + 1, start, Text.Length);
            Array.Fill(mask, true, start, end - start);
        }

        return mask;
    }

    private IReadOnlySet<string> FindAnchors()
    {
        // The dashboard drops a lesson's first H1 because the page layout already shows the title.
        HeadingBlock? dropped = IsLesson && Document.Count > 0 && Document[0] is HeadingBlock { Level: 1 } title ? title : null;

        var anchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (HeadingBlock heading in Document.Descendants<HeadingBlock>())
        {
            string? id = heading.GetAttributes().Id;
            if (heading != dropped && !string.IsNullOrEmpty(id))
            {
                anchors.Add(id);
            }
        }

        return anchors;
    }
}
