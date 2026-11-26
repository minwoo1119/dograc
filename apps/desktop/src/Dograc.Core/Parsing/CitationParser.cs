using System.Text.RegularExpressions;
using Dograc.Core.Models;

namespace Dograc.Core.Parsing;

/// <summary>
/// Parses grounded inline citations formatted as [FileName, p.Number] and assigns compact sequential numbers [1], [2], ...
/// while preserving the exact underlying FileName and PageNumber reference.
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
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in CitationRegex().Matches(content))
        {
            var fileName = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(fileName) || !int.TryParse(match.Groups[2].Value, out var page))
            {
                continue;
            }

            var key = $"{fileName}#{page}";
            if (!seen.ContainsKey(key))
            {
                var number = results.Count + 1;
                seen[key] = number;
                results.Add(new CitationReference(fileName, page, number));
            }
        }

        return results;
    }

    public static IReadOnlyDictionary<string, int> BuildCitationNumberMap(string? fullMessageContent)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var citation in ExtractUniqueCitations(fullMessageContent))
        {
            map[$"{citation.FileName}#{citation.PageNumber}"] = citation.Index;
        }

        return map;
    }

    public static IReadOnlyList<CitationSegment> SplitTextWithCitations(
        string? text,
        IReadOnlyDictionary<string, int>? numberMap = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var localMap = numberMap ?? BuildCitationNumberMap(text);
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
                var key = $"{fileName}#{page}";
                var index = localMap.TryGetValue(key, out var assigned) ? assigned : 0;
                segments.Add(new CitationSegment(match.Value, new CitationReference(fileName, page, index)));
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
