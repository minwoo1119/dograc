namespace Dograc.Core.Errors;

public enum ErrorSeverity
{
    Information,
    Warning,
    Error,
    Critical
}

public enum ErrorCategory
{
    Startup,
    DockerInfra,
    OllamaModel,
    BackendApi,
    DocumentIndexing,
    RagChat,
    WorkspaceData,
    UpdateInstall,
    Application
}

public enum RecoveryActionKind
{
    Retry,
    StartServices,
    RecheckConnection,
    OpenModelSettings,
    OpenSystemPage,
    OpenSetupWizard,
    OpenUpdateSettings,
    OpenDocumentsPage,
    LaunchDockerDesktop,
    LaunchOllama,
    CopyModelCommand,
    ReselectFile,
    ReindexDocument,
    RestoreInput,
    ViewErrorDetails,
    CopyDiagnostics,
    ExportDiagnosticReport,
    OpenInstallGuide,
    OpenReleasePage
}

public sealed record RecoveryActionDescriptor(
    RecoveryActionKind Kind,
    string Label,
    bool IsPrimary = false,
    string? Parameter = null,
    string? Tooltip = null);

public sealed record ErrorContext(
    string Component,
    string Service,
    string Operation,
    string? CorrelationId = null,
    int? HttpStatusCode = null,
    int? ProcessExitCode = null,
    string? PipelineStage = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public string ResolvedCorrelationId =>
        string.IsNullOrWhiteSpace(CorrelationId)
            ? Guid.NewGuid().ToString("N")[..12]
            : CorrelationId;
}

/// <summary>
/// Standardized user-facing + diagnostic error model across dograc Desktop.
/// Separates clear Korean guidance from technical diagnostic details.
/// </summary>
public sealed record ApplicationError(
    string ErrorCode,
    ErrorSeverity Severity,
    ErrorCategory Category,
    string Title,
    string Description,
    string Cause,
    bool IsConfirmedCause,
    string Impact,
    string Resolution,
    IReadOnlyList<RecoveryActionDescriptor> RecoveryActions,
    string TechnicalDetails,
    DateTimeOffset Timestamp,
    string CorrelationId,
    ErrorContext Context,
    bool IsRetryable = false,
    string? PipelineStage = null)
{
    public string CausePrefixLabel => IsConfirmedCause ? "확인된 원인" : "추정 원인";

    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

public sealed record StructuredLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Component,
    string Service,
    string Operation,
    string Message,
    string? ErrorCode = null,
    string? CorrelationId = null,
    string? ExceptionType = null,
    string? ExceptionMessage = null,
    string? StackTrace = null,
    int? ProcessExitCode = null,
    int? HttpStatusCode = null,
    string? ContextSummary = null)
{
    public string FormattedLine
    {
        get
        {
            var codePart = string.IsNullOrWhiteSpace(ErrorCode) ? string.Empty : $" [{ErrorCode}]";
            var corrPart = string.IsNullOrWhiteSpace(CorrelationId) ? string.Empty : $" (cid:{CorrelationId})";
            return $"[{Timestamp.ToLocalTime():HH:mm:ss}] [{Level}] [{Service}/{Component}]{codePart} {Message}{corrPart}";
        }
    }
}

public sealed record ToastNotificationItem(
    Guid Id,
    string DeduplicationKey,
    ErrorSeverity Severity,
    string Title,
    string Message,
    string? ErrorCode,
    ApplicationError? SourceError,
    IReadOnlyList<RecoveryActionDescriptor> Actions,
    DateTimeOffset CreatedAt,
    Func<Task>? RetryCallback = null);

/// <summary>
/// Exception carrying a pre-classified ApplicationError from Infrastructure/Core layers.
/// </summary>
public sealed class DogracApplicationException : Exception
{
    public DogracApplicationException(ApplicationError error, Exception? innerException = null)
        : base(error.Description, innerException)
    {
        Error = error;
    }

    public ApplicationError Error { get; }
}
