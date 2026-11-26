using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

/// <summary>
/// ViewModel for the environment diagnostic & recovery Setup Wizard modal.
/// Displays install state, detected/required version, running/reachable status, path, failure reason, resolution, and official guide link.
/// </summary>
public sealed partial class SetupWizardViewModel : ObservableObject
{
    private readonly IStartupOrchestrator _startupOrchestrator;
    private readonly IServiceManager _serviceManager;

    [ObservableProperty]
    private string _projectRoot = string.Empty;

    [ObservableProperty]
    private string _summaryMessage = "실행 환경을 진단한 결과 일부 필수 구성 요소를 확인해야 합니다.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRetrying))]
    private bool _isRetrying;

    [ObservableProperty]
    private string? _actionNotice;

    public SetupWizardViewModel(
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator)
    {
        _serviceManager = serviceManager;
        _startupOrchestrator = startupOrchestrator;
    }

    public bool IsNotRetrying => !IsRetrying;

    public ObservableCollection<EnvironmentDiagnosticItem> DiagnosticItems { get; } = [];
    public ObservableCollection<EnvironmentDiagnosticItem> RequiredItems { get; } = [];
    public ObservableCollection<EnvironmentDiagnosticItem> OptionalItems { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartServices))]
    [NotifyPropertyChangedFor(nameof(StartButtonLabel))]
    [NotifyPropertyChangedFor(nameof(StartButtonTooltip))]
    [NotifyPropertyChangedFor(nameof(AppLaunchStatusLabel))]
    [NotifyPropertyChangedFor(nameof(RagServiceStatusLabel))]
    private bool _areRequiredItemsReady;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RagServiceStatusLabel))]
    private bool _isRagFullyReady;

    public bool CanStartServices => IsNotRetrying && AreRequiredItemsReady;

    public string AppLaunchStatusLabel => AreRequiredItemsReady
        ? "앱 · 백엔드 실행 가능"
        : "앱 실행 불가 (필수 항목 미구성)";

    public string RagServiceStatusLabel => !AreRequiredItemsReady
        ? "RAG 질의응답 불가 (백엔드 필수 구성 필요)"
        : IsRagFullyReady
            ? "RAG 질의응답 즉시 사용 가능"
            : "문서 관리 가능 · AI 답변 생성은 Ollama 실행 필요";

    public string StartButtonLabel => AreRequiredItemsReady
        ? (IsRagFullyReady ? "서비스 시작" : "백엔드만 시작")
        : "필수 항목 구성 필요";

    public string StartButtonTooltip => !AreRequiredItemsReady
        ? "필수 구성 요소가 아직 준비되지 않았습니다. 위 안내를 완료한 뒤 [다시 검사]를 눌러 주세요."
        : IsRagFullyReady
            ? "필수 구성 요소와 로컬 AI(Ollama)가 모두 준비되었습니다. 클릭하여 서비스를 시작합니다."
            : "앱과 백엔드 서버(문서 업로드·검색)는 바로 시작할 수 있습니다. AI 답변 생성을 위해 Ollama를 별도로 실행해 주세요.";

    partial void OnIsRetryingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartServices));
    }

    public event EventHandler? Completed;

    public void LoadSummary(EnvironmentCheckSummary summary)
    {
        ProjectRoot = summary.ProjectRoot;
        DiagnosticItems.Clear();

        if (summary.Diagnostics is { Count: > 0 })
        {
            foreach (var item in summary.Diagnostics)
            {
                DiagnosticItems.Add(item);
            }
        }
        else
        {
            // Fallback synthesis for summaries created without explicit diagnostics list
            DiagnosticItems.Add(new EnvironmentDiagnosticItem(
                "api_source", "dograc 백엔드 소스 (apps/api)", true, summary.HasApiDirectory,
                summary.HasApiDirectory ? "감지됨" : "없음", "apps/api",
                summary.HasApiDirectory, summary.HasApiDirectory, summary.ProjectRoot,
                summary.HasApiDirectory ? null : $"경로를 찾을 수 없습니다: {summary.ProjectRoot}",
                summary.HasApiDirectory ? "정상 구성됨" : "프로젝트 루트 경로를 확인해 주세요.",
                "https://github.com/minwoo1119/dograc#readme",
                summary.HasApiDirectory ? ServiceState.Ready : ServiceState.NotInstalled));

            DiagnosticItems.Add(new EnvironmentDiagnosticItem(
                "python_runtime", "Python / uv 백엔드 런타임", true, summary.HasBackendRuntime,
                summary.HasBackendRuntime ? "감지됨" : "미감지", "Python 3.12+ / uv",
                summary.HasBackendRuntime, summary.HasBackendRuntime, "apps/api/.venv",
                summary.HasBackendRuntime ? null : "apps/api/.venv 또는 uv / python 실행 파일이 없습니다.",
                summary.HasBackendRuntime ? "정상 구성됨" : "uv 설치 후 apps/api에서 'uv sync'를 실행해 주세요.",
                "https://docs.astral.sh/uv/getting-started/installation/",
                summary.HasBackendRuntime ? ServiceState.Ready : ServiceState.NotInstalled));

            DiagnosticItems.Add(new EnvironmentDiagnosticItem(
                "docker", "Docker Desktop (인프라 권장)", false, summary.HasDockerExecutable,
                summary.HasDockerExecutable ? "설치됨" : "미설치", "Docker Desktop 4.x+",
                summary.HasDockerDaemon, summary.HasDockerDaemon, summary.HasDockerExecutable ? "docker" : "미설치",
                summary.HasDockerDaemon ? null : (summary.HasDockerExecutable ? "Docker Desktop이 설치되어 있으나 실행 중이 아닙니다." : "Docker Desktop이 설치되어 있지 않습니다."),
                summary.HasDockerDaemon ? "정상 가동 중" : (summary.HasDockerExecutable ? "Docker Desktop 앱을 실행한 뒤 [다시 검사]를 눌러 주세요." : "Docker Desktop을 설치하거나 로컬 폴백 모드로 실행하세요."),
                "https://www.docker.com/products/docker-desktop/",
                !summary.HasDockerExecutable ? ServiceState.NotInstalled : (summary.HasDockerDaemon ? ServiceState.Ready : ServiceState.Stopped)));

            DiagnosticItems.Add(new EnvironmentDiagnosticItem(
                "ollama", "Ollama 로컬 LLM (권장)", false, summary.HasOllamaService,
                summary.HasOllamaService ? "감지됨" : "미감지", "Ollama 0.3+",
                summary.HasOllamaService, summary.HasOllamaService, "http://127.0.0.1:11434",
                summary.HasOllamaService ? null : "Ollama가 감지되지 않았습니다.",
                summary.HasOllamaService ? "정상 연결됨" : "ollama.com에서 설치 후 'ollama run qwen2.5:7b'를 실행해 주세요.",
                "https://ollama.com/download",
                summary.HasOllamaService ? ServiceState.Ready : ServiceState.NotInstalled));
        }

        RequiredItems.Clear();
        OptionalItems.Clear();
        foreach (var item in DiagnosticItems)
        {
            if (item.IsRequired)
            {
                RequiredItems.Add(item);
            }
            else
            {
                OptionalItems.Add(item);
            }
        }

        var missingRequired = RequiredItems.Count(d => !d.IsSatisfied);
        var optionalWarnings = OptionalItems.Count(d => !d.IsSatisfied);
        var ollamaReady = OptionalItems.Any(d =>
            string.Equals(d.Key, "ollama", StringComparison.OrdinalIgnoreCase) && d.IsSatisfied);

        AreRequiredItemsReady = missingRequired == 0;
        IsRagFullyReady = AreRequiredItemsReady && ollamaReady;

        SummaryMessage = missingRequired > 0
            ? $"필수 구성 요소 {missingRequired}건이 준비되지 않아 앱 백엔드를 시작할 수 없습니다. 아래 해결 방법을 완료한 뒤 [다시 검사]를 눌러 주세요."
            : !ollamaReady
                ? $"앱·백엔드 실행 필수 항목은 준비되었으나, Ollama가 미실행 상태입니다. 지금 시작하면 문서 관리는 가능하지만 AI 답변 생성은 Ollama 실행 후 사용할 수 있습니다."
                : optionalWarnings > 0
                    ? $"앱 실행 및 RAG 질의응답이 가능합니다 (Docker 미실행 시 로컬 영속 저장소 모드로 동작)."
                    : "앱 실행 및 전체 로컬 RAG 인프라·AI 모델이 모두 정상 준비되었습니다.";
    }

    [RelayCommand]
    private void OpenGuideUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            ActionNotice = $"공식 설치 가이드를 열었습니다: {url}";
        }
        catch (Exception ex)
        {
            ActionNotice = $"브라우저 열기 실패: {ex.Message} ({url})";
        }
    }

    [RelayCommand]
    private async Task RecheckOnlyAsync()
    {
        if (IsRetrying)
        {
            return;
        }

        IsRetrying = true;
        ActionNotice = "로컬 환경을 다시 진단하고 있습니다...";
        try
        {
            var latest = await _serviceManager.InspectEnvironmentAsync();
            LoadSummary(latest);
            ActionNotice = "환경 재진단이 완료되었습니다.";
        }
        catch (Exception ex)
        {
            ActionNotice = $"환경 진단 중 오류: {ex.Message}";
        }
        finally
        {
            IsRetrying = false;
        }
    }

    [RelayCommand]
    private async Task RecheckAndStartAsync()
    {
        if (IsRetrying)
        {
            return;
        }

        IsRetrying = true;
        ActionNotice = "환경을 재검사하고 서비스를 시작하는 중입니다...";
        try
        {
            var latest = await _serviceManager.InspectEnvironmentAsync();
            LoadSummary(latest);

            if (!latest.NeedsSetupWizard)
            {
                await _startupOrchestrator.RunStartupAsync();
                Completed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ActionNotice = "아직 준비되지 않은 필수 항목이 있습니다. 위 항목을 먼저 구성해 주세요.";
            }
        }
        catch (Exception ex)
        {
            ActionNotice = $"시작 중 오류: {ex.Message}";
        }
        finally
        {
            IsRetrying = false;
        }
    }
}
