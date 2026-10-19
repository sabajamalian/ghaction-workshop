using System.Text;
using System.Text.RegularExpressions;
using Markdig.Extensions.Alerts;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using WorkshopDashboard.Models;
using WorkshopDashboard.Services;

namespace WorkshopDashboard.Tests.Content;

// The mechanical rules from docs/editorial-guide.md ("What the tests check").
// Each rule returns findings instead of failing on the first one, so a test run lists every problem at once.
internal static partial class ContentRules
{
    public const string LessonsReadme = "lessons/README.md";

    private static readonly HashSet<string> CheckedTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bash", ".bicep", ".config", ".cs", ".cshtml", ".csproj", ".css", ".editorconfig", ".gitattributes", ".gitignore",
        ".html", ".js", ".json", ".mjs", ".props", ".ps1", ".psm1", ".razor", ".sh", ".sln", ".slnx", ".targets", ".txt",
        ".xml", ".yaml", ".yml",
    };

    private static readonly HashSet<string> AlertKinds = new(StringComparer.Ordinal) { "NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION" };

    // Every lesson has these sections. The lesson page sets "Check your result" apart as the lesson's success criteria.
    private static readonly (string Heading, string Purpose)[] RequiredLessonSections =
    [
        (MarkdownRenderer.ResultHeading, "that describes what the learner can see when they've finished"),
        ("Next step", "that links to the next lesson or back to the lesson list"),
    ];

    private static readonly HashSet<string> VagueLinkText = new(StringComparer.Ordinal)
    {
        "click", "click here", "go", "go here", "here", "learn more", "link", "more", "page", "read more", "see here",
        "this", "this article", "this document", "this link", "this page",
    };

    // Files that have to stay exact copies of another file, and the file each one copies.
    public static readonly (string Copy, string Original)[] ExactCopies =
    [
        ("solutions/migration/migrated-ci.yml", "migration/azure-devops/expected-workflow.yml"),
    ];

    // Em dashes, en dashes, and emojis, outside code. Applies to Markdown and to the text files people read in the repository.
    public static List<Finding> CheckPunctuation(ContentRepository repository)
    {
        var findings = new List<Finding>();
        foreach (string path in repository.FileList)
        {
            if (ContentRepository.IsMarkdown(path))
            {
                findings.AddRange(CheckPunctuation(repository.Markdown(path)));
            }
            else if (IsCheckedTextFile(path))
            {
                findings.AddRange(CheckPunctuation(path, repository.ReadText(path), isCode: null));
            }
        }

        return findings;
    }

    public static IEnumerable<Finding> CheckPunctuation(MarkdownFile file)
    {
        return CheckPunctuation(file.Path, file.Text, file.IsCode);
    }

    // Vendored libraries, license texts, and the .lock.yml workflows that gh aw compile generates are copied as they are.
    public static bool IsCheckedTextFile(string path)
    {
        string name = path[(path.LastIndexOf('/') + 1)..];
        return CheckedTextExtensions.Contains(Path.GetExtension(name))
            && !path.Contains("/wwwroot/lib/", StringComparison.Ordinal)
            && !name.StartsWith("LICENSE", StringComparison.Ordinal)
            && !name.EndsWith(".lock.yml", StringComparison.Ordinal);
    }

    public static string? DescribeDash(int codePoint)
    {
        return codePoint switch
        {
            0x2014 => "an em dash (U+2014)",
            0x2013 => "an en dash (U+2013)",
            0x2012 or 0x2015 or 0x2E3A or 0x2E3B or 0xFE31 or 0xFE32 or 0xFE58 => $"a long dash (U+{codePoint:X4})",
            _ => null,
        };
    }

    public static bool IsEmoji(int codePoint)
    {
        return codePoint is (>= 0x1F000 and <= 0x1FAFF)
            or (>= 0x2600 and <= 0x27BF)
            or (>= 0x2B00 and <= 0x2BFF)
            or 0x231A or 0x231B
            or (>= 0x23E9 and <= 0x23F3)
            or (>= 0x23F8 and <= 0x23FA)
            or 0x200D or 0x20E3 or 0xFE0F
            or (>= 0xE0020 and <= 0xE007F);
    }

    // Fenced blocks with a language, and source= or excerpt= blocks that still match their files.
    public static List<Finding> CheckCodeBlocks(ContentRepository repository)
    {
        return repository.MarkdownFiles.SelectMany(file => CheckCodeBlocks(file, repository)).ToList();
    }

    public static IEnumerable<Finding> CheckCodeBlocks(MarkdownFile file, ContentRepository repository)
    {
        foreach (CodeBlock block in file.Document.Descendants<CodeBlock>())
        {
            int line = block.Line + 1;
            if (block is not FencedCodeBlock fenced)
            {
                yield return new Finding(file.Path, line, "This text is indented four or more spaces, so it renders as a code block. Use a fenced code block with a language, or remove the indentation.");
                continue;
            }

            if (fenced.ClosingFencedCharCount == 0)
            {
                yield return new Finding(file.Path, line, "This code block isn't closed, so everything after it renders as code. Add a closing fence.");
            }

            string language = fenced.Info ?? string.Empty;
            if (language.Length == 0)
            {
                yield return new Finding(file.Path, line, "Add a language after the opening fence, such as ```yaml, ```bash, or ```text.");
            }
            else if (file.IsLesson && !CodeLanguages.IsKnown(language))
            {
                yield return new Finding(file.Path, line, $"The dashboard doesn't recognize the language \"{language}\". Use one of: {string.Join(", ", CodeLanguages.Known)}.");
            }

            foreach (Finding finding in CheckSnippet(file, fenced, repository))
            {
                yield return finding;
            }
        }
    }

    // Links that resolve, link text that says where it goes, and no images.
    public static List<Finding> CheckLinks(ContentRepository repository)
    {
        return repository.MarkdownFiles.SelectMany(file => CheckLinks(file, repository)).ToList();
    }

    public static IEnumerable<Finding> CheckLinks(MarkdownFile file, ContentRepository repository)
    {
        foreach (LinkInline link in file.Document.Descendants<LinkInline>())
        {
            int line = file.LineAt(link.Span.Start);
            if (link.IsImage)
            {
                yield return new Finding(file.Path, line, "Remove the image. Screenshots go out of date when GitHub's interface changes, so describe the interface with its exact labels instead.");
                continue;
            }

            string text = NormalizeLinkText(MarkdownFile.PlainText(link));
            if (text.Length == 0)
            {
                yield return new Finding(file.Path, line, "This link has no text. Write text that says where the link goes.");
            }
            else if (VagueLinkText.Contains(text))
            {
                yield return new Finding(file.Path, line, $"The link text \"{text}\" doesn't say where the link goes. Name the destination instead, such as \"the workflow syntax reference\".");
            }

            foreach (Finding finding in CheckLinkTarget(file, repository, link.Url, line))
            {
                yield return finding;
            }
        }

        foreach (AutolinkInline autolink in file.Document.Descendants<AutolinkInline>())
        {
            if (!autolink.IsEmail && LinkResolver.Classify(autolink.Url) != LinkKind.SafeAbsolute)
            {
                yield return new Finding(file.Path, file.LineAt(autolink.Span.Start), $"The dashboard doesn't link {autolink.Url}. Use an https link.");
            }
        }
    }

    // Raw HTML other than the <details> lines used for hints shows up on the dashboard as plain text.
    public static List<Finding> CheckHtml(ContentRepository repository)
    {
        return repository.MarkdownFiles.SelectMany(CheckHtml).ToList();
    }

    public static IEnumerable<Finding> CheckHtml(MarkdownFile file)
    {
        foreach (HtmlBlock block in file.Document.Descendants<HtmlBlock>())
        {
            if (block.Type == HtmlBlockType.Comment)
            {
                continue;
            }

            for (int i = 0; i < block.Lines.Count; i++)
            {
                StringLine line = block.Lines.Lines[i];
                string text = line.Slice.ToString();
                if (text.Trim().Length > 0 && !AllowedHtml.TryGetSafeLine(text, out _))
                {
                    yield return new Finding(file.Path, line.Line + 1, "The dashboard shows this line as plain text. The only HTML it renders is <details>, <summary>Text</summary>, and </details>, each on a line of its own with a blank line after the summary.");
                }
            }
        }

        // A hint has to open and close in the same place, such as the same list item, or the page's structure breaks.
        foreach (ContainerBlock container in file.Document.Descendants<ContainerBlock>().Prepend(file.Document))
        {
            var open = new Stack<int>();
            foreach (Block child in container)
            {
                if (child is not HtmlBlock { Type: not HtmlBlockType.Comment } html)
                {
                    continue;
                }

                for (int i = 0; i < html.Lines.Count; i++)
                {
                    StringLine line = html.Lines.Lines[i];
                    string text = line.Slice.ToString().Trim();
                    if (text is "<details>" or "<details open>")
                    {
                        open.Push(line.Line + 1);
                    }
                    else if (text == "</details>" && !open.TryPop(out _))
                    {
                        yield return new Finding(file.Path, line.Line + 1, "This </details> doesn't close a <details> in the same list item or quote. Indent both lines the same way.");
                    }
                }
            }

            foreach (int line in open.Reverse())
            {
                yield return new Finding(file.Path, line, "This <details> isn't closed. Add </details> on a line of its own, indented the same way, after the hint.");
            }
        }

        foreach (HtmlInline html in file.Document.Descendants<HtmlInline>())
        {
            yield return new Finding(file.Path, file.LineAt(html.Span.Start), $"The dashboard shows {html.Tag} as plain text. Put placeholders such as <path> in backticks, and use Markdown instead of HTML.");
        }
    }

    // GitHub's five alert types, at the top level of the file, where GitHub and the dashboard render them.
    public static List<Finding> CheckAlerts(ContentRepository repository)
    {
        return repository.MarkdownFiles.SelectMany(CheckAlerts).ToList();
    }

    public static IEnumerable<Finding> CheckAlerts(MarkdownFile file)
    {
        foreach (QuoteBlock quote in file.Document.Descendants<QuoteBlock>())
        {
            int line = quote.Line + 1;
            if (quote is AlertBlock alert)
            {
                string kind = alert.Kind.ToString();
                if (!AlertKinds.Contains(kind))
                {
                    yield return new Finding(file.Path, line, $"[!{kind}] isn't an alert type GitHub supports. Use NOTE, TIP, IMPORTANT, WARNING, or CAUTION, in capital letters.");
                }

                continue;
            }

            if (quote.Count == 0 || quote[0] is not ParagraphBlock { Inline: not null } paragraph)
            {
                continue;
            }

            string firstLine = MarkdownFile.PlainText(paragraph.Inline, firstLineOnly: true).Trim();
            if (!AlertMarker().IsMatch(firstLine))
            {
                continue;
            }

            if (quote.Parent is MarkdownDocument)
            {
                yield return new Finding(file.Path, line, "Put the alert type on a line of its own, such as > [!NOTE], and start the text on the next line.");
            }
            else
            {
                yield return new Finding(file.Path, line, "Alerts only work at the top level of a file, not inside a list or another quote. Move the alert out, or remove the [!TYPE] line.");
            }
        }
    }

    // Bold marks UI labels and important instructions. Bold text that ends in punctuation is a sentence or a run-in heading.
    public static List<Finding> CheckBold(ContentRepository repository)
    {
        return repository.MarkdownFiles.SelectMany(CheckBold).ToList();
    }

    public static IEnumerable<Finding> CheckBold(MarkdownFile file)
    {
        foreach (EmphasisInline emphasis in file.Document.Descendants<EmphasisInline>())
        {
            if (emphasis.DelimiterCount != 2)
            {
                continue;
            }

            // Some UI labels end with an ellipsis, such as "Find or create a branch...".
            string text = MarkdownFile.PlainText(emphasis).TrimEnd();
            if (text.Length > 0 && ".?!:".Contains(text[^1]) && !text.EndsWith("...", StringComparison.Ordinal))
            {
                yield return new Finding(file.Path, file.LineAt(emphasis.Span.Start), "Don't bold a whole sentence or a run-in heading. Keep bold for UI labels and important instructions, and put punctuation after the closing **.");
            }
        }
    }

    // Lessons the dashboard can load, with titles and headings that follow the guide.
    public static List<Finding> CheckLessons(ContentRepository repository)
    {
        var findings = new List<Finding>();
        string folders = string.Join(" and ", LessonLoader.Folders.Select(folder => $"lessons/{folder}"));
        foreach (string path in repository.FileList)
        {
            if (path.StartsWith("lessons/", StringComparison.Ordinal)
                && ContentRepository.IsMarkdown(path)
                && path != LessonsReadme
                && !MarkdownFile.IsLessonPath(path))
            {
                findings.Add(new Finding(path, 0, $"The dashboard only reads lessons from {folders}, so it ignores this file. Move it into one of those folders."));
            }
        }

        if (!repository.Directories.Contains("lessons"))
        {
            return findings;
        }

        LessonLoadResult result = LessonLoader.Load(Path.Combine(repository.Root, "lessons"), new MarkdownRenderer(), "https://github.com/octo/workshop");
        findings.AddRange(result.Errors.Select(error => new Finding(error.SourcePath, 0, error.Message)));

        foreach (Lesson lesson in result.Lessons.Lessons)
        {
            if (repository.Files.Contains(lesson.SourcePath))
            {
                findings.AddRange(CheckLessonHeadings(repository.Markdown(lesson.SourcePath), lesson.Title, lesson.Order));
            }
        }

        return findings;
    }

    public static IEnumerable<Finding> CheckLessonHeadings(MarkdownFile file, string title, int order)
    {
        string prefix = $"Lab {order}: ";
        if (!title.StartsWith(prefix, StringComparison.Ordinal))
        {
            yield return new Finding(file.Path, FrontMatterLine(file, "title"), $"The title must start with \"{prefix}\" to match the order field.");
        }

        HeadingBlock? first = file.Document.Count > 0 && file.Document[0] is HeadingBlock { Level: 1 } heading ? heading : null;
        if (first is null)
        {
            int line = file.Document.Count > 0 ? file.Document[0].Line + 1 : Math.Max(file.FrontMatterLineCount, 1);
            yield return new Finding(file.Path, line, $"Start the lesson with an H1 that matches the title: # {title}");
        }
        else
        {
            Match source = AtxHeading().Match(file.LineText(first.Line + 1));
            if (!source.Success || source.Groups["text"].Value != title)
            {
                yield return new Finding(file.Path, first.Line + 1, $"The H1 has to match the title in the front matter: # {title}");
            }
        }

        foreach (HeadingBlock other in file.Document.Descendants<HeadingBlock>())
        {
            if (other.Level == 1 && other != first)
            {
                yield return new Finding(file.Path, other.Line + 1, "Use ## or ### here. The dashboard shows the title as the page's only H1.");
            }
        }

        foreach ((string section, string purpose) in RequiredLessonSections)
        {
            List<Block> found = file.Document.Where(block => MarkdownRenderer.IsSectionHeading(block, section)).ToList();
            if (found.Count == 0)
            {
                yield return new Finding(file.Path, 0, $"Add a \"## {section}\" section {purpose}. Use that exact heading.");
            }

            foreach (Block extra in found.Skip(1))
            {
                yield return new Finding(file.Path, extra.Line + 1, $"A lesson has only one \"## {section}\" section. Merge this one into the first.");
            }
        }
    }

    // Copies that still match their originals exactly. CRLF line endings and a byte order mark don't count, because Git
    // and editors on Windows can add them.
    public static List<Finding> CheckCopies(ContentRepository repository)
    {
        return CheckCopies(repository, ExactCopies);
    }

    public static List<Finding> CheckCopies(ContentRepository repository, IEnumerable<(string Copy, string Original)> copies)
    {
        var findings = new List<Finding>();
        foreach ((string copy, string original) in copies)
        {
            if (!repository.Files.Contains(copy) || !repository.Files.Contains(original))
            {
                findings.Add(new Finding(copy, 0, $"This file has to be an exact copy of {original}, but one of them is missing. Restore it, or remove the pair from {nameof(ExactCopies)} in ContentRules.cs."));
                continue;
            }

            string copyText = repository.ReadText(copy);
            string originalText = repository.ReadText(original);
            if (copyText == originalText)
            {
                continue;
            }

            string[] copyLines = SplitLines(copyText);
            string[] originalLines = SplitLines(originalText);
            int shared = Math.Min(copyLines.Length, originalLines.Length);
            int index = Enumerable.Range(0, shared).FirstOrDefault(i => copyLines[i] != originalLines[i], shared);
            string problem = index < shared
                ? $"This line doesn't match line {index + 1} of {original}."
                : copyLines.Length != originalLines.Length
                    ? $"This file has {CountLines(copyLines.Length)}, but {original} has {CountLines(originalLines.Length)}."
                    : $"Only one of this file and {original} ends with a line break.";
            findings.Add(new Finding(copy, Math.Min(index + 1, copyLines.Length), $"{problem} Keep this file an exact copy: edit {original}, then copy it over this file."));
        }

        return findings;
    }

    // The lines an editor shows: a line break at the end of the file doesn't start another line.
    private static string[] SplitLines(string text)
    {
        return (text.EndsWith('\n') ? text[..^1] : text).Split('\n');
    }

    private static IEnumerable<Finding> CheckPunctuation(string path, string text, Func<int, bool>? isCode)
    {
        string[] lines = text.Split('\n');
        int offset = 0;
        for (int number = 1; number <= lines.Length; number++)
        {
            string line = lines[number - 1];
            var problems = new List<string>();
            bool hasDash = false;
            bool hasEmoji = false;

            for (int i = 0; i < line.Length;)
            {
                Rune.DecodeFromUtf16(line.AsSpan(i), out Rune rune, out int consumed);
                if (isCode?.Invoke(offset + i) != true)
                {
                    string? dash = DescribeDash(rune.Value);
                    if (dash is not null)
                    {
                        hasDash = true;
                        AddOnce(problems, dash);
                    }
                    else if (IsEmoji(rune.Value))
                    {
                        hasEmoji = true;
                        AddOnce(problems, $"an emoji (U+{rune.Value:X4})");
                    }
                }

                i += Math.Max(consumed, 1);
            }

            foreach (Match entity in DashEntity().Matches(line))
            {
                if (isCode?.Invoke(offset + entity.Index) != true)
                {
                    hasDash = true;
                    AddOnce(problems, $"{entity.Value}, which renders as a dash");
                }
            }

            if (problems.Count > 0)
            {
                var message = new StringBuilder("Found ").Append(JoinWithAnd(problems)).Append('.');
                if (hasDash)
                {
                    message.Append(" Use a comma, colon, period, or parentheses instead. For a range, write \"to\", such as 10 to 15.");
                }

                if (hasEmoji)
                {
                    message.Append(" Remove the emoji.");
                }

                yield return new Finding(path, number, message.ToString());
            }

            offset += line.Length + 1;
        }
    }

    private static IEnumerable<Finding> CheckSnippet(MarkdownFile file, FencedCodeBlock block, ContentRepository repository)
    {
        int line = block.Line + 1;
        string? kind = null;
        string target = string.Empty;
        foreach (string token in (block.Arguments ?? string.Empty).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = token.IndexOf('=');
            string name = equals < 0 ? token : token[..equals];
            if (kind is null && name is "source" or "excerpt" && equals > 0)
            {
                kind = name;
                target = token[(equals + 1)..];
                continue;
            }

            yield return new Finding(file.Path, line, $"Remove \"{token}\" from the opening fence. After the language, a code block can only have source=PATH or excerpt=PATH.");
        }

        if (kind is null)
        {
            yield break;
        }

        if (!IsRepositoryFilePath(target))
        {
            yield return new Finding(file.Path, line, $"\"{kind}={target}\" needs a path from the repository root, such as {kind}=.github/workflows/build.yml, without a leading / or any . or .. folders.");
            yield break;
        }

        if (!repository.Files.Contains(target))
        {
            yield return new Finding(file.Path, line, $"{kind}= points to {target}, which isn't in the repository. Check the spelling and the capitalization.");
            yield break;
        }

        string[] blockLines = new string[block.Lines.Count];
        int[] blockLineNumbers = new int[block.Lines.Count];
        for (int i = 0; i < block.Lines.Count; i++)
        {
            blockLines[i] = block.Lines.Lines[i].Slice.ToString().TrimEnd();
            blockLineNumbers[i] = block.Lines.Lines[i].Line + 1;
        }

        string[] fileLines = repository.ReadText(target).Split('\n').Select(text => text.TrimEnd()).ToArray();
        Finding? mismatch = kind == "source"
            ? CompareSource(file.Path, line, target, blockLines, blockLineNumbers, fileLines)
            : CompareExcerpt(file.Path, line, target, blockLines, fileLines);

        if (mismatch is not null)
        {
            yield return mismatch;
        }
    }

    private static Finding? CompareSource(string path, int fenceLine, string target, string[] blockLines, int[] blockLineNumbers, string[] fileLines)
    {
        int blockCount = CountWithoutTrailingBlankLines(blockLines);
        int fileCount = CountWithoutTrailingBlankLines(fileLines);

        for (int i = 0; i < Math.Min(blockCount, fileCount); i++)
        {
            if (blockLines[i] != fileLines[i])
            {
                return new Finding(path, blockLineNumbers[i], $"This line doesn't match line {i + 1} of {target}, which is \"{Shorten(fileLines[i])}\". Copy the file into the lesson again.");
            }
        }

        if (blockCount < fileCount)
        {
            return new Finding(path, fenceLine, $"This source= block stops after {CountLines(blockCount)}, but {target} has {CountLines(fileCount)}. Copy the whole file, or use excerpt= to show part of it.");
        }

        if (blockCount > fileCount)
        {
            return new Finding(path, blockLineNumbers[fileCount], $"This source= block has {CountLines(blockCount)}, but {target} only has {CountLines(fileCount)}. Copy the file into the lesson again.");
        }

        return null;
    }

    private static Finding? CompareExcerpt(string path, int fenceLine, string target, string[] blockLines, string[] fileLines)
    {
        int start = Array.FindIndex(blockLines, text => text.Length > 0);
        if (start < 0)
        {
            return new Finding(path, fenceLine, "This excerpt= block is empty. Copy the lines you want to show from the file.");
        }

        int end = Array.FindLastIndex(blockLines, text => text.Length > 0);
        string[] excerpt = Dedent(blockLines[start..(end + 1)]);

        for (int i = 0; i + excerpt.Length <= fileLines.Length; i++)
        {
            if (Dedent(fileLines[i..(i + excerpt.Length)]).SequenceEqual(excerpt, StringComparer.Ordinal))
            {
                return null;
            }
        }

        var trimmedFileLines = fileLines.Select(text => text.Trim()).ToHashSet(StringComparer.Ordinal);
        int missing = Array.FindIndex(excerpt, text => !trimmedFileLines.Contains(text.Trim()));
        string detail = missing >= 0
            ? $"The line \"{Shorten(excerpt[missing].Trim())}\" isn't in the file."
            : "Every line is in the file, but not in this order or with this indentation.";
        return new Finding(path, fenceLine, $"This excerpt= block doesn't match a run of lines in {target}. {detail} Copy the lines from the file again.");
    }

    private static IEnumerable<Finding> CheckLinkTarget(MarkdownFile file, ContentRepository repository, string? url, int line)
    {
        string target = url?.Trim() ?? string.Empty;
        if (target.Length == 0)
        {
            yield return new Finding(file.Path, line, "This link has no target. Add the URL, or the relative path to a file in this repository.");
            yield break;
        }

        switch (LinkResolver.Classify(target))
        {
            case LinkKind.SafeAbsolute:
                yield break;
            case LinkKind.Unsafe:
                yield return new Finding(file.Path, line, $"The dashboard blocks the link {target}. Use an https link, or a relative path to a file in this repository.");
                yield break;
            case LinkKind.Fragment:
                if (target == "#")
                {
                    yield return new Finding(file.Path, line, "The link target # is a placeholder. Link to a heading, a file, or a URL.");
                }
                else if (!file.Anchors.Contains(Uri.UnescapeDataString(target[1..])))
                {
                    yield return new Finding(file.Path, line, $"No heading in this file has the anchor {target}. Check the heading's text, or remove the link.");
                }

                yield break;
        }

        RepositoryLink? resolved = LinkResolver.ResolveRepositoryPath(target, file.Path);
        if (resolved is null)
        {
            yield return new Finding(file.Path, line, $"The link {target} points outside the repository. Use an https link instead.");
            yield break;
        }

        if (!repository.Exists(resolved.Path))
        {
            yield return new Finding(file.Path, line, $"The link {target} points to {resolved.Path}, which isn't in the repository. Check the path and its capitalization.");
            yield break;
        }

        string anchor = resolved.Fragment.TrimStart('#');
        if (anchor.Length > 0
            && ContentRepository.IsMarkdown(resolved.Path)
            && repository.Files.Contains(resolved.Path)
            && !repository.Markdown(resolved.Path).Anchors.Contains(Uri.UnescapeDataString(anchor)))
        {
            yield return new Finding(file.Path, line, $"{resolved.Path} has no heading with the anchor #{anchor}. Check the heading's text.");
        }
    }

    private static int FrontMatterLine(MarkdownFile file, string field)
    {
        for (int line = 2; line < file.FrontMatterLineCount; line++)
        {
            if (file.LineText(line).StartsWith(field + ":", StringComparison.Ordinal))
            {
                return line;
            }
        }

        return 1;
    }

    // A path from the repository root, such as .github/workflows/build.yml.
    private static bool IsRepositoryFilePath(string path)
    {
        return path.Length > 0
            && !path.Contains('\\')
            && path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static string[] Dedent(string[] lines)
    {
        int indent = lines.Where(text => text.Length > 0).Select(text => text.Length - text.TrimStart(' ').Length).DefaultIfEmpty(0).Min();
        return lines.Select(text => text.Length == 0 ? text : text[indent..]).ToArray();
    }

    private static int CountWithoutTrailingBlankLines(string[] lines)
    {
        int count = lines.Length;
        while (count > 0 && lines[count - 1].Length == 0)
        {
            count--;
        }

        return count;
    }

    private static string NormalizeLinkText(string text)
    {
        return Whitespace().Replace(text.Trim(), " ").ToLowerInvariant().TrimEnd('.', ',', ':', ';', '!', '?');
    }

    private static string Shorten(string text)
    {
        return text.Length <= 80 ? text : text[..77] + "...";
    }

    private static void AddOnce(List<string> items, string item)
    {
        if (!items.Contains(item))
        {
            items.Add(item);
        }
    }

    private static string JoinWithAnd(List<string> items)
    {
        return items.Count switch
        {
            1 => items[0],
            2 => items[0] + " and " + items[1],
            _ => string.Join(", ", items[..^1]) + ", and " + items[^1],
        };
    }

    private static string CountLines(int count)
    {
        return count == 1 ? "1 line" : $"{count} lines";
    }

    [GeneratedRegex(@"^ {0,3}#[ \t]+(?<text>.*?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex AtxHeading();

    [GeneratedRegex(@"^\[![A-Za-z]+\]")]
    private static partial Regex AlertMarker();

    [GeneratedRegex("&(?:mdash|ndash|horbar|#821[0-3]|#[xX]201[2-5]);")]
    private static partial Regex DashEntity();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
