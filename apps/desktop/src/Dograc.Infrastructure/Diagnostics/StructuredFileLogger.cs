using Dograc.Core.Errors;

namespace Dograc.Infrastructure.Diagnostics;

/// <summary>
/// Thread-safe structured logger with automatic size rotation (5 MB x 3),
/// 14-day retention cleanup, sensitive data masking, and live in-memory buffer.
/// </summary>
public sealed class StructuredFileLogger : IStructuredLogger
{
    private const int MaxInMemoryEntries = 2000;
    private const long MaxLogFileBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxRotatedFiles = 3;
    private const int RetentionDays = 14;

    private readonly object _lock = new();
    private readonly List<StructuredLogEntry> _entries = [];

    public StructuredFileLogger()
        : this(GetDefaultLogDirectory())
    {
    }

    public StructuredFileLogger(string logDirectory)
    {
        LogDirectory = logDirectory;
        TryEnsureDirectoryAndPruneOldLogs();
    }

    public string LogDirectory { get; }

    public string CurrentLogFilePath => Path.Combine(LogDirectory, $"dograc-{DateTime.Now:yyyyMMdd}.log");

    public IReadOnlyList<StructuredLogEntry> RecentEntries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public event EventHandler<StructuredLogEntry>? EntryLogged;

    public void Log(StructuredLogEntry entry)
    {
        var sanitized = entry with
        {
            Message = SensitiveDataSanitizer.Sanitize(entry.Message),
            ExceptionMessage = entry.ExceptionMessage is null ? null : SensitiveDataSanitizer.Sanitize(entry.ExceptionMessage),
            StackTrace = entry.StackTrace is null ? null : SensitiveDataSanitizer.Sanitize(entry.StackTrace),
            ContextSummary = entry.ContextSummary is null ? null : SensitiveDataSanitizer.Sanitize(entry.ContextSummary)
        };

        lock (_lock)
        {
            _entries.Add(sanitized);
            if (_entries.Count > MaxInMemoryEntries)
            {
                _entries.RemoveAt(0);
            }

            WriteToFileSafe(sanitized);
        }

        EntryLogged?.Invoke(this, sanitized);
    }

    public void LogInfo(
        string component,
        string service,
        string operation,
        string message,
        string? correlationId = null,
        string? contextSummary = null)
    {
        Log(new StructuredLogEntry(
            DateTimeOffset.Now, "Info", component, service, operation, message,
            CorrelationId: correlationId, ContextSummary: contextSummary));
    }

    public void LogWarning(
        string component,
        string service,
        string operation,
        string message,
        string? errorCode = null,
        string? correlationId = null,
        string? contextSummary = null)
    {
        Log(new StructuredLogEntry(
            DateTimeOffset.Now, "Warning", component, service, operation, message,
            ErrorCode: errorCode, CorrelationId: correlationId, ContextSummary: contextSummary));
    }

    public void LogError(ApplicationError error, Exception? exception = null)
    {
        var level = error.Severity switch
        {
            ErrorSeverity.Critical => "Critical",
            ErrorSeverity.Warning => "Warning",
            ErrorSeverity.Information => "Info",
            _ => "Error"
        };

        Log(new StructuredLogEntry(
            Timestamp: error.Timestamp,
            Level: level,
            Component: error.Context.Component,
            Service: error.Context.Service,
            Operation: error.Context.Operation,
            Message: $"{error.Title} — {error.Description}",
            ErrorCode: error.ErrorCode,
            CorrelationId: error.CorrelationId,
            ExceptionType: exception?.GetType().Name,
            ExceptionMessage: exception?.Message,
            StackTrace: exception?.StackTrace,
            ProcessExitCode: error.Context.ProcessExitCode,
            HttpStatusCode: error.Context.HttpStatusCode,
            ContextSummary: error.TechnicalDetails));
    }

    public void ClearInMemory()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private void WriteToFileSafe(StructuredLogEntry entry)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var target = CurrentLogFilePath;
            RotateIfNeeded(target);

            var line = entry.FormattedLine;
            if (!string.IsNullOrWhiteSpace(entry.ExceptionType))
            {
                line += $" | ex={entry.ExceptionType}: {entry.ExceptionMessage}";
            }
            if (entry.HttpStatusCode is not null)
            {
                line += $" | http={entry.HttpStatusCode}";
            }
            if (entry.ProcessExitCode is not null)
            {
                line += $" | exit={entry.ProcessExitCode}";
            }

            File.AppendAllText(target, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StructuredFileLogger] Failed writing log file: {ex.Message}");
        }
    }

    private static void RotateIfNeeded(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var info = new FileInfo(filePath);
        if (info.Length < MaxLogFileBytes)
        {
            return;
        }

        for (var idx = MaxRotatedFiles; idx >= 1; idx--)
        {
            var src = idx == 1 ? filePath : $"{filePath}.{idx - 1}";
            var dst = $"{filePath}.{idx}";
            if (File.Exists(dst))
            {
                File.Delete(dst);
            }
            if (File.Exists(src))
            {
                File.Move(src, dst);
            }
        }
    }

    private void TryEnsureDirectoryAndPruneOldLogs()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            foreach (var path in Directory.EnumerateFiles(LogDirectory, "dograc-*.log*"))
            {
                var info = new FileInfo(path);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    info.Delete();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StructuredFileLogger] Log directory init/prune warning: {ex.Message}");
        }
    }

    private static string GetDefaultLogDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "dograc", "logs");
    }
}
