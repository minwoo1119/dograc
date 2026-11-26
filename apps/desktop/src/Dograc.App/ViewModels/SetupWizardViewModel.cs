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

        var missingRequired = DiagnosticItems.Count(d => d.IsRequired && !d.IsSatisfied);
        var optionalWarnings = DiagnosticItems.Count(d => !d.IsRequired && !d.IsSatisfied);

        SummaryMessage = missingRequired > 0
            ? $"필수 항목 {missingRequired}건의 구성이 필요합니다. 아래 해결 방법을 확인한 뒤 [다시 검사 및 시작]을 눌러 주세요."
            : optionalWarnings > 0
                ? $"필수 항목은 모두 준비되었습니다 (권장 항목 {optionalWarnings}건 미실행). 바로 시작하거나 권장 도구를 실행할 수 있습니다."
                : "모든 필수 및 권장 환경이 정상 준비되었습니다.";
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
