namespace Dograc.Infrastructure.Diagnostics;

/// <summary>
/// Bounded retry policy with exponential backoff, per-attempt timeout, and cancellation support.
/// Strictly used for idempotent operations (never unbounded retries).
/// </summary>
public static class RetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxRetries = 2,
        TimeSpan? baseDelay = null,
        TimeSpan? perAttemptTimeout = null,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        var delay = baseDelay ?? TimeSpan.FromMilliseconds(350);
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var attemptCts = perAttemptTimeout is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;
            if (attemptCts is not null && perAttemptTimeout is not null)
            {
                attemptCts.CancelAfter(perAttemptTimeout.Value);
            }

            var effectiveCt = attemptCts?.Token ?? cancellationToken;

            try
            {
                return await operation(effectiveCt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException oce) when (attemptCts is not null && attemptCts.IsCancellationRequested)
            {
                var timeoutEx = new TimeoutException(
                    $"Operation timed out after {perAttemptTimeout?.TotalSeconds:0.#}s.",
                    oce);
                if (attempt >= maxRetries || (shouldRetry is not null && !shouldRetry(timeoutEx)))
                {
                    throw timeoutEx;
                }
            }
            catch (Exception ex)
            {
                if (attempt >= maxRetries || (shouldRetry is not null && !shouldRetry(ex)))
                {
                    throw;
                }
            }

            attempt++;
            var backoff = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, attempt - 1));
            await Task.Delay(backoff, cancellationToken);
        }
    }
}
