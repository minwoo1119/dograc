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
/// Full lifecycle state machine for an individual local service component.
/// </summary>
public enum ServiceState
{
    Unknown,
    NotInstalled,
    Stopped,
    Checking,
    Starting,
    Initializing,
    Ready,
    Degraded,
    Unreachable,
    Failed,
    Stopping,
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
    string? ActionHint = null,
    string? ErrorCode = null)
{
    public bool IsReady => State == ServiceState.Ready;

    public bool IsUsable => State is ServiceState.Ready or ServiceState.Degraded;

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
    public string LevelLabel => Level switch
    {
        ServiceLogLevel.Error => "Error",
        ServiceLogLevel.Warning => "Warning",
        _ => "Info"
    };

    public string FormattedLine =>
        $"[{Timestamp.ToLocalTime():HH:mm:ss}] [{LevelLabel}] [{Source}] {Message}";
}

/// <summary>
/// Detailed diagnostic check item for Setup Wizard (version, path, running/reachable distinction, official guide).
/// </summary>
public sealed record EnvironmentDiagnosticItem(
    string Key,
    string Name,
    bool IsRequired,
    bool IsInstalled,
    string DetectedVersion,
    string RequiredVersion,
    bool IsRunning,
    bool IsReachable,
    string ExecutableOrPath,
    string? FailureReason,
    string Resolution,
    string? OfficialGuideUrl,
    ServiceState State)
{
    public bool IsSatisfied => State is ServiceState.Ready or ServiceState.Degraded;

    public string StatusBadge => State switch
    {
        ServiceState.Ready => "정상",
        ServiceState.Degraded => "부분 준비",
        ServiceState.NotInstalled => "미설치",
        ServiceState.Stopped => "설치됨 · 미실행",
        ServiceState.Unreachable => "연결 불가",
        ServiceState.Failed or ServiceState.Error => "오류",
        _ => "확인 필요"
    };
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
    IReadOnlyList<string> MissingRequirements,
    IReadOnlyList<EnvironmentDiagnosticItem>? Diagnostics = null)
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
