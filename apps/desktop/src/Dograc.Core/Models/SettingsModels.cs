using Dograc.Core.Theme;

namespace Dograc.Core.Models;

/// <summary>
/// Persisted user preferences and local document path mappings for dograc Desktop.
/// </summary>
public sealed record AppSettings
{
    public AppThemeMode ThemeMode { get; init; } = AppThemeMode.System;

    public string OllamaEndpointUrl { get; init; } = "http://localhost:11434";

    public string SelectedModelName { get; init; } = "qwen2.5:7b";

    public bool StopManagedServicesOnExit { get; init; } = true;

    public Dictionary<string, string> DocumentLocalPaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string NormalizedOllamaV1Endpoint
        => $"{OllamaEndpointUrl.Trim().TrimEnd('/')}"
            .EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? OllamaEndpointUrl.Trim().TrimEnd('/')
            : $"{OllamaEndpointUrl.Trim().TrimEnd('/')}/v1";
}

/// <summary>
/// Metadata for a locally installed Ollama model reported by GET /api/tags.
/// </summary>
public sealed record OllamaModelInfo(
    string Name,
    long SizeBytes,
    string? ParameterSize,
    string? Family)
{
    public string SizeLabel => SizeBytes <= 0
        ? "크기 정보 없음"
        : $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):0.0} GB";

    public string Subtitle => string.IsNullOrWhiteSpace(ParameterSize)
        ? SizeLabel
        : $"{ParameterSize} · {SizeLabel}";
}

/// <summary>
/// Result of testing connectivity to a local Ollama runtime.
/// </summary>
public sealed record OllamaConnectionResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<OllamaModelInfo> Models);

/// <summary>
/// Result of checking GitHub Releases for a newer dograc desktop version.
/// </summary>
public sealed record UpdateCheckResult(
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseNotes,
    string ReleaseUrl,
    string? InstallerUrl,
    string Message);

/// <summary>
/// Request to display a document and target page inside the right Context Panel Preview tab.
/// </summary>
public sealed record PreviewDocumentRequest(
    string FileName,
    int PageNumber = 1,
    string? ExcerptText = null);
