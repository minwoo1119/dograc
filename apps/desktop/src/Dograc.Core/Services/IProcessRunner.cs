namespace Dograc.Core.Services;

/// <summary>
/// Result of a short-lived CLI process execution (e.g., docker info / docker compose).
/// </summary>
public sealed record ProcessCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Represents a long-running subprocess started and owned by dograc Desktop.
/// </summary>
public interface IManagedProcess : IDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
    int? ExitCode { get; }
    void KillTree();
}

/// <summary>
/// Abstraction over executable discovery and subprocess execution for testability.
/// </summary>
public interface IProcessRunner
{
    string? FindExecutable(string name);

    Task<ProcessCommandResult> RunCommandAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    IManagedProcess StartLongRunning(
        string fileName,
        string arguments,
        string workingDirectory,
        Action<string> onOutputLine);

    IManagedProcess StartLongRunning(
        string fileName,
        string arguments,
        string workingDirectory,
        Action<string> onOutputLine,
        IReadOnlyDictionary<string, string>? environmentVariables)
        => StartLongRunning(fileName, arguments, workingDirectory, onOutputLine);
}

/// <summary>
/// Abstraction over HTTP and TCP health probes for local services.
/// </summary>
public interface IHealthProbe
{
    Task<bool> IsUrlAccessibleAsync(string url, TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<bool> IsPortOpenAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default);
}
