using Dograc.Core.Models;
using Dograc.Core.Services;
using Dograc.Infrastructure.Services;
using Xunit;

namespace Dograc.Core.Tests;

public sealed class ServiceManagerTests
{
    [Fact]
    public async Task ExternalServices_AreDetectedAsExternal_AndNotStoppedOnManagedShutdown()
    {
        var tempRoot = CreateTempProjectRoot(withApiDir: true, withCompose: true);
        try
        {
            var runner = new FakeProcessRunner
            {
                Executables = { ["docker"] = "docker.exe", ["uv"] = "uv.exe" },
                CommandHandler = (_, args) =>
                {
                    if (args == "info") return new ProcessCommandResult(0, "ok", "");
                    if (args.Contains("ps --status running")) return new ProcessCommandResult(0, "container123", "");
                    return new ProcessCommandResult(0, "", "");
                }
            };
            var probe = new FakeHealthProbe
            {
                AccessibleUrls =
                {
                    "http://127.0.0.1:11434/api/tags",
                    "http://127.0.0.1:8000/api/v1/health/ready"
                }
            };

            var options = new ServiceRuntimeOptions(tempRoot, StartupTimeoutSeconds: 2);
            using var manager = new LocalServiceManager(options, runner, probe);

            var statuses = await manager.RefreshStatusesAsync();

            Assert.All(statuses, s => Assert.Equal(ServiceState.Ready, s.State));
            Assert.All(statuses, s => Assert.Equal(ServiceOwnership.External, s.Ownership));
            Assert.True(manager.IsApiReady);

            await manager.StopManagedServicesAsync();

            Assert.DoesNotContain(runner.ExecutedCommands, cmd => cmd.Contains("compose") && cmd.Contains("down"));
            Assert.Empty(runner.StartedProcesses);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ManagedBackend_StartsStreamsLogs_AndStopsProcessTreeOnShutdown()
    {
        var tempRoot = CreateTempProjectRoot(withApiDir: true, withCompose: false);
        try
        {
            var probe = new FakeHealthProbe();
            var runner = new FakeProcessRunner
            {
                Executables = { ["uv"] = "uv.exe" },
                OnStartLongRunning = (_, args, onOutput) =>
                {
                    onOutput("Uvicorn running on http://127.0.0.1:8000");
                    probe.AccessibleUrls.Add("http://127.0.0.1:8000/api/v1/health/live");
                }
            };

            var options = new ServiceRuntimeOptions(tempRoot, StartupTimeoutSeconds: 3);
            using var manager = new LocalServiceManager(options, runner, probe);

            var started = await manager.StartAllAsync();

            Assert.True(started);
            Assert.True(manager.IsApiReady);
            var apiStatus = manager.CurrentStatuses.Single(s => s.Key == "api");
            Assert.Equal(ServiceOwnership.Managed, apiStatus.Ownership);
            Assert.Contains(manager.LogHistory, l => l.Message.Contains("Uvicorn running"));

            var managedProc = Assert.Single(runner.StartedProcesses);
            Assert.False(managedProc.KillTreeCalled);

            await manager.StopManagedServicesAsync();

            Assert.True(managedProc.KillTreeCalled);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task StartupOrchestrator_TransitionsToNeedsSetup_WhenApiDirectoryMissing()
    {
        var tempRoot = CreateTempProjectRoot(withApiDir: false, withCompose: false);
        try
        {
            var runner = new FakeProcessRunner();
            var probe = new FakeHealthProbe();
            var options = new ServiceRuntimeOptions(tempRoot, StartupTimeoutSeconds: 1);

            using var manager = new LocalServiceManager(options, runner, probe);
            var orchestrator = new StartupOrchestrator(manager);
            var observedStates = new List<StartupState>();
            orchestrator.StateChanged += (_, state) => observedStates.Add(state);

            await orchestrator.RunStartupAsync();

            Assert.Equal(StartupState.NeedsSetup, orchestrator.State);
            Assert.NotNull(orchestrator.LastEnvironmentCheck);
            Assert.True(orchestrator.LastEnvironmentCheck!.NeedsSetupWizard);
            Assert.Contains(StartupState.Checking, observedStates);
            Assert.Contains(StartupState.NeedsSetup, observedStates);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task StartupOrchestrator_TransitionsToReadyImmediately_WhenApiAlreadyHealthy()
    {
        var tempRoot = CreateTempProjectRoot(withApiDir: true, withCompose: false);
        try
        {
            var runner = new FakeProcessRunner { Executables = { ["uv"] = "uv.exe" } };
            var probe = new FakeHealthProbe
            {
                AccessibleUrls = { "http://127.0.0.1:8000/api/v1/health/ready" }
            };
            var options = new ServiceRuntimeOptions(tempRoot, StartupTimeoutSeconds: 1);

            using var manager = new LocalServiceManager(options, runner, probe);
            var orchestrator = new StartupOrchestrator(manager);

            await orchestrator.RunStartupAsync();

            Assert.Equal(StartupState.Ready, orchestrator.State);
            Assert.Empty(runner.StartedProcesses);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempProjectRoot(bool withApiDir, bool withCompose)
    {
        var root = Path.Combine(Path.GetTempPath(), $"dograc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        if (withApiDir)
        {
            Directory.CreateDirectory(Path.Combine(root, "apps", "api"));
        }
        if (withCompose)
        {
            var infra = Path.Combine(root, "infra");
            Directory.CreateDirectory(infra);
            File.WriteAllText(Path.Combine(infra, "compose.yaml"), "services: {}");
        }

        return root;
    }

    private sealed class FakeHealthProbe : IHealthProbe
    {
        public HashSet<string> AccessibleUrls { get; } = [];
        public HashSet<int> OpenPorts { get; } = [];

        public Task<bool> IsUrlAccessibleAsync(string url, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult(AccessibleUrls.Contains(url));

        public Task<bool> IsPortOpenAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult(OpenPorts.Contains(port));
    }

    private sealed class FakeManagedProcess : IManagedProcess
    {
        public int ProcessId => 4242;
        public bool HasExited { get; set; }
        public int? ExitCode => HasExited ? 0 : null;
        public bool KillTreeCalled { get; private set; }

        public void KillTree()
        {
            KillTreeCalled = true;
            HasExited = true;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Dictionary<string, string> Executables { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ExecutedCommands { get; } = [];
        public List<FakeManagedProcess> StartedProcesses { get; } = [];
        public Func<string, string, ProcessCommandResult>? CommandHandler { get; set; }
        public Action<string, string, Action<string>>? OnStartLongRunning { get; set; }

        public string? FindExecutable(string name)
            => Executables.TryGetValue(name, out var path) ? path : null;

        public Task<ProcessCommandResult> RunCommandAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ExecutedCommands.Add($"{fileName} {arguments}");
            var result = CommandHandler?.Invoke(fileName, arguments) ?? new ProcessCommandResult(0, "", "");
            return Task.FromResult(result);
        }

        public IManagedProcess StartLongRunning(
            string fileName,
            string arguments,
            string workingDirectory,
            Action<string> onOutputLine)
        {
            var proc = new FakeManagedProcess();
            StartedProcesses.Add(proc);
            OnStartLongRunning?.Invoke(fileName, arguments, onOutputLine);
            return proc;
        }
    }
}
