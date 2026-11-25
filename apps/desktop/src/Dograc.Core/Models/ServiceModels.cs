namespace Dograc.Core.Models;

/// <summary>
/// Indicates whether a service was already running externally or started by dograc Desktop.
/// </summary>
public enum ServiceOwnership
{
    None,
    External,
    Managed
}

/// <summary>
/// Lifecycle state of an individual local service component.
/// </summary>
public enum ServiceState
{
    Unknown,
    Checking,
    Starting,
    Ready,
    Degraded,
    Stopped,
    Error
}

/// <summary>
/// Overall startup pipeline state for the desktop shell.
/// </summary>
public enum StartupState
{
    Idle,
    Checking,
    Starting,
    Ready,
    NeedsSetup,
    Failed
}

/// <summary>
/// Severity level for system and subprocess log entries.
/// </summary>
public enum ServiceLogLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Runtime status snapshot for a local service component (Docker, Ollama, FastAPI).
/// </summary>
public sealed record ServiceComponentStatus(
    string Key,
    string DisplayName,
    string Description,
    ServiceState State,
    ServiceOwnership Ownership,
    string StatusText,
    string DetailMessage,
    string? ActionHint = null)
{
    public bool IsReady => State == ServiceState.Ready;

    public string OwnershipLabel => Ownership switch
    {
        ServiceOwnership.Managed => "앱 관리",
        ServiceOwnership.External => "외부 실행",
        _ => "미실행"
    };
}

/// <summary>
/// Timestamped log entry produced by the service manager or managed subprocesses.
/// </summary>
public sealed record ServiceLogEntry(
    DateTimeOffset Timestamp,
    string Source,
    ServiceLogLevel Level,
    string Message)
{
    public string FormattedLine =>
        $"[{Timestamp.ToLocalTime():HH:mm:ss}] [{Source}] {Message}";
}

/// <summary>
/// Preflight environment inspection result used to decide whether Setup Wizard is required.
/// </summary>
public sealed record EnvironmentCheckSummary(
    string ProjectRoot,
    bool HasApiDirectory,
    bool HasEnvFile,
    bool HasComposeFile,
    bool HasDockerExecutable,
    bool HasDockerDaemon,
    bool HasOllamaService,
    bool HasBackendRuntime,
    IReadOnlyList<string> MissingRequirements)
{
    public bool NeedsSetupWizard => !HasApiDirectory || !HasBackendRuntime;
}

/// <summary>
/// Runtime configuration for local service discovery and startup.
/// </summary>
public sealed record ServiceRuntimeOptions(
    string ProjectRoot,
    string ApiHost = "127.0.0.1",
    int ApiPort = 8000,
    string OllamaHost = "127.0.0.1",
    int OllamaPort = 11434,
    int StartupTimeoutSeconds = 35)
{
    public string ApiBaseUrl => $"http://{ApiHost}:{ApiPort}";
    public string ApiHealthLiveUrl => $"{ApiBaseUrl}/api/v1/health/live";
    public string ApiHealthReadyUrl => $"{ApiBaseUrl}/api/v1/health/ready";
    public string OllamaTagsUrl => $"http://{OllamaHost}:{OllamaPort}/api/tags";
    public string ApiDirectory => Path.Combine(ProjectRoot, "apps", "api");
    public string ComposeFilePath => Path.Combine(ProjectRoot, "infra", "compose.yaml");
    public string EnvFilePath => Path.Combine(ProjectRoot, ".env");
    public string EnvExampleFilePath => Path.Combine(ProjectRoot, ".env.example");
}
