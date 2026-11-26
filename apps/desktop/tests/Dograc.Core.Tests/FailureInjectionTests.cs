using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Dograc.Infrastructure.Api;
using Dograc.Infrastructure.Diagnostics;
using Dograc.Infrastructure.Services;
using Dograc.Infrastructure.Settings;
using Xunit;

namespace Dograc.Core.Tests;

/// <summary>
/// Automated Failure Injection Test Suite verifying all 18 required failure scenarios,
/// 10-state ServiceState transitions, sensitive data masking, and recovery policies.
/// </summary>
public sealed class FailureInjectionTests
{
    private readonly ErrorMapper _mapper = new();

    // Scenario 1: Docker stopped when app starts
    [Fact]
    public async Task Scenario01_DockerStopped_ReportsUnreachableAndDistinguishesFromNotInstalled()
    {
        var tempRoot = CreateTempProjectRoot();
        try
        {
            var runner = new StubProcessRunner(
                executables: new Dictionary<string, string> { ["docker"] = @"C:\Docker\docker.exe", ["python"] = @"C:\Python\python.exe" },
                commandHandler: (_, args) => args == "info"
                    ? new ProcessCommandResult(1, string.Empty, "error during connect: docker daemon is not running")
                    : new ProcessCommandResult(0, "Docker version 27.1.0", string.Empty));
            var probe = new StubHealthProbe();
            using var mgr = new LocalServiceManager(new ServiceRuntimeOptions(tempRoot), runner, probe);

            var statuses = await mgr.RefreshStatusesAsync();
            var docker = statuses.Single(s => s.Key == "docker");
            var env = await mgr.InspectEnvironmentAsync();
            var dockerDiag = env.Diagnostics!.Single(d => d.Key == "docker");

            Assert.Equal(ServiceState.Unreachable, docker.State);
            Assert.Equal("DKR-001", docker.ErrorCode);
            Assert.True(dockerDiag.IsInstalled);
            Assert.False(dockerDiag.IsRunning);
            Assert.Equal("설치됨 · 미실행", dockerDiag.StatusBadge);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    // Scenario 2: Ollama not installed
    [Fact]
    public async Task Scenario02_OllamaNotInstalled_ReportsNotInstalledWithInstallGuide()
    {
        var tempRoot = CreateTempProjectRoot();
        try
        {
            var runner = new StubProcessRunner(executables: new Dictionary<string, string>());
            var probe = new StubHealthProbe();
            using var mgr = new LocalServiceManager(new ServiceRuntimeOptions(tempRoot), runner, probe);

            var statuses = await mgr.RefreshStatusesAsync();
            var ollama = statuses.Single(s => s.Key == "ollama");
            var env = await mgr.InspectEnvironmentAsync();
            var ollamaDiag = env.Diagnostics!.Single(d => d.Key == "ollama");

            Assert.Equal(ServiceState.NotInstalled, ollama.State);
            Assert.Equal("LLM-006", ollama.ErrorCode);
            Assert.False(ollamaDiag.IsInstalled);
            Assert.Equal("https://ollama.com/download", ollamaDiag.OfficialGuideUrl);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    // Scenario 3: Invalid Ollama Endpoint URL
    [Fact]
    public async Task Scenario03_InvalidOllamaEndpoint_ReturnsLlm004WithoutThrowing()
    {
        var svc = new OllamaService(new HttpClient(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await svc.CheckAndListModelsAsync("not-a-valid-endpoint");

        Assert.False(result.IsSuccess);
        Assert.Contains("LLM-004", result.Message);

        var mapped = _mapper.Map(new UriFormatException("Invalid URI"), new ErrorContext("Models", "Ollama", "Connect"));
        Assert.Equal("LLM-004", mapped.ErrorCode);
        Assert.True(mapped.IsConfirmedCause);
    }

    // Scenario 4: Non-existent AI model selected
    [Fact]
    public void Scenario04_MissingModelSelected_MapsToLlm002WithModelSettingsAction()
    {
        var apiEx = new ApiException(503, "model 'nonexistent:99b' not found", "GENERATION_FAILED", pipelineStage: "generation");

        var err = _mapper.Map(apiEx, new ErrorContext("Chat", "Ollama", "답변 생성"));

        Assert.Equal("LLM-002", err.ErrorCode);
        Assert.True(err.IsConfirmedCause);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.OpenModelSettings);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.RestoreInput);
    }

    // Scenario 5: Backend port conflict / abnormal exit
    [Fact]
    public async Task Scenario05_BackendPortConflictOrCrashOnStart_ReportsFailedWithApi002()
    {
        var tempRoot = CreateTempProjectRoot();
        try
        {
            var crashedProcess = new StubManagedProcess(pid: 9999, hasExited: true, exitCode: 1);
            var runner = new StubProcessRunner(
                executables: new Dictionary<string, string> { ["python"] = @"C:\Python\python.exe" },
                longRunningFactory: (_, _) => crashedProcess);
            var probe = new StubHealthProbe();
            using var mgr = new LocalServiceManager(new ServiceRuntimeOptions(tempRoot, StartupTimeoutSeconds: 2), runner, probe);

            var started = await mgr.StartAllAsync();
            var api = mgr.CurrentStatuses.Single(s => s.Key == "api");

            Assert.False(started);
            Assert.Equal(ServiceState.Failed, api.State);
            Assert.Equal("API-002", api.ErrorCode);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    // Scenario 6: Backend force-killed mid-run (connection refused)
    [Fact]
    public void Scenario06_BackendForceKilled_MapsHttpRequestExceptionToApi001WithStartServices()
    {
        var err = _mapper.Map(
            new HttpRequestException("Connection refused"),
            new ErrorContext("Workspaces", "API", "워크스페이스 조회"));

        Assert.Equal("API-001", err.ErrorCode);
        Assert.True(err.IsRetryable);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.StartServices);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.Retry);
    }

    // Scenario 7: Qdrant connection dropped during document indexing
    [Fact]
    public void Scenario07_QdrantDropDuringIndexing_MapsToIdx004WithVectorIndexingStage()
    {
        var apiEx = new ApiException(
            500,
            "벡터 데이터베이스(Qdrant) 색인(Vector Indexing) 단계에서 오류가 발생했습니다.",
            "DOCUMENT_PROCESSING_FAILED",
            pipelineStage: "vector_indexing");

        var err = _mapper.Map(apiEx, new ErrorContext("Documents", "API", "문서 색인"));

        Assert.Equal("IDX-004", err.ErrorCode);
        Assert.Equal("Vector Indexing", err.PipelineStage);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.StartServices);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.ReindexDocument);
    }

    // Scenario 8: Corrupt PDF upload
    [Fact]
    public void Scenario08_CorruptPdfUpload_MapsToIdx001WithTextExtractionStage()
    {
        var apiEx = new ApiException(
            422,
            "문서에서 텍스트를 추출하지 못했습니다. 손상된 파일인지 확인해 주세요.",
            "DOCUMENT_PARSE_FAILED",
            pipelineStage: "text_extraction");

        var err = _mapper.Map(apiEx, new ErrorContext("Documents", "API", "문서 색인"));

        Assert.Equal("IDX-001", err.ErrorCode);
        Assert.Equal("텍스트 추출", err.PipelineStage);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.ReselectFile);
    }

    // Scenario 9: Encrypted PDF upload
    [Fact]
    public void Scenario09_EncryptedPdfUpload_MapsToDoc004ConfirmedCause()
    {
        var apiEx = new ApiException(
            422,
            "암호로 보호된 PDF는 텍스트를 추출할 수 없습니다.",
            "DOCUMENT_PARSE_FAILED",
            pipelineStage: "text_extraction",
            rawDetails: "{\"stage_code\":\"DOCUMENT_ENCRYPTED_PDF\"}");

        var err = _mapper.Map(apiEx, new ErrorContext("Documents", "API", "문서 색인"));

        Assert.Equal("DOC-004", err.ErrorCode);
        Assert.True(err.IsConfirmedCause);
        Assert.Equal("텍스트 추출", err.PipelineStage);
        Assert.False(err.IsRetryable);
    }

    // Scenario 10: Connection lost during file upload
    [Fact]
    public void Scenario10_UploadDisconnect_MapsToDoc006WithUploadStage()
    {
        var err = _mapper.Map(
            new HttpRequestException("The response ended prematurely"),
            new ErrorContext("Documents", "API", "UploadDocument"));

        Assert.Equal("DOC-006", err.ErrorCode);
        Assert.Equal("upload", err.PipelineStage);
        Assert.True(err.IsRetryable);
    }

    // Scenario 11: Ollama shuts down mid-generation
    [Fact]
    public void Scenario11_OllamaShutdownMidChat_MapsToChat001WithRestoreInput()
    {
        var apiEx = new ApiException(503, "Connection reset by peer", "GENERATION_FAILED", pipelineStage: "generation");

        var err = _mapper.Map(apiEx, new ErrorContext("Chat", "Ollama", "답변 생성"));

        Assert.Equal("CHAT-001", err.ErrorCode);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.Retry);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.RestoreInput);
    }

    // Scenario 12: API Timeout reports waited duration
    [Fact]
    public void Scenario12_ApiTimeout_ReportsTimeoutCodeAndWaitedDuration()
    {
        var ctx = new ErrorContext(
            "Chat", "API", "답변 생성",
            Metadata: new Dictionary<string, string> { ["timeout_seconds"] = "120" });
        var timeoutEx = new TaskCanceledException("HttpClient.Timeout of 120 seconds elapsing", new TimeoutException());

        var err = _mapper.Map(timeoutEx, ctx);

        Assert.Equal("API-004", err.ErrorCode);
        Assert.Contains("120초", err.Description);
        Assert.True(err.IsRetryable);
    }

    // Scenario 13: Malformed API JSON response
    [Fact]
    public async Task Scenario13_MalformedApiResponse_ThrowsApiExceptionOrMapsToApi005()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Bad Gateway</html>", Encoding.UTF8, "text/html")
        });
        var client = new DogracApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:8000/api/v1/") });

        var ex = await Assert.ThrowsAsync<JsonException>(() => client.GetWorkspacesAsync());
        var mapped = _mapper.Map(ex, new ErrorContext("Workspaces", "API", "워크스페이스 목록 조회"));

        Assert.Equal("API-005", mapped.ErrorCode);
    }

    // Scenario 14: Offline update check keeps current version intact
    [Fact]
    public async Task Scenario14_OfflineUpdateCheck_ReturnsUpd001WithoutCrashing()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("No network"));
        var updater = new GitHubUpdateService(new HttpClient(handler), "0.2.0", enableVelopack: false);

        var result = await updater.CheckForUpdatesAsync();

        Assert.False(result.HasUpdate);
        Assert.Contains("UPD-001", result.Message);
        Assert.Contains("v0.2.0", result.Message);
    }

    // Scenario 15: File write permission denied
    [Fact]
    public void Scenario15_WritePermissionDenied_MapsToEnv006()
    {
        var err = _mapper.Map(
            new UnauthorizedAccessException("Access to path denied"),
            new ErrorContext("Settings", "System", "설정 저장"));

        Assert.Equal("ENV-006", err.ErrorCode);
        Assert.Contains(err.RecoveryActions, a => a.Kind == RecoveryActionKind.Retry);
    }

    // Scenario 16: Corrupt settings JSON is backed up and safely recovered
    [Fact]
    public void Scenario16_CorruptSettingsFile_BacksUpCorruptFileAndRaisesSet001()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dograc-set-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(settingsPath, "{ broken json !!!");

            var store = new JsonSettingsStore(settingsPath);

            Assert.NotNull(store.LastStorageError);
            Assert.Equal("SET-001", store.LastStorageError!.ErrorCode);
            Assert.Equal("qwen2.5:7b", store.Current.SelectedModelName);
            var backups = Directory.GetFiles(tempDir, "settings.json.corrupt_json.*.bak");
            Assert.Single(backups);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // Scenario 17: User cancellation is distinguished from failure/timeout
    [Fact]
    public void Scenario17_UserCancellation_MapsToInformationChat002NotTimeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelEx = new OperationCanceledException(cts.Token);

        var err = _mapper.Map(cancelEx, new ErrorContext("Chat", "Ollama", "답변 생성"));

        Assert.Equal("CHAT-002", err.ErrorCode);
        Assert.Equal(ErrorSeverity.Information, err.Severity);
        Assert.True(err.IsConfirmedCause);
    }

    // Scenario 18: Bounded RetryPolicy recovers after transient error
    [Fact]
    public async Task Scenario18_RecoveryAndRetry_SucceedsAfterTransientFailure()
    {
        var attempts = 0;
        var value = await RetryPolicy.ExecuteAsync(
            ct =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new HttpRequestException("Transient reset");
                }
                return Task.FromResult("recovered-ok");
            },
            maxRetries: 2,
            baseDelay: TimeSpan.FromMilliseconds(10),
            shouldRetry: ex => ex is HttpRequestException);

        Assert.Equal("recovered-ok", value);
        Assert.Equal(2, attempts);
    }

    // Extra: Backend live without ready => Degraded (NOT Ready!)
    [Fact]
    public async Task BackendLiveWithoutReady_ReportsDegradedNeverFalseReady()
    {
        var tempRoot = CreateTempProjectRoot();
        try
        {
            var runner = new StubProcessRunner(executables: new Dictionary<string, string>());
            var probe = new StubHealthProbe(accessibleUrls: new HashSet<string> { "http://127.0.0.1:8000/api/v1/health/live" });
            using var mgr = new LocalServiceManager(new ServiceRuntimeOptions(tempRoot), runner, probe);

            var statuses = await mgr.RefreshStatusesAsync();
            var api = statuses.Single(s => s.Key == "api");

            Assert.Equal(ServiceState.Degraded, api.State);
            Assert.Equal("API-011", api.ErrorCode);
            Assert.False(api.IsReady);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    // Extra: Sensitive data sanitizer masks tokens, keys, emails, and prompt blocks
    [Fact]
    public void SensitiveDataSanitizer_MasksSecretsAndPromptContent()
    {
        var raw = "Authorization: Bearer eyJhbGciOi12345 api_key=sk-1234567890abcdef1234 user=admin@corp.com [문서 문맥] 기밀 원문 내용";

        var sanitized = SensitiveDataSanitizer.Sanitize(raw);

        Assert.DoesNotContain("eyJhbGciOi12345", sanitized);
        Assert.DoesNotContain("sk-1234567890abcdef1234", sanitized);
        Assert.DoesNotContain("admin@corp.com", sanitized);
        Assert.DoesNotContain("기밀 원문 내용", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }

    private static string CreateTempProjectRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dograc-fi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "apps", "api"));
        Directory.CreateDirectory(Path.Combine(root, "infra"));
        File.WriteAllText(Path.Combine(root, ".env"), "APP_NAME=dograc");
        File.WriteAllText(Path.Combine(root, "infra", "compose.yaml"), "services: {}");
        return root;
    }

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private sealed class StubHealthProbe(HashSet<string>? accessibleUrls = null) : IHealthProbe
    {
        public Task<bool> IsUrlAccessibleAsync(string url, TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(accessibleUrls?.Contains(url) == true);

        public Task<bool> IsPortOpenAsync(string host, int port, TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class StubProcessRunner(
        Dictionary<string, string> executables,
        Func<string, string, ProcessCommandResult>? commandHandler = null,
        Func<string, string, IManagedProcess>? longRunningFactory = null) : IProcessRunner
    {
        public string? FindExecutable(string commandName)
            => executables.TryGetValue(commandName, out var path) ? path : null;

        public Task<ProcessCommandResult> RunCommandAsync(string fileName, string arguments, string workingDirectory, TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(commandHandler?.Invoke(fileName, arguments) ?? new ProcessCommandResult(0, string.Empty, string.Empty));

        public IManagedProcess StartLongRunning(string fileName, string arguments, string workingDirectory, Action<string> onOutputLine)
            => longRunningFactory?.Invoke(fileName, arguments) ?? new StubManagedProcess(1234, false, null);
    }

    private sealed class StubManagedProcess(int pid, bool hasExited, int? exitCode) : IManagedProcess
    {
        public int ProcessId => pid;
        public bool HasExited => hasExited;
        public int? ExitCode => exitCode;
        public void KillTree() { }
        public void Dispose() { }
    }
}
