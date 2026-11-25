using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

public sealed record SetupCheckItem(string Title, bool IsSatisfied, string Detail);

/// <summary>
/// ViewModel for the first-time environment Setup Wizard modal.
/// </summary>
public sealed partial class SetupWizardViewModel : ObservableObject
{
    private readonly IStartupOrchestrator _startupOrchestrator;
    private readonly IServiceManager _serviceManager;

    [ObservableProperty]
    private string _projectRoot = string.Empty;

    [ObservableProperty]
    private string _summaryMessage = "최초 실행 환경을 점검한 결과 일부 필수 구성 요소가 필요합니다.";

    [ObservableProperty]
    private bool _isRetrying;

    public SetupWizardViewModel(
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator)
    {
        _serviceManager = serviceManager;
        _startupOrchestrator = startupOrchestrator;
    }

    public ObservableCollection<SetupCheckItem> CheckItems { get; } = [];

    public event EventHandler? Completed;

    public void LoadSummary(EnvironmentCheckSummary summary)
    {
        ProjectRoot = summary.ProjectRoot;
        CheckItems.Clear();

        CheckItems.Add(new SetupCheckItem(
            "dograc 백엔드 소스 (apps/api)",
            summary.HasApiDirectory,
            summary.HasApiDirectory ? "확인됨" : $"경로를 찾을 수 없습니다: {summary.ProjectRoot}"));

        CheckItems.Add(new SetupCheckItem(
            "Python / uv 백엔드 런타임",
            summary.HasBackendRuntime,
            summary.HasBackendRuntime ? "확인됨" : "apps/api/.venv 또는 uv / python 설치가 필요합니다."));

        CheckItems.Add(new SetupCheckItem(
            "환경 변수 파일 (.env / .env.example)",
            summary.HasEnvFile,
            summary.HasEnvFile ? "확인됨 (부재 시 자동 복사)" : ".env.example 파일이 없습니다."));

        CheckItems.Add(new SetupCheckItem(
            "Docker Desktop (권장)",
            summary.HasDockerDaemon,
            summary.HasDockerDaemon
                ? "Docker 데몬 가동 중"
                : summary.HasDockerExecutable
                    ? "Docker Desktop이 설치되어 있으나 실행 중이 아닙니다."
                    : "미설치 (PostgreSQL/Qdrant/MinIO 로컬 컨테이너 사용 시 필요)"));

        CheckItems.Add(new SetupCheckItem(
            "Ollama 로컬 LLM (권장)",
            summary.HasOllamaService,
            summary.HasOllamaService ? "감지됨" : "미감지 (ollama.com 설치 후 ollama run qwen2.5:7b 권장)"));

        SummaryMessage = summary.NeedsSetupWizard
            ? "필수 항목을 구성한 뒤 [다시 검사 및 시작]을 눌러 주세요."
            : "필수 항목이 모두 준비되었습니다. 서비스를 시작할 수 있습니다.";
    }

    [RelayCommand]
    private async Task RecheckAndStartAsync()
    {
        if (IsRetrying)
        {
            return;
        }

        IsRetrying = true;
        try
        {
            var latest = await _serviceManager.InspectEnvironmentAsync();
            LoadSummary(latest);

            if (!latest.NeedsSetupWizard)
            {
                await _startupOrchestrator.RunStartupAsync();
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            IsRetrying = false;
        }
    }
}
