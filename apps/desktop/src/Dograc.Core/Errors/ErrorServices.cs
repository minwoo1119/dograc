using Dograc.Core.Models;

namespace Dograc.Core.Errors;

public interface IErrorMapper
{
    ApplicationError Map(Exception exception, ErrorContext context);

    ApplicationError MapApiException(ApiException exception, ErrorContext context);

    ApplicationError MapDocumentFailureStage(string? failureCode, string? message, ErrorContext context);
}

public interface IStructuredLogger
{
    string LogDirectory { get; }
    string CurrentLogFilePath { get; }
    IReadOnlyList<StructuredLogEntry> RecentEntries { get; }

    event EventHandler<StructuredLogEntry>? EntryLogged;

    void Log(StructuredLogEntry entry);
    void LogInfo(string component, string service, string operation, string message, string? correlationId = null, string? contextSummary = null);
    void LogWarning(string component, string service, string operation, string message, string? errorCode = null, string? correlationId = null, string? contextSummary = null);
    void LogError(ApplicationError error, Exception? exception = null);
    void ClearInMemory();
}

public interface INotificationService
{
    event EventHandler<ToastNotificationItem>? NotificationAdded;
    event EventHandler<Guid>? NotificationDismissed;

    void NotifyError(ApplicationError error, Func<Task>? retryCallback = null);
    void NotifyInfo(string title, string message, string? deduplicationKey = null);
    void NotifySuccess(string title, string message, string? deduplicationKey = null);
    void Dismiss(Guid id);
}

public interface IErrorHandlingService
{
    IReadOnlyList<ApplicationError> RecentErrors { get; }

    event EventHandler<ApplicationError>? ErrorRecorded;

    ApplicationError Handle(
        Exception exception,
        ErrorContext context,
        Func<Task>? retryCallback = null,
        bool notifyToast = true);

    ApplicationError HandleError(
        ApplicationError error,
        Func<Task>? retryCallback = null,
        bool notifyToast = true);
}

public interface IRecoveryActionService
{
    Task ExecuteAsync(
        RecoveryActionDescriptor action,
        ApplicationError? sourceError = null,
        Func<Task>? retryCallback = null);
}

public interface IDiagnosticService
{
    string GetIncludedDataSummary();
    string BuildSanitizedReport(ApplicationError? focusError = null);
    Task<string> ExportReportAsync(string filePath, ApplicationError? focusError = null);
    string SanitizeSensitiveText(string? input);
}
