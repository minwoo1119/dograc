using System.Collections.Concurrent;
using Dograc.Core.Errors;

namespace Dograc.App.Services;

/// <summary>
/// Centralized toast notification dispatcher with deduplication to prevent infinite alert storms.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(4);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentKeys = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ToastNotificationItem>? NotificationAdded;
    public event EventHandler<Guid>? NotificationDismissed;

    public void NotifyError(ApplicationError error, Func<Task>? retryCallback = null)
    {
        var dedupKey = $"{error.ErrorCode}:{error.Context.Component}:{error.Context.Operation}";
        if (IsDuplicate(dedupKey))
        {
            return;
        }

        var item = new ToastNotificationItem(
            Id: Guid.NewGuid(),
            DeduplicationKey: dedupKey,
            Severity: error.Severity,
            Title: error.Title,
            Message: error.Description,
            ErrorCode: error.ErrorCode,
            SourceError: error,
            Actions: error.RecoveryActions.Take(2).ToList(),
            CreatedAt: DateTimeOffset.Now,
            RetryCallback: retryCallback);

        NotificationAdded?.Invoke(this, item);
    }

    public void NotifyInfo(string title, string message, string? deduplicationKey = null)
    {
        var key = deduplicationKey ?? $"INFO:{title}:{message}";
        if (IsDuplicate(key))
        {
            return;
        }

        var item = new ToastNotificationItem(
            Id: Guid.NewGuid(),
            DeduplicationKey: key,
            Severity: ErrorSeverity.Information,
            Title: title,
            Message: message,
            ErrorCode: null,
            SourceError: null,
            Actions: [],
            CreatedAt: DateTimeOffset.Now);

        NotificationAdded?.Invoke(this, item);
    }

    public void NotifySuccess(string title, string message, string? deduplicationKey = null)
        => NotifyInfo(title, message, deduplicationKey ?? $"OK:{title}:{message}");

    public void Dismiss(Guid id) => NotificationDismissed?.Invoke(this, id);

    private bool IsDuplicate(string key)
    {
        var now = DateTimeOffset.Now;
        if (_recentKeys.TryGetValue(key, out var last) && now - last < DeduplicationWindow)
        {
            return true;
        }

        _recentKeys[key] = now;
        return false;
    }
}
