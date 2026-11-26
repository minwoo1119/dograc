using Dograc.Core.Models;

namespace Dograc.Core.Services;

/// <summary>
/// Persists and broadcasts desktop application settings.
/// </summary>
public interface ISettingsStore
{
    AppSettings Current { get; }

    event EventHandler<AppSettings>? SettingsChanged;

    AppSettings Load();

    void Save(AppSettings settings);

    void RegisterDocumentLocalPath(string fileName, string fullPath);

    string? ResolveDocumentLocalPath(string fileName);
}

/// <summary>
/// Queries a local Ollama server for connectivity and available models.
/// </summary>
public interface IOllamaService
{
    Task<OllamaConnectionResult> CheckAndListModelsAsync(string endpointUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Checks GitHub Releases / Velopack feed for newer versions of dograc Desktop and applies updates.
/// </summary>
public interface IUpdateService
{
    string CurrentVersion { get; }

    bool IsAutoUpdateSupported { get; }

    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task DownloadAndApplyUpdateAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);
}
