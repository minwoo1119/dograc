using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Controls Docker Compose infrastructure, Ollama runtime, and the FastAPI backend
/// while strictly distinguishing externally started services from app-managed services.
/// </summary>
public sealed class LocalServiceManager : IServiceManager
{
    private const int MaxLogEntries = 2000;

    private readonly IProcessRunner _processRunner;
    private readonly IHealthProbe _healthProbe;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _stateLock = new();
    private readonly List<ServiceLogEntry> _logHistory = [];

    private IManagedProcess? _managedBackendProcess;
    private IManagedProcess? _managedOllamaProcess;
    private ServiceOwnership _dockerOwnership = ServiceOwnership.None;
    private ServiceOwnership _ollamaOwnership = ServiceOwnership.None;
    private ServiceOwnership _backendOwnership = ServiceOwnership.None;

    private List<ServiceComponentStatus> _currentStatuses;
    private bool _disposed;

    public LocalServiceManager(
        ServiceRuntimeOptions options,
        IProcessRunner processRunner,
        IHealthProbe healthProbe)
    {
        Options = options;
        _processRunner = processRunner;
        _healthProbe = healthProbe;

        _currentStatuses =
        [
            new ServiceComponentStatus(
                "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                ServiceState.Unknown, ServiceOwnership.None, "확인 전", "상태 점검 대기 중"),
            new ServiceComponentStatus(
                "ollama", "Ollama", "로컬 LLM 서비스 (Ollama)",
                ServiceState.Unknown, ServiceOwnership.None, "확인 전", "상태 점검 대기 중"),
            new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Unknown, ServiceOwnership.None, "확인 전", "상태 점검 대기 중")
        ];
    }

    public ServiceRuntimeOptions Options { get; }

    public IReadOnlyList<ServiceComponentStatus> CurrentStatuses
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStatuses.ToList();
            }
        }
    }

    public IReadOnlyList<ServiceLogEntry> LogHistory
    {
        get
        {
            lock (_stateLock)
            {
                return _logHistory.ToList();
            }
        }
    }

    public bool IsApiReady
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStatuses.Any(s => s.Key == "api" && s.IsReady);
            }
        }
    }

    public event EventHandler<IReadOnlyList<ServiceComponentStatus>>? StatusesChanged;

    public event EventHandler<ServiceLogEntry>? LogAdded;

    public async Task<EnvironmentCheckSummary> InspectEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        var hasApiDir = Directory.Exists(Options.ApiDirectory);
        var hasEnvFile = File.Exists(Options.EnvFilePath) || File.Exists(Options.EnvExampleFilePath);
        var hasComposeFile = File.Exists(Options.ComposeFilePath);

        var dockerCmd = _processRunner.FindExecutable("docker");
        var hasDockerExe = dockerCmd is not null;
        var hasDockerDaemon = false;

        if (dockerCmd is not null)
        {
            var infoRes = await _processRunner.RunCommandAsync(
                dockerCmd, "info", Options.ProjectRoot, TimeSpan.FromSeconds(5), cancellationToken);
            hasDockerDaemon = infoRes.Succeeded;
        }

        var hasOllama = await IsOllamaResponsiveAsync(cancellationToken) ||
                        _processRunner.FindExecutable("ollama") is not null;

        var hasBackendRuntime = ResolveBackendLaunchSpec() is not null;

        var missing = new List<string>();
        if (!hasApiDir)
        {
            missing.Add($"백엔드 소스 경로를 찾을 수 없습니다: {Options.ApiDirectory}");
        }
        if (!hasBackendRuntime)
        {
            missing.Add("Python 가상환경(.venv) 또는 uv / python 실행 파일을 찾을 수 없습니다.");
        }
        if (!hasDockerExe)
        {
            missing.Add("Docker Desktop이 설치되어 있지 않습니다 (선택: 외부 DB 사용 시 생략 가능).");
        }
        if (!hasOllama)
        {
            missing.Add("Ollama가 감지되지 않았습니다 (로컬 LLM 사용 시 설치 권장).");
        }

        return new EnvironmentCheckSummary(
            ProjectRoot: Options.ProjectRoot,
            HasApiDirectory: hasApiDir,
            HasEnvFile: hasEnvFile,
            HasComposeFile: hasComposeFile,
            HasDockerExecutable: hasDockerExe,
            HasDockerDaemon: hasDockerDaemon,
            HasOllamaService: hasOllama,
            HasBackendRuntime: hasBackendRuntime,
            MissingRequirements: missing);
    }

    public async Task<IReadOnlyList<ServiceComponentStatus>> RefreshStatusesAsync(CancellationToken cancellationToken = default)
    {
        var dockerTask = CheckDockerStatusAsync(cancellationToken);
        var ollamaTask = CheckOllamaStatusAsync(cancellationToken);
        var apiTask = CheckBackendStatusAsync(cancellationToken);

        await Task.WhenAll(dockerTask, ollamaTask, apiTask);

        var updated = new List<ServiceComponentStatus>
        {
            dockerTask.Result,
            ollamaTask.Result,
            apiTask.Result
        };

        UpdateStatuses(updated);
        return updated;
    }

    public async Task<bool> StartAllAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            AppendLog("System", ServiceLogLevel.Info, "=== 로컬 서비스 점검 및 자동 기동 시작 ===");

            EnsureEnvFile();
            await StartDockerIfNeededAsync(cancellationToken);
            await StartOllamaIfNeededAsync(cancellationToken);
            var backendStarted = await StartBackendIfNeededAsync(cancellationToken);

            var ready = backendStarted && await WaitForBackendReadyAsync(cancellationToken);
            await RefreshStatusesAsync(cancellationToken);

            if (ready)
            {
                AppendLog("System", ServiceLogLevel.Info, "=== 서비스 준비 완료 (FastAPI 정상 응답) ===");
            }
            else
            {
                AppendLog("System", ServiceLogLevel.Error, "=== 서비스 준비 실패 (실행 로그와 상태 카드를 확인해 주세요) ===");
            }

            return ready;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task StopManagedServicesAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            AppendLog("System", ServiceLogLevel.Info, "앱이 시작한(Managed) 서비스 정리 중...");

            if (_managedBackendProcess is not null)
            {
                AppendLog("API", ServiceLogLevel.Info, $"백엔드 프로세스(PID: {_managedBackendProcess.ProcessId}) 종료 중...");
                _managedBackendProcess.KillTree();
                _managedBackendProcess.Dispose();
                _managedBackendProcess = null;
                _backendOwnership = ServiceOwnership.None;
            }
            else if (_backendOwnership == ServiceOwnership.External)
            {
                AppendLog("API", ServiceLogLevel.Info, "외부에서 실행 중인 FastAPI 백엔드는 유지합니다.");
            }

            if (_managedOllamaProcess is not null)
            {
                AppendLog("Ollama", ServiceLogLevel.Info, $"앱이 시작한 Ollama 프로세스(PID: {_managedOllamaProcess.ProcessId}) 종료 중...");
                _managedOllamaProcess.KillTree();
                _managedOllamaProcess.Dispose();
                _managedOllamaProcess = null;
                _ollamaOwnership = ServiceOwnership.None;
            }
            else if (_ollamaOwnership == ServiceOwnership.External)
            {
                AppendLog("Ollama", ServiceLogLevel.Info, "외부에서 실행 중인 Ollama 서비스는 유지합니다.");
            }

            if (_dockerOwnership == ServiceOwnership.Managed)
            {
                var dockerCmd = _processRunner.FindExecutable("docker");
                if (dockerCmd is not null && File.Exists(Options.ComposeFilePath))
                {
                    AppendLog("Infra", ServiceLogLevel.Info, "앱이 시작한 Docker Compose 인프라 종료 중...");
                    await _processRunner.RunCommandAsync(
                        dockerCmd,
                        $"compose -f \"{Options.ComposeFilePath}\" down",
                        Options.ProjectRoot,
                        TimeSpan.FromSeconds(25),
                        cancellationToken);
                }

                _dockerOwnership = ServiceOwnership.None;
            }
            else if (_dockerOwnership == ServiceOwnership.External)
            {
                AppendLog("Infra", ServiceLogLevel.Info, "외부에서 실행 중인 Docker 컨테이너는 유지합니다.");
            }

            await RefreshStatusesAsync(cancellationToken);
            AppendLog("System", ServiceLogLevel.Info, "앱 관리 서비스 정리 완료.");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void ClearLogs()
    {
        lock (_stateLock)
        {
            _logHistory.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_managedBackendProcess is not null)
        {
            _managedBackendProcess.KillTree();
            _managedBackendProcess.Dispose();
            _managedBackendProcess = null;
        }

        if (_managedOllamaProcess is not null)
        {
            _managedOllamaProcess.KillTree();
            _managedOllamaProcess.Dispose();
            _managedOllamaProcess = null;
        }

        _operationGate.Dispose();
    }

    private void EnsureEnvFile()
    {
        if (!File.Exists(Options.EnvFilePath) && File.Exists(Options.EnvExampleFilePath))
        {
            try
            {
                File.Copy(Options.EnvExampleFilePath, Options.EnvFilePath);
                AppendLog("System", ServiceLogLevel.Info, ".env 파일이 없어 .env.example에서 자동 생성했습니다.");
            }
            catch (Exception ex)
            {
                AppendLog("System", ServiceLogLevel.Warning, $".env 자동 생성 실패: {ex.Message}");
            }
        }
    }

    private async Task<ServiceComponentStatus> CheckDockerStatusAsync(CancellationToken cancellationToken)
    {
        var dockerCmd = _processRunner.FindExecutable("docker");
        if (dockerCmd is null)
        {
            _dockerOwnership = ServiceOwnership.None;
            return new ServiceComponentStatus(
                "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                ServiceState.Stopped, ServiceOwnership.None, "미설치",
                "Docker 실행 파일을 찾을 수 없습니다.",
                "Docker Desktop을 설치하거나 실행해 주세요.");
        }

        var info = await _processRunner.RunCommandAsync(
            dockerCmd, "info", Options.ProjectRoot, TimeSpan.FromSeconds(5), cancellationToken);
        if (!info.Succeeded)
        {
            if (_dockerOwnership != ServiceOwnership.Managed)
            {
                _dockerOwnership = ServiceOwnership.None;
            }

            return new ServiceComponentStatus(
                "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                ServiceState.Stopped, ServiceOwnership.None, "미응답",
                "Docker 데몬이 응답하지 않습니다.",
                "Docker Desktop을 실행한 뒤 [재시도]를 눌러 주세요.");
        }

        if (File.Exists(Options.ComposeFilePath))
        {
            var ps = await _processRunner.RunCommandAsync(
                dockerCmd,
                $"compose -f \"{Options.ComposeFilePath}\" ps --status running -q",
                Options.ProjectRoot,
                TimeSpan.FromSeconds(5),
                cancellationToken);

            var hasRunningContainers = ps.Succeeded && !string.IsNullOrWhiteSpace(ps.StandardOutput);
            if (hasRunningContainers)
            {
                if (_dockerOwnership == ServiceOwnership.None)
                {
                    _dockerOwnership = ServiceOwnership.External;
                }

                return new ServiceComponentStatus(
                    "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                    ServiceState.Ready, _dockerOwnership, "정상",
                    "Docker 인프라 컨테이너 정상 가동 중");
            }

            return new ServiceComponentStatus(
                "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                ServiceState.Stopped, ServiceOwnership.None, "중지됨",
                "Docker 데몬은 켜져 있으나 dograc 컨테이너가 중지되어 있습니다.",
                "[전체 시작]으로 인프라 컨테이너를 기동할 수 있습니다.");
        }

        if (_dockerOwnership == ServiceOwnership.None)
        {
            _dockerOwnership = ServiceOwnership.External;
        }

        return new ServiceComponentStatus(
            "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
            ServiceState.Ready, _dockerOwnership, "정상",
            "Docker 데몬 정상 가동 중");
    }

    private async Task<ServiceComponentStatus> CheckOllamaStatusAsync(CancellationToken cancellationToken)
    {
        var responsive = await IsOllamaResponsiveAsync(cancellationToken);
        if (responsive)
        {
            var ownership = _managedOllamaProcess is not null && !_managedOllamaProcess.HasExited
                ? ServiceOwnership.Managed
                : ServiceOwnership.External;
            _ollamaOwnership = ownership;

            return new ServiceComponentStatus(
                "ollama", "Ollama", "로컬 LLM 서비스 (Ollama)",
                ServiceState.Ready, ownership, "연결됨",
                $"로컬 Ollama 연결됨 ({Options.OllamaHost}:{Options.OllamaPort})");
        }

        _ollamaOwnership = ServiceOwnership.None;
        return new ServiceComponentStatus(
            "ollama", "Ollama", "로컬 LLM 서비스 (Ollama)",
            ServiceState.Stopped, ServiceOwnership.None, "미응답",
            $"Ollama 서비스에 연결할 수 없습니다 ({Options.OllamaHost}:{Options.OllamaPort}).",
            "Ollama 앱을 실행하거나 터미널에서 'ollama serve'를 실행해 주세요.");
    }

    private async Task<ServiceComponentStatus> CheckBackendStatusAsync(CancellationToken cancellationToken)
    {
        var isManagedAlive = _managedBackendProcess is not null && !_managedBackendProcess.HasExited;

        var ready = await _healthProbe.IsUrlAccessibleAsync(Options.ApiHealthReadyUrl, TimeSpan.FromSeconds(1.5), cancellationToken);
        if (ready)
        {
            _backendOwnership = isManagedAlive ? ServiceOwnership.Managed : ServiceOwnership.External;
            return new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Ready, _backendOwnership, "준비 완료",
                $"FastAPI 및 하위 의존성 정상 준비 ({Options.ApiBaseUrl})");
        }

        var live = await _healthProbe.IsUrlAccessibleAsync(Options.ApiHealthLiveUrl, TimeSpan.FromSeconds(1.5), cancellationToken);
        if (live)
        {
            _backendOwnership = isManagedAlive ? ServiceOwnership.Managed : ServiceOwnership.External;
            return new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Ready, _backendOwnership, "가동 중",
                $"FastAPI 서버 응답 중 ({Options.ApiBaseUrl})");
        }

        var portOpen = await _healthProbe.IsPortOpenAsync(Options.ApiHost, Options.ApiPort, TimeSpan.FromMilliseconds(600), cancellationToken);
        if (portOpen)
        {
            _backendOwnership = isManagedAlive ? ServiceOwnership.Managed : ServiceOwnership.External;
            return new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Starting, _backendOwnership, "초기화 중",
                $"포트 {Options.ApiPort} 응답 중 (헬스체크 대기)");
        }

        _backendOwnership = ServiceOwnership.None;
        return new ServiceComponentStatus(
            "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
            ServiceState.Stopped, ServiceOwnership.None, "미가동",
            $"FastAPI 백엔드가 중지되어 있습니다 ({Options.ApiBaseUrl}).",
            "[전체 시작] 또는 [재시도] 버튼으로 백엔드를 기동하세요.");
    }

    private async Task StartDockerIfNeededAsync(CancellationToken cancellationToken)
    {
        var current = await CheckDockerStatusAsync(cancellationToken);
        if (current.IsReady)
        {
            AppendLog("Infra", ServiceLogLevel.Info, $"Docker 인프라가 이미 가동 중입니다 ({current.OwnershipLabel}).");
            return;
        }

        var dockerCmd = _processRunner.FindExecutable("docker");
        if (dockerCmd is null || !File.Exists(Options.ComposeFilePath))
        {
            AppendLog("Infra", ServiceLogLevel.Warning, "Docker CLI 또는 compose.yaml이 없어 컨테이너 기동을 건너뜁니다.");
            return;
        }

        var info = await _processRunner.RunCommandAsync(
            dockerCmd, "info", Options.ProjectRoot, TimeSpan.FromSeconds(5), cancellationToken);
        if (!info.Succeeded)
        {
            AppendLog("Infra", ServiceLogLevel.Warning, "Docker 데몬이 꺼져 있어 인프라 기동을 건너뜁니다. Docker Desktop을 실행해 주세요.");
            return;
        }

        AppendLog("Infra", ServiceLogLevel.Info, "Docker Compose 인프라(PostgreSQL, Qdrant, MinIO) 기동 중...");
        var up = await _processRunner.RunCommandAsync(
            dockerCmd,
            $"compose -f \"{Options.ComposeFilePath}\" up -d",
            Options.ProjectRoot,
            TimeSpan.FromSeconds(30),
            cancellationToken);

        if (up.Succeeded)
        {
            _dockerOwnership = ServiceOwnership.Managed;
            AppendLog("Infra", ServiceLogLevel.Info, "Docker 인프라 기동 완료 (앱 관리).");
        }
        else
        {
            AppendLog("Infra", ServiceLogLevel.Error, $"Docker Compose 기동 실패: {up.StandardError.Trim()}");
        }
    }

    private async Task StartOllamaIfNeededAsync(CancellationToken cancellationToken)
    {
        if (await IsOllamaResponsiveAsync(cancellationToken))
        {
            if (_ollamaOwnership == ServiceOwnership.None)
            {
                _ollamaOwnership = ServiceOwnership.External;
            }

            AppendLog("Ollama", ServiceLogLevel.Info, $"Ollama 서비스가 이미 실행 중입니다 ({_ollamaOwnership}).");
            return;
        }

        var ollamaCmd = _processRunner.FindExecutable("ollama");
        if (ollamaCmd is null)
        {
            AppendLog("Ollama", ServiceLogLevel.Warning, "Ollama가 감지되지 않았습니다. 로컬 LLM 사용 시 ollama.com에서 설치해 주세요.");
            return;
        }

        try
        {
            AppendLog("Ollama", ServiceLogLevel.Info, "로컬 Ollama 서비스(ollama serve) 자동 시작 중...");
            _managedOllamaProcess = _processRunner.StartLongRunning(
                ollamaCmd,
                "serve",
                Options.ProjectRoot,
                line => AppendLog("Ollama", ServiceLogLevel.Info, line));
            _ollamaOwnership = ServiceOwnership.Managed;
            AppendLog("Ollama", ServiceLogLevel.Info, $"Ollama 프로세스 시작됨 (PID: {_managedOllamaProcess.ProcessId})");
        }
        catch (Exception ex)
        {
            AppendLog("Ollama", ServiceLogLevel.Warning, $"Ollama 자동 기동 실패: {ex.Message}");
        }
    }

    private async Task<bool> StartBackendIfNeededAsync(CancellationToken cancellationToken)
    {
        var status = await CheckBackendStatusAsync(cancellationToken);
        if (status.IsReady)
        {
            AppendLog("API", ServiceLogLevel.Info, $"FastAPI 백엔드가 이미 실행 중입니다 ({status.OwnershipLabel}).");
            return true;
        }

        if (_managedBackendProcess is not null && !_managedBackendProcess.HasExited)
        {
            AppendLog("API", ServiceLogLevel.Info, "백엔드 프로세스가 이미 기동 중입니다.");
            return true;
        }

        if (!Directory.Exists(Options.ApiDirectory))
        {
            AppendLog("API", ServiceLogLevel.Error, $"백엔드 디렉터리를 찾을 수 없습니다: {Options.ApiDirectory}");
            return false;
        }

        var launchSpec = ResolveBackendLaunchSpec();
        if (launchSpec is null)
        {
            AppendLog("API", ServiceLogLevel.Error, "Python 가상환경(.venv), uv 또는 python 실행 파일을 찾을 수 없습니다.");
            return false;
        }

        try
        {
            AppendLog("API", ServiceLogLevel.Info, $"FastAPI 백엔드 시작: {launchSpec.Value.Description}");
            _managedBackendProcess = _processRunner.StartLongRunning(
                launchSpec.Value.FileName,
                launchSpec.Value.Arguments,
                Options.ApiDirectory,
                line => AppendLog("API", ServiceLogLevel.Info, line));
            _backendOwnership = ServiceOwnership.Managed;
            AppendLog("API", ServiceLogLevel.Info, $"FastAPI 프로세스 시작됨 (PID: {_managedBackendProcess.ProcessId})");
            return true;
        }
        catch (Exception ex)
        {
            AppendLog("API", ServiceLogLevel.Error, $"FastAPI 프로세스 시작 실패: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> WaitForBackendReadyAsync(CancellationToken cancellationToken)
    {
        AppendLog("API", ServiceLogLevel.Info, $"FastAPI 헬스체크 대기 중 (최대 {Options.StartupTimeoutSeconds}초)...");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Options.StartupTimeoutSeconds);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_managedBackendProcess is not null && _managedBackendProcess.HasExited)
            {
                AppendLog("API", ServiceLogLevel.Error, $"FastAPI 프로세스가 예기치 않게 종료되었습니다 (ExitCode: {_managedBackendProcess.ExitCode}).");
                return false;
            }

            var status = await CheckBackendStatusAsync(cancellationToken);
            if (status.IsReady)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        AppendLog("API", ServiceLogLevel.Warning, "FastAPI 헬스체크 대기 시간이 초과되었습니다.");
        return false;
    }

    private (string FileName, string Arguments, string Description)? ResolveBackendLaunchSpec()
    {
        var venvPython = Path.Combine(Options.ApiDirectory, ".venv", "Scripts", "python.exe");
        if (File.Exists(venvPython))
        {
            return (
                venvPython,
                $"-m uvicorn app.main:app --host {Options.ApiHost} --port {Options.ApiPort}",
                $"가상환경 Python ({Path.GetFileName(venvPython)})");
        }

        var uvCmd = _processRunner.FindExecutable("uv");
        if (uvCmd is not null)
        {
            return (
                uvCmd,
                $"run uvicorn app.main:app --host {Options.ApiHost} --port {Options.ApiPort}",
                "uv 패키지 매니저");
        }

        var pythonCmd = _processRunner.FindExecutable("python");
        if (pythonCmd is not null)
        {
            return (
                pythonCmd,
                $"-m uvicorn app.main:app --host {Options.ApiHost} --port {Options.ApiPort}",
                $"시스템 Python ({pythonCmd})");
        }

        return null;
    }

    private async Task<bool> IsOllamaResponsiveAsync(CancellationToken cancellationToken)
    {
        return await _healthProbe.IsUrlAccessibleAsync(Options.OllamaTagsUrl, TimeSpan.FromSeconds(1), cancellationToken)
               || await _healthProbe.IsPortOpenAsync(Options.OllamaHost, Options.OllamaPort, TimeSpan.FromMilliseconds(600), cancellationToken);
    }

    private void UpdateStatuses(List<ServiceComponentStatus> updated)
    {
        lock (_stateLock)
        {
            _currentStatuses = updated;
        }

        StatusesChanged?.Invoke(this, updated);
    }

    private void AppendLog(string source, ServiceLogLevel level, string message)
    {
        var entry = new ServiceLogEntry(DateTimeOffset.Now, source, level, message);
        lock (_stateLock)
        {
            _logHistory.Add(entry);
            if (_logHistory.Count > MaxLogEntries)
            {
                _logHistory.RemoveAt(0);
            }
        }

        LogAdded?.Invoke(this, entry);
    }
}
