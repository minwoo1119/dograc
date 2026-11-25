using System.Diagnostics;
using System.Text;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Wraps a Windows subprocess with safe process-tree termination.
/// </summary>
internal sealed class ManagedSystemProcess : IManagedProcess
{
    private readonly Process _process;
    private bool _disposed;

    public ManagedSystemProcess(Process process)
    {
        _process = process;
    }

    public int ProcessId => _process.Id;

    public bool HasExited
    {
        get
        {
            try
            {
                return _disposed || _process.HasExited;
            }
            catch
            {
                return true;
            }
        }
    }

    public int? ExitCode
    {
        get
        {
            try
            {
                return !_disposed && _process.HasExited ? _process.ExitCode : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public void KillTree()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // Ignore race conditions if process already exited.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _process.Dispose();
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}

/// <summary>
/// Discovers local executables and executes short-lived or long-running processes without console windows.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    private static readonly string[] WindowsExtensions = [".exe", ".cmd", ".bat"];

    public string? FindExecutable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (Path.IsPathRooted(name) && File.Exists(name))
        {
            return name;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var found = TryFindInDirectory(dir.Trim(), name);
            if (found is not null)
            {
                return found;
            }
        }

        foreach (var customDir in GetFallbackDirectories(name))
        {
            var found = TryFindInDirectory(customDir, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public async Task<ProcessCommandResult> RunCommandAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore cleanup failure.
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new ProcessCommandResult(-1, string.Empty, $"명령 실행 시간 초과 ({timeout.TotalSeconds:0}초)");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new ProcessCommandResult(process.ExitCode, stdout, stderr);
    }

    public IManagedProcess StartLongRunning(
        string fileName,
        string arguments,
        string workingDirectory,
        Action<string> onOutputLine)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                onOutputLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                onOutputLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new ManagedSystemProcess(process);
    }

    private static string? TryFindInDirectory(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        if (Path.HasExtension(name))
        {
            var direct = Path.Combine(directory, name);
            return File.Exists(direct) ? direct : null;
        }

        foreach (var ext in WindowsExtensions)
        {
            var candidate = Path.Combine(directory, name + ext);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetFallbackDirectories(string name)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return name.ToLowerInvariant() switch
        {
            "uv" =>
            [
                Path.Combine(home, ".local", "bin"),
                Path.Combine(home, ".cargo", "bin"),
                Path.Combine(localAppData, "Programs", "uv")
            ],
            "docker" =>
            [
                @"C:\Program Files\Docker\Docker\resources\bin",
                @"C:\Program Files\Docker\Docker"
            ],
            "ollama" =>
            [
                Path.Combine(localAppData, "Programs", "Ollama"),
                @"C:\Program Files\Ollama"
            ],
            _ => []
        };
    }
}
