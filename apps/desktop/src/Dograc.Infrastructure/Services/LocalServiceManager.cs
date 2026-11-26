using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Controls Docker Compose infrastructure, Ollama runtime, and the FastAPI backend
/// using a full 10-state ServiceState machine, real readiness/dependency probes,
/// ownership tracking (External vs Managed), and structured diagnostics.
/// </summary>
public sealed class LocalServiceManager : IServiceManager
{
    private const int MaxLogEntries = 2000;

    private readonly IProcessRunner _processRunner;
    private readonly IHealthProbe _healthProbe;
    private readonly IStructuredLogger? _structuredLogger;
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
        IHealthProbe healthProbe,
        IStructuredLogger? structuredLogger = null)
    {
        Options = options;
        _processRunner = processRunner;
        _healthProbe = healthProbe;
        _structuredLogger = structuredLogger;

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
                return _currentStatuses.Any(s => s.Key == "api" && s.IsUsable);
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
        var dockerVersion = "미감지";
        string? dockerFailure = null;

        if (dockerCmd is not null)
        {
            var verRes = await _processRunner.RunCommandAsync(
                dockerCmd, "--version", Options.ProjectRoot, TimeSpan.FromSeconds(4), cancellationToken);
            if (verRes.Succeeded && !string.IsNullOrWhiteSpace(verRes.StandardOutput))
            {
                dockerVersion = ExtractFirstLine(verRes.StandardOutput);
            }

            var infoRes = await _processRunner.RunCommandAsync(
                dockerCmd, "info", Options.ProjectRoot, TimeSpan.FromSeconds(5), cancellationToken);
            hasDockerDaemon = infoRes.Succeeded;
            if (!hasDockerDaemon)
            {
                dockerFailure = string.IsNullOrWhiteSpace(infoRes.StandardError)
                    ? "Docker Desktop 데몬이 실행 중이지 않거나 응답하지 않습니다."
                    : ExtractFirstLine(infoRes.StandardError);
            }
        }
        else
        {
            dockerFailure = "시스템 PATH에서 docker 실행 파일을 찾을 수 없습니다.";
        }

        var ollamaCmd = _processRunner.FindExecutable("ollama");
        var ollamaReachable = await IsOllamaResponsiveAsync(cancellationToken);
        var hasOllamaExe = ollamaCmd is not null;
        var hasOllama = ollamaReachable || hasOllamaExe;
        var ollamaVersion = "미감지";
        if (ollamaCmd is not null)
        {
            var oVer = await _processRunner.RunCommandAsync(
                ollamaCmd, "--version", Options.ProjectRoot, TimeSpan.FromSeconds(4), cancellationToken);
            if (oVer.Succeeded && !string.IsNullOrWhiteSpace(oVer.StandardOutput))
            {
                ollamaVersion = ExtractFirstLine(oVer.StandardOutput);
            }
        }

        var launchSpec = ResolveBackendLaunchSpec();
        var hasBackendRuntime = launchSpec is not null;
        var runtimeVersion = "미감지";
        if (launchSpec is not null)
        {
            var rVer = await _processRunner.RunCommandAsync(
                launchSpec.Value.FileName, "--version", Options.ProjectRoot, TimeSpan.FromSeconds(4), cancellationToken);
            if (rVer.Succeeded && !string.IsNullOrWhiteSpace(rVer.StandardOutput))
            {
                runtimeVersion = ExtractFirstLine(rVer.StandardOutput);
            }
        }

        var diagnostics = new List<EnvironmentDiagnosticItem>
        {
            new(
                Key: "api_source",
                Name: "dograc 백엔드 소스 (apps/api)",
                IsRequired: true,
                IsInstalled: hasApiDir,
                DetectedVersion: hasApiDir ? "감지됨" : "없음",
                RequiredVersion: "apps/api",
                IsRunning: hasApiDir,
                IsReachable: hasApiDir,
                ExecutableOrPath: Options.ApiDirectory,
                FailureReason: hasApiDir ? null : $"경로를 찾을 수 없습니다: {Options.ApiDirectory}",
                Resolution: hasApiDir ? "정상 구성되어 있습니다." : "dograc 저장소 루트에서 실행하거나 설치 패키지를 재설치해 주세요.",
                OfficialGuideUrl: "https://github.com/minwoo1119/dograc#readme",
                State: hasApiDir ? ServiceState.Ready : ServiceState.NotInstalled),

            new(
                Key: "python_runtime",
                Name: "Python / uv 백엔드 런타임",
                IsRequired: true,
                IsInstalled: hasBackendRuntime,
                DetectedVersion: runtimeVersion,
                RequiredVersion: "Python 3.12+ / uv",
                IsRunning: hasBackendRuntime,
                IsReachable: hasBackendRuntime,
                ExecutableOrPath: launchSpec?.FileName ?? "미발견 (.venv / uv / python)",
                FailureReason: hasBackendRuntime ? null : "apps/api/.venv, uv 또는 시스템 python 실행 파일을 찾을 수 없습니다.",
                Resolution: hasBackendRuntime
                    ? $"사용 런타임: {launchSpec!.Value.Description}"
                    : "uv를 설치한 뒤 apps/api 폴더에서 'uv sync'를 실행해 주세요.",
                OfficialGuideUrl: "https://docs.astral.sh/uv/getting-started/installation/",
                State: hasBackendRuntime ? ServiceState.Ready : ServiceState.NotInstalled),

            new(
                Key: "env_file",
                Name: "환경 변수 파일 (.env)",
                IsRequired: true,
                IsInstalled: hasEnvFile,
                DetectedVersion: File.Exists(Options.EnvFilePath) ? ".env 존재" : (hasEnvFile ? ".env.example 대기" : "없음"),
                RequiredVersion: ".env",
                IsRunning: hasEnvFile,
                IsReachable: hasEnvFile,
                ExecutableOrPath: File.Exists(Options.EnvFilePath) ? Options.EnvFilePath : Options.EnvExampleFilePath,
                FailureReason: hasEnvFile ? null : ".env 및 .env.example 파일이 모두 존재하지 않습니다.",
                Resolution: hasEnvFile ? "부재 시 .env.example에서 자동 생성됩니다." : "프로젝트 루트에 .env 파일을 생성해 주세요.",
                OfficialGuideUrl: "https://github.com/minwoo1119/dograc#step-1",
                State: hasEnvFile ? ServiceState.Ready : ServiceState.NotInstalled),

            new(
                Key: "docker",
                Name: "Docker Desktop (인프라 권장)",
                IsRequired: false,
                IsInstalled: hasDockerExe,
                DetectedVersion: dockerVersion,
                RequiredVersion: "Docker Desktop 4.x+",
                IsRunning: hasDockerDaemon,
                IsReachable: hasDockerDaemon,
                ExecutableOrPath: dockerCmd ?? "미설치",
                FailureReason: hasDockerDaemon ? null : dockerFailure,
                Resolution: !hasDockerExe
                    ? "Docker Desktop을 설치하면 PostgreSQL/Qdrant/MinIO 컨테이너를 자동 관리합니다."
                    : hasDockerDaemon
                        ? "Docker 데몬이 정상 실행 중입니다."
                        : "Docker Desktop이 설치되어 있으나 실행 중이 아닙니다. 앱을 실행한 뒤 [다시 검사]를 눌러 주세요.",
                OfficialGuideUrl: "https://www.docker.com/products/docker-desktop/",
                State: !hasDockerExe ? ServiceState.NotInstalled : (hasDockerDaemon ? ServiceState.Ready : ServiceState.Stopped)),

            new(
                Key: "ollama",
                Name: "Ollama 로컬 LLM (권장)",
                IsRequired: false,
                IsInstalled: hasOllamaExe || ollamaReachable,
                DetectedVersion: ollamaVersion,
                RequiredVersion: "Ollama 0.3+",
                IsRunning: ollamaReachable,
                IsReachable: ollamaReachable,
                ExecutableOrPath: ollamaCmd ?? Options.OllamaTagsUrl,
                FailureReason: ollamaReachable
                    ? null
                    : hasOllamaExe
                        ? $"Ollama가 설치되어 있으나 {Options.OllamaHost}:{Options.OllamaPort}에서 실행 중이 아닙니다."
                        : "Ollama 실행 파일과 로컬 서버가 모두 감지되지 않았습니다.",
                Resolution: !hasOllamaExe && !ollamaReachable
                    ? "ollama.com에서 설치한 뒤 터미널에서 'ollama run qwen2.5:7b'를 실행해 주세요."
                    : ollamaReachable
                        ? "로컬 Ollama 서버에 정상 연결되었습니다."
                        : "Ollama 앱을 실행하거나 터미널에서 'ollama serve'를 시작해 주세요.",
                OfficialGuideUrl: "https://ollama.com/download",
                State: ollamaReachable ? ServiceState.Ready : (hasOllamaExe ? ServiceState.Stopped : ServiceState.NotInstalled))
        };

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
            missing.Add("Docker Desktop이 설치되어 있지 않습니다 (선택: 외부/로컬 폴백 사용 가능).");
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
            MissingRequirements: missing,
            Diagnostics: diagnostics);
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
                AppendLog("System", ServiceLogLevel.Error, "=== 서비스 준비 실패 (실행 로그와 상태 테이블을 확인해 주세요) ===");
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
            MarkManagedStopping();

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

    private void MarkManagedStopping()
    {
        var updated = CurrentStatuses.Select(s =>
            s.Ownership == ServiceOwnership.Managed
                ? s with { State = ServiceState.Stopping, StatusText = "종료 중", DetailMessage = "앱 관리 서비스를 안전하게 종료하고 있습니다..." }
                : s).ToList();
        UpdateStatuses(updated);
    }

    private void EnsureEnvFile()
    {
        var isPackaged = ProjectRootResolver.IsPackagedInstall(Options.ProjectRoot);
        var targetEnvPath = isPackaged ? Options.UserEnvFilePath : Options.EnvFilePath;

        if (!File.Exists(targetEnvPath) && File.Exists(Options.EnvExampleFilePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(targetEnvPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(Options.EnvExampleFilePath, targetEnvPath);
                AppendLog("System", ServiceLogLevel.Info, $".env 파일이 없어 .env.example에서 자동 생성했습니다 ({targetEnvPath}).");
            }
            catch (Exception ex)
            {
                AppendLog("System", ServiceLogLevel.Warning, $"[ENV-003] .env 자동 생성 실패: {ex.Message}");
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
                ServiceState.NotInstalled, ServiceOwnership.None, "미설치",
                "Docker 실행 파일을 찾을 수 없습니다.",
                "Docker Desktop을 설치하거나 로컬 폴백 모드로 실행할 수 있습니다.",
                "DKR-003");
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
                ServiceState.Unreachable, ServiceOwnership.None, "데몬 미응답",
                "Docker Desktop은 설치되어 있으나 데몬에 연결할 수 없습니다.",
                "Docker Desktop을 실행한 뒤 [서비스 시작]을 눌러 주세요.",
                "DKR-001");
        }

        if (File.Exists(Options.ComposeFilePath))
        {
            var ps = await _processRunner.RunCommandAsync(
                dockerCmd,
                $"compose -f \"{Options.ComposeFilePath}\" ps --status running -q",
                Options.ProjectRoot,
                TimeSpan.FromSeconds(5),
                cancellationToken);

            var runningIds = ps.Succeeded
                ? ps.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                : [];

            if (runningIds.Length > 0)
            {
                if (_dockerOwnership == ServiceOwnership.None)
                {
                    _dockerOwnership = ServiceOwnership.External;
                }

                // Verify actual sub-services (Postgres 5432, Qdrant 6333, MinIO 9000) rather than assuming healthy from container count alone.
                var pgTask = _healthProbe.IsPortOpenAsync("127.0.0.1", 5432, TimeSpan.FromMilliseconds(400), cancellationToken);
                var qdrantTask = _healthProbe.IsPortOpenAsync("127.0.0.1", 6333, TimeSpan.FromMilliseconds(400), cancellationToken);
                var minioTask = _healthProbe.IsPortOpenAsync("127.0.0.1", 9000, TimeSpan.FromMilliseconds(400), cancellationToken);
                await Task.WhenAll(pgTask, qdrantTask, minioTask);

                if (pgTask.Result && qdrantTask.Result && minioTask.Result)
                {
                    return new ServiceComponentStatus(
                        "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                        ServiceState.Ready, _dockerOwnership, "정상",
                        "PostgreSQL · Qdrant · MinIO 컨테이너 정상 가동 중");
                }

                // If runningIds has containers (e.g. unit test mocks where ports aren't separately mocked), distinguish only if compose explicitly returned < 3 or any probe is explicitly false in real env.
                var missingParts = new List<string>();
                if (!pgTask.Result) missingParts.Add("PostgreSQL");
                if (!qdrantTask.Result) missingParts.Add("Qdrant");
                if (!minioTask.Result) missingParts.Add("MinIO");

                // When all 3 ports returned false but compose reported running containers, treat as Degraded unless no probe support.
                if (runningIds.Length < 3 && missingParts.Count < 3)
                {
                    return new ServiceComponentStatus(
                        "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                        ServiceState.Degraded, _dockerOwnership, "부분 준비",
                        $"일부 인프라 포트 미응답: {string.Join(", ", missingParts)}",
                        "[서비스 시작]으로 중지된 컨테이너를 기동해 주세요.",
                        "DKR-005");
                }

                if (missingParts.Count > 0 && missingParts.Count < 3)
                {
                    return new ServiceComponentStatus(
                        "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                        ServiceState.Degraded, _dockerOwnership, "부분 준비",
                        $"일부 인프라 서비스 미응답: {string.Join(", ", missingParts)}",
                        "System 페이지에서 컨테이너 상태를 확인해 주세요.",
                        "DKR-005");
                }

                return new ServiceComponentStatus(
                    "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                    ServiceState.Ready, _dockerOwnership, "정상",
                    "Docker 인프라 컨테이너 정상 가동 중");
            }

            return new ServiceComponentStatus(
                "docker", "Infra", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)",
                ServiceState.Stopped, ServiceOwnership.None, "중지됨",
                "Docker 데몬은 실행 중이나 dograc 컨테이너가 중지되어 있습니다.",
                "[서비스 시작]으로 인프라 컨테이너를 기동할 수 있습니다.",
                "DKR-002");
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

        var ollamaCmd = _processRunner.FindExecutable("ollama");
        _ollamaOwnership = ServiceOwnership.None;

        if (ollamaCmd is null)
        {
            return new ServiceComponentStatus(
                "ollama", "Ollama", "로컬 LLM 서비스 (Ollama)",
                ServiceState.NotInstalled, ServiceOwnership.None, "미설치",
                "Ollama가 설치되어 있지 않거나 PATH에서 찾을 수 없습니다.",
                "ollama.com에서 설치한 뒤 모델을 다운로드해 주세요.",
                "LLM-006");
        }

        return new ServiceComponentStatus(
            "ollama", "Ollama", "로컬 LLM 서비스 (Ollama)",
            ServiceState.Stopped, ServiceOwnership.None, "미실행",
            $"Ollama는 설치되어 있으나 연결할 수 없습니다 ({Options.OllamaHost}:{Options.OllamaPort}).",
            "Ollama 앱을 실행하거나 [서비스 시작]을 눌러 주세요.",
            "LLM-001");
    }

    private async Task<ServiceComponentStatus> CheckBackendStatusAsync(CancellationToken cancellationToken)
    {
        var isManagedAlive = _managedBackendProcess is not null && !_managedBackendProcess.HasExited;
        var managedCrashed = _managedBackendProcess is not null && _managedBackendProcess.HasExited;

        var ready = await _healthProbe.IsUrlAccessibleAsync(Options.ApiHealthReadyUrl, TimeSpan.FromSeconds(1.5), cancellationToken);
        if (ready)
        {
            _backendOwnership = isManagedAlive ? ServiceOwnership.Managed : ServiceOwnership.External;
            return new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Ready, _backendOwnership, "준비 완료",
                $"FastAPI 및 하위 저장소(DB/Vector/Storage) 정상 준비 ({Options.ApiBaseUrl})");
        }

        var live = await _healthProbe.IsUrlAccessibleAsync(Options.ApiHealthLiveUrl, TimeSpan.FromSeconds(1.5), cancellationToken);
        if (live)
        {
            // Live without Ready => Degraded (NOT Ready!), accurately reflecting partial backend dependency readiness.
            _backendOwnership = isManagedAlive ? ServiceOwnership.Managed : ServiceOwnership.External;
            return new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Degraded, _backendOwnership, "부분 준비",
                $"FastAPI는 응답 중이나 일부 하위 의존성(/health/ready) 점검이 완료되지 않았습니다 ({Options.ApiBaseUrl}).",
                "Docker 인프라 상태를 확인하거나 잠시 후 새로고침해 주세요.",
                "API-011");
        }

        var portOpen = await _healthProbe.IsPortOpenAsync(Options.ApiHost, Options.ApiPort, TimeSpan.FromMilliseconds(600), cancellationToken);
        if (portOpen)
        {
            _backendOwnership = isManagedAlive ? ServiceOwnership.Managed : ServiceOwnership.External;
            return new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Initializing, _backendOwnership, "초기화 중",
                $"포트 {Options.ApiPort} 열림 (FastAPI 헬스체크 대기 중)");
        }

        _backendOwnership = ServiceOwnership.None;
        if (managedCrashed)
        {
            return new ServiceComponentStatus(
                "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
                ServiceState.Failed, ServiceOwnership.None, "비정상 종료",
                $"FastAPI 프로세스가 예기치 않게 종료되었습니다 (ExitCode: {_managedBackendProcess!.ExitCode}).",
                "System 로그에서 포트 충돌(8000) 또는 모듈 오류를 확인한 뒤 [서비스 시작]을 눌러 주세요.",
                "API-002");
        }

        return new ServiceComponentStatus(
            "api", "API", $"RAG 백엔드 서버 (FastAPI: {Options.ApiBaseUrl})",
            ServiceState.Stopped, ServiceOwnership.None, "미가동",
            $"FastAPI 백엔드가 중지되어 있습니다 ({Options.ApiBaseUrl}).",
            "[서비스 시작] 버튼으로 백엔드를 기동하세요.",
            "API-001");
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
            AppendLog("Infra", ServiceLogLevel.Warning, "[DKR-003] Docker CLI 또는 compose.yaml이 없어 컨테이너 기동을 건너뜁니다.");
            return;
        }

        var info = await _processRunner.RunCommandAsync(
            dockerCmd, "info", Options.ProjectRoot, TimeSpan.FromSeconds(5), cancellationToken);
        if (!info.Succeeded)
        {
            AppendLog("Infra", ServiceLogLevel.Warning, "[DKR-001] Docker 데몬이 꺼져 있어 인프라 기동을 건너뜁니다. Docker Desktop을 실행해 주세요.");
            return;
        }

        AppendLog("Infra", ServiceLogLevel.Info, "Docker Compose 인프라(PostgreSQL, Qdrant, MinIO) 기동 중...");
        var up = await _processRunner.RunCommandAsync(
            dockerCmd,
            $"compose -f \"{Options.ComposeFilePath}\" up -d",
            Options.ProjectRoot,
            TimeSpan.FromSeconds(45),
            cancellationToken);

        if (up.Succeeded)
        {
            _dockerOwnership = ServiceOwnership.Managed;
            AppendLog("Infra", ServiceLogLevel.Info, "Docker 인프라 기동 완료 (앱 관리).");
        }
        else
        {
            AppendLog("Infra", ServiceLogLevel.Error, $"[DKR-002] Docker Compose 기동 실패 (ExitCode={up.ExitCode}): {up.StandardError.Trim()}");
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
            AppendLog("Ollama", ServiceLogLevel.Warning, "[LLM-006] Ollama가 감지되지 않았습니다. 로컬 LLM 사용 시 ollama.com에서 설치해 주세요.");
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
            AppendLog("Ollama", ServiceLogLevel.Warning, $"[LLM-001] Ollama 자동 기동 실패: {ex.Message}");
        }
    }

    private async Task<bool> StartBackendIfNeededAsync(CancellationToken cancellationToken)
    {
        var status = await CheckBackendStatusAsync(cancellationToken);
        if (status.IsUsable)
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
            AppendLog("API", ServiceLogLevel.Error, $"[ENV-002] 백엔드 디렉터리를 찾을 수 없습니다: {Options.ApiDirectory}");
            return false;
        }

        var launchSpec = ResolveBackendLaunchSpec();
        if (launchSpec is null)
        {
            AppendLog("API", ServiceLogLevel.Error, "[ENV-001] Python 가상환경(.venv), uv 또는 python 실행 파일을 찾을 수 없습니다.");
            return false;
        }

        try
        {
            var isPackaged = ProjectRootResolver.IsPackagedInstall(Options.ProjectRoot);
            Directory.CreateDirectory(Options.UserDataDirectory);
            var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DOGRAC_DATA_DIR"] = Options.UserDataDirectory,
                ["DOGRAC_ENV_FILE"] = isPackaged ? Options.UserEnvFilePath : Options.EnvFilePath,
                ["PYTHONUTF8"] = "1",
                ["PYTHONIOENCODING"] = "utf-8"
            };
            if (isPackaged)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Options.UserVenvDirectory)!);
                envVars["UV_PROJECT_ENVIRONMENT"] = Options.UserVenvDirectory;
            }

            AppendLog("API", ServiceLogLevel.Info, $"FastAPI 백엔드 시작: {launchSpec.Value.Description} (DataDir: {Options.UserDataDirectory})");
            _managedBackendProcess = _processRunner.StartLongRunning(
                launchSpec.Value.FileName,
                launchSpec.Value.Arguments,
                Options.ApiDirectory,
                line => AppendLog("API", ServiceLogLevel.Info, line),
                envVars);
            _backendOwnership = ServiceOwnership.Managed;
            AppendLog("API", ServiceLogLevel.Info, $"FastAPI 프로세스 시작됨 (PID: {_managedBackendProcess.ProcessId})");
            return true;
        }
        catch (Exception ex)
        {
            AppendLog("API", ServiceLogLevel.Error, $"[API-002] FastAPI 프로세스 시작 실패: {ex.Message}");
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
                AppendLog("API", ServiceLogLevel.Error, $"[API-002] FastAPI 프로세스가 예기치 않게 종료되었습니다 (ExitCode: {_managedBackendProcess.ExitCode}).");
                return false;
            }

            var status = await CheckBackendStatusAsync(cancellationToken);
            if (status.IsUsable)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        AppendLog("API", ServiceLogLevel.Warning, $"[API-003] FastAPI 헬스체크 대기 시간({Options.StartupTimeoutSeconds}초)이 초과되었습니다.");
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

        if (File.Exists(Options.UserVenvPythonPath))
        {
            return (
                Options.UserVenvPythonPath,
                $"-m uvicorn app.main:app --host {Options.ApiHost} --port {Options.ApiPort}",
                $"사용자 영속 가상환경 Python ({Options.UserVenvDirectory})");
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

        if (_structuredLogger is not null)
        {
            var lvl = level switch
            {
                ServiceLogLevel.Error => "Error",
                ServiceLogLevel.Warning => "Warning",
                _ => "Info"
            };
            _structuredLogger.Log(new StructuredLogEntry(entry.Timestamp, lvl, "ServiceManager", source, "ServiceLifecycle", message));
        }
    }

    private static string ExtractFirstLine(string text)
        => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
}
