using Dograc.Core.Errors;

namespace Dograc.App.Services;

/// <summary>
/// Central error handling hub that classifies exceptions, writes structured logs,
/// maintains recent error history, and notifies the UI layer.
/// </summary>
public sealed class ErrorHandlingService : IErrorHandlingService
{
    private const int MaxRecentErrors = 50;

    private readonly IErrorMapper _mapper;
    private readonly IStructuredLogger _logger;
    private readonly INotificationService _notificationService;
    private readonly object _lock = new();
    private readonly List<ApplicationError> _recentErrors = [];

    public ErrorHandlingService(
        IErrorMapper mapper,
        IStructuredLogger logger,
        INotificationService notificationService)
    {
        _mapper = mapper;
        _logger = logger;
        _notificationService = notificationService;
    }

    public IReadOnlyList<ApplicationError> RecentErrors
    {
        get
        {
            lock (_lock)
            {
                return _recentErrors.ToList();
            }
        }
    }

    public event EventHandler<ApplicationError>? ErrorRecorded;

    public ApplicationError Handle(
        Exception exception,
        ErrorContext context,
        Func<Task>? retryCallback = null,
        bool notifyToast = true)
    {
        var error = _mapper.Map(exception, context);
        return RecordAndNotify(error, exception, retryCallback, notifyToast);
    }

    public ApplicationError HandleError(
        ApplicationError error,
        Func<Task>? retryCallback = null,
        bool notifyToast = true)
    {
        return RecordAndNotify(error, null, retryCallback, notifyToast);
    }

    private ApplicationError RecordAndNotify(
        ApplicationError error,
        Exception? exception,
        Func<Task>? retryCallback,
        bool notifyToast)
    {
        _logger.LogError(error, exception);

        lock (_lock)
        {
            _recentErrors.Add(error);
            if (_recentErrors.Count > MaxRecentErrors)
            {
                _recentErrors.RemoveAt(0);
            }
        }

        ErrorRecorded?.Invoke(this, error);

        if (notifyToast)
        {
            _notificationService.NotifyError(error, retryCallback);
        }

        return error;
    }
}
