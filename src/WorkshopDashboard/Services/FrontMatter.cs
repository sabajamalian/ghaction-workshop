using System.Text;
using System.Text.RegularExpressions;

namespace WorkshopDashboard.Services;

public sealed record FrontMatterDocument(IReadOnlyDictionary<string, string> Values, string Body);

// Reads the small YAML block at the top of each lesson.
// Supports only flat "key: value" pairs, which is all the lessons use.
// Values that contain ": " or " #" must be quoted, because YAML (and GitHub's file preview) would reject them otherwise.
public static partial class FrontMatter
{
    private const string Delimiter = "---";

    public static bool TryParse(string text, out FrontMatterDocument? document, out string? error)
    {
        document = null;
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalized.StartsWith('\uFEFF'))
        {
            normalized = normalized[1..];
        }

        if (!normalized.StartsWith(Delimiter + "\n", StringComparison.Ordinal))
        {
            error = "The file doesn't start with a front matter block. Add a line containing only --- at the top.";
            return false;
        }

        string[] lines = normalized.Split('\n');
        int closingLine = Array.IndexOf(lines, Delimiter, 1);
        if (closingLine < 0)
        {
            error = "The front matter block isn't closed. Add a line containing only --- after the last field.";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < closingLine; i++)
        {
            string line = lines[i];
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            Match match = FieldPattern().Match(line);
            if (!match.Success)
            {
                error = $"Line {i + 1} isn't a \"key: value\" pair.";
                return false;
            }

            string key = match.Groups["key"].Value;
            if (values.ContainsKey(key))
            {
                error = $"The field \"{key}\" appears more than once.";
                return false;
            }

            if (!TryParseScalar(match.Groups["value"].Value, out string value, out string? scalarError))
            {
                error = $"The field \"{key}\" on line {i + 1} has an invalid value. {scalarError}";
                return false;
            }

            values[key] = value;
        }

        string body = string.Join('\n', lines.Skip(closingLine + 1));
        document = new FrontMatterDocument(values, body);
        error = null;
        return true;
    }

    private static bool TryParseScalar(string raw, out string value, out string? error)
    {
        string trimmed = raw.Trim();
        value = string.Empty;
        error = null;

        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed[0] == '"')
        {
            return TryParseDoubleQuoted(trimmed, out value, out error);
        }

        if (trimmed[0] == '\'')
        {
            return TryParseSingleQuoted(trimmed, out value, out error);
        }

        if ("[]{}&*!|>%@`#,".Contains(trimmed[0])
            || trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("? ", StringComparison.Ordinal)
            || trimmed.Contains(": ", StringComparison.Ordinal)
            || trimmed.EndsWith(':')
            || trimmed.Contains(" #", StringComparison.Ordinal))
        {
            error = "Wrap the value in double quotes.";
            return false;
        }

        value = trimmed;
        return true;
    }

    private static bool TryParseDoubleQuoted(string raw, out string value, out string? error)
    {
        var builder = new StringBuilder();
        for (int i = 1; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '\\' && i + 1 < raw.Length)
            {
                char next = raw[++i];
                switch (next)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    default:
                        value = string.Empty;
                        error = $"The escape sequence \\{next} isn't supported.";
                        return false;
                }
            }
            else if (c == '"')
            {
                return EnsureNothingAfterQuote(raw, i, builder, out value, out error);
            }
            else
            {
                builder.Append(c);
            }
        }

        value = string.Empty;
        error = "The closing double quote is missing.";
        return false;
    }

    private static bool TryParseSingleQuoted(string raw, out string value, out string? error)
    {
        var builder = new StringBuilder();
        for (int i = 1; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '\'')
            {
                if (i + 1 < raw.Length && raw[i + 1] == '\'')
                {
                    builder.Append('\'');
                    i++;
                    continue;
                }

                return EnsureNothingAfterQuote(raw, i, builder, out value, out error);
            }

            builder.Append(c);
        }

        value = string.Empty;
        error = "The closing single quote is missing.";
        return false;
    }

    private static bool EnsureNothingAfterQuote(string raw, int closingIndex, StringBuilder builder, out string value, out string? error)
    {
        if (raw[(closingIndex + 1)..].Trim().Length > 0)
        {
            value = string.Empty;
            error = "Remove the text after the closing quote.";
            return false;
        }

        value = builder.ToString();
        error = null;
        return true;
    }

    [GeneratedRegex("^(?<key>[a-z][a-z0-9_]*):(?:[ \\t]+(?<value>.*))?$")]
    private static partial Regex FieldPattern();
}
