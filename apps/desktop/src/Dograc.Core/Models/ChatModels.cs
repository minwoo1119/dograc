namespace Dograc.Core.Models;

/// <summary>
/// Normalized document citation parsed from [FileName, p.Number].
/// </summary>
public sealed record CitationReference(string FileName, int PageNumber, int Index = 0)
{
    public string DisplayPage => $"p.{PageNumber}";

    public string DisplayLabel => $"{FileName}, {DisplayPage}";

    public string NumberBadge => Index > 0 ? $"[{Index}]" : $"[{DisplayLabel}]";
}

/// <summary>
/// Segment produced when splitting text into plain runs and inline citations.
/// </summary>
public sealed record CitationSegment(string Text, CitationReference? Citation)
{
    public bool IsCitation => Citation is not null;
}

/// <summary>
/// Request to inspect a specific RAG trace and optionally focus a cited source chunk/page.
/// </summary>
public sealed record EvidenceSelectionRequest(
    Guid TraceId,
    string? HighlightFileName = null,
    int? HighlightPageNumber = null);
