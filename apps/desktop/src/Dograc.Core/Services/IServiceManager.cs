using Dograc.Core.Models;

namespace Dograc.Core.Services;

/// <summary>
/// Manages local dograc infrastructure services (Docker, Ollama, FastAPI), ownership tracking, and logs.
/// </summary>
public interface IServiceManager : IDisposable
{
    ServiceRuntimeOptions Options { get; }

    IReadOnlyList<ServiceComponentStatus> CurrentStatuses { get; }

    IReadOnlyList<ServiceLogEntry> LogHistory { get; }

    bool IsApiReady { get; }

    event EventHandler<IReadOnlyList<ServiceComponentStatus>>? StatusesChanged;

    event EventHandler<ServiceLogEntry>? LogAdded;

    Task<EnvironmentCheckSummary> InspectEnvironmentAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceComponentStatus>> RefreshStatusesAsync(CancellationToken cancellationToken = default);

    Task<bool> StartAllAsync(CancellationToken cancellationToken = default);

    Task StopManagedServicesAsync(CancellationToken cancellationToken = default);

    void ClearLogs();
}

/// <summary>
/// Coordinates asynchronous startup health checks, auto-start, and Setup Wizard detection.
/// </summary>
public interface IStartupOrchestrator
{
    StartupState State { get; }

    string StatusBannerText { get; }

    string? FailureReason { get; }

    EnvironmentCheckSummary? LastEnvironmentCheck { get; }

    event EventHandler<StartupState>? StateChanged;

    Task RunStartupAsync(CancellationToken cancellationToken = default);
}
