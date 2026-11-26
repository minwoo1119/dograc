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

public sealed record OllamaPullProgress(
    string Status,
    long CompletedBytes,
    long TotalBytes,
    int Percent);

/// <summary>
/// Queries a local Ollama server for connectivity, available models, and in-app model pulls.
/// </summary>
public interface IOllamaService
{
    Task<OllamaConnectionResult> CheckAndListModelsAsync(string endpointUrl, CancellationToken cancellationToken = default);

    Task PullModelAsync(
        string endpointUrl,
        string modelName,
        IProgress<OllamaPullProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("이 Ollama 서비스 구현은 인앱 모델 다운로드를 지원하지 않습니다.");
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
