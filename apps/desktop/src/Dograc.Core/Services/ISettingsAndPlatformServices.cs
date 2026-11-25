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
/// Checks GitHub Releases for newer versions of dograc Desktop.
/// </summary>
public interface IUpdateService
{
    string CurrentVersion { get; }

    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}
