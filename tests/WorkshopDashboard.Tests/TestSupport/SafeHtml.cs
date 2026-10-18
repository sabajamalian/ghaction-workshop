using System.Text.RegularExpressions;
using Xunit;

namespace WorkshopDashboard.Tests.TestSupport;

// Checks rendered lesson HTML against the elements, attributes, and link targets the renderer is allowed to produce.
// Every "<" in the output must start one of these tags. Anything else means raw HTML got through.
internal static partial class SafeHtml
{
    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "a", "blockquote", "br", "code", "del", "details", "div", "em", "h2", "h3", "h4", "h5", "h6", "hr",
        "img", "li", "ol", "p", "pre", "section", "span", "strong", "summary", "table", "tbody", "td", "th", "thead", "tr", "ul",
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "alt", "class", "href", "id", "open", "src", "start", "title",
    };

    public static void AssertSafe(string html)
    {
        MatchCollection tags = TagPattern().Matches(html);
        int angleBrackets = html.Count(c => c == '<');
        Assert.True(tags.Count == angleBrackets, $"The HTML contains a '<' that doesn't start an allowed tag:\n{html}");

        foreach (Match tag in tags)
        {
            string element = tag.Groups["name"].Value;
            Assert.True(AllowedElements.Contains(element), $"Unexpected <{element}> element:\n{html}");

            foreach (Capture attribute in tag.Groups["attribute"].Captures)
            {
                Assert.True(AllowedAttributes.Contains(attribute.Value), $"Unexpected {attribute.Value} attribute on <{element}>:\n{html}");
            }
        }

        foreach (Match url in UrlAttributePattern().Matches(html))
        {
            string value = url.Groups["url"].Value;
            Assert.True(SafeUrlPattern().IsMatch(value), $"Unexpected link target \"{value}\":\n{html}");
        }
    }

    // A start or end tag whose attributes are all bare names or name="value" pairs.
    [GeneratedRegex("<(?:/)?(?<name>[a-z][a-z0-9]*)(?:\\s+(?<attribute>[a-z-]+)(?:=\"[^\"<>]*\")?)*\\s*/?>")]
    private static partial Regex TagPattern();

    [GeneratedRegex("\\s(?:href|src)=\"(?<url>[^\"]*)\"")]
    private static partial Regex UrlAttributePattern();

    [GeneratedRegex("^(?:#|/lessons/[a-z0-9-]+(?:#[^\\s]*)?$|https://|http://|mailto:)")]
    private static partial Regex SafeUrlPattern();
}
