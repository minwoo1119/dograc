using System.Text.RegularExpressions;
using Dograc.Core.Models;

namespace Dograc.Core.Parsing;

/// <summary>
/// Parses grounded inline citations formatted as [FileName, p.Number].
/// </summary>
public static partial class CitationParser
{
    [GeneratedRegex(@"\[([^\[\]]+),\s*p\.(\d+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CitationRegex();

    public static IReadOnlyList<CitationReference> ExtractUniqueCitations(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var results = new List<CitationReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in CitationRegex().Matches(content))
        {
            var fileName = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(fileName) || !int.TryParse(match.Groups[2].Value, out var page))
            {
                continue;
            }

            var key = $"{fileName}#{page}";
            if (seen.Add(key))
            {
                results.Add(new CitationReference(fileName, page));
            }
        }

        return results;
    }

    public static IReadOnlyList<CitationSegment> SplitTextWithCitations(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var segments = new List<CitationSegment>();
        var lastIndex = 0;

        foreach (Match match in CitationRegex().Matches(text))
        {
            if (match.Index > lastIndex)
            {
                segments.Add(new CitationSegment(text[lastIndex..match.Index], null));
            }

            var fileName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(fileName) && int.TryParse(match.Groups[2].Value, out var page))
            {
                segments.Add(new CitationSegment(match.Value, new CitationReference(fileName, page)));
            }
            else
            {
                segments.Add(new CitationSegment(match.Value, null));
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            segments.Add(new CitationSegment(text[lastIndex..], null));
        }

        return segments;
    }
}
