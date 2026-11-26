using System.Diagnostics;
using System.IO;
using System.Windows;
using Dograc.Core.Errors;
using Dograc.Core.Navigation;
using Dograc.Core.Services;

namespace Dograc.App.Services;

/// <summary>
/// Executes concrete, verified user recovery actions from ErrorCards, Banners, Toasts, and Dialogs.
/// </summary>
public sealed class RecoveryActionService : IRecoveryActionService
{
    private readonly INavigationService _navigationService;
    private readonly IServiceManager _serviceManager;
    private readonly IStartupOrchestrator _startupOrchestrator;
    private readonly IDiagnosticService _diagnosticService;
    private readonly INotificationService _notificationService;
    private readonly IStructuredLogger _logger;

    public RecoveryActionService(
        INavigationService navigationService,
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator,
        IDiagnosticService diagnosticService,
        INotificationService notificationService,
        IStructuredLogger logger)
    {
        _navigationService = navigationService;
        _serviceManager = serviceManager;
        _startupOrchestrator = startupOrchestrator;
        _diagnosticService = diagnosticService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public event EventHandler? SetupWizardRequested;
    public event EventHandler<ApplicationError>? ErrorDetailDialogRequested;
    public event EventHandler<ApplicationError?>? ExportDiagnosticsDialogRequested;
    public event EventHandler? FileReselectRequested;
    public event EventHandler? ChatRestoreInputRequested;

    public async Task ExecuteAsync(
        RecoveryActionDescriptor action,
        ApplicationError? sourceError = null,
        Func<Task>? retryCallback = null)
    {
        _logger.LogInfo("Recovery", "System", action.Kind.ToString(),
            $"복구 동작 실행: {action.Label}", correlationId: sourceError?.CorrelationId);

        switch (action.Kind)
        {
            case RecoveryActionKind.Retry:
            case RecoveryActionKind.ReindexDocument:
                if (retryCallback is not null)
                {
                    await retryCallback();
                }
                else
                {
                    await _serviceManager.RefreshStatusesAsync();
                }
                break;

            case RecoveryActionKind.StartServices:
                await _startupOrchestrator.RunStartupAsync();
                break;

            case RecoveryActionKind.RecheckConnection:
                await _serviceManager.RefreshStatusesAsync();
                _notificationService.NotifyInfo("연결 점검 완료", "로컬 서비스 상태를 최신 정보로 갱신했습니다.");
                break;

            case RecoveryActionKind.OpenModelSettings:
                _navigationService.NavigateTo(NavigationPage.Models);
                break;

            case RecoveryActionKind.OpenSystemPage:
                _navigationService.NavigateTo(NavigationPage.System);
                break;

            case RecoveryActionKind.OpenDocumentsPage:
                _navigationService.NavigateTo(NavigationPage.Documents);
                break;

            case RecoveryActionKind.OpenUpdateSettings:
                _navigationService.NavigateTo(NavigationPage.Settings);
                break;

            case RecoveryActionKind.OpenSetupWizard:
                SetupWizardRequested?.Invoke(this, EventArgs.Empty);
                break;

            case RecoveryActionKind.LaunchDockerDesktop:
                LaunchDockerDesktopOrGuide();
                break;

            case RecoveryActionKind.LaunchOllama:
                LaunchOllamaOrGuide();
                break;

            case RecoveryActionKind.CopyModelCommand:
                CopyToClipboardSafe(action.Parameter ?? "ollama run qwen2.5:7b", "모델 설치 명령을 복사했습니다.");
                break;

            case RecoveryActionKind.ReselectFile:
                FileReselectRequested?.Invoke(this, EventArgs.Empty);
                break;

            case RecoveryActionKind.RestoreInput:
                ChatRestoreInputRequested?.Invoke(this, EventArgs.Empty);
                break;

            case RecoveryActionKind.ViewErrorDetails:
                if (sourceError is not null)
                {
                    ErrorDetailDialogRequested?.Invoke(this, sourceError);
                }
                break;

            case RecoveryActionKind.CopyDiagnostics:
                var report = _diagnosticService.BuildSanitizedReport(sourceError);
                CopyToClipboardSafe(report, "민감 정보가 마스킹된 진단 정보를 클립보드에 복사했습니다.");
                break;

            case RecoveryActionKind.ExportDiagnosticReport:
                ExportDiagnosticsDialogRequested?.Invoke(this, sourceError);
                break;

            case RecoveryActionKind.OpenInstallGuide:
                OpenExternalUrlSafe(action.Parameter ?? "https://github.com/minwoo1119/dograc#readme");
                break;

            case RecoveryActionKind.OpenReleasePage:
                OpenExternalUrlSafe(action.Parameter ?? "https://github.com/minwoo1119/dograc/releases/latest");
                break;
        }
    }

    private void LaunchDockerDesktopOrGuide()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "Docker Desktop.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Docker", "Docker Desktop.exe")
        };

        var exe = candidates.FirstOrDefault(File.Exists);
        if (exe is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                _notificationService.NotifyInfo("Docker Desktop 실행 요청", "Docker Desktop이 준비될 때까지 잠시 기다린 뒤 [새로고침]을 눌러 주세요.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Recovery", "Docker", "LaunchDockerDesktop", $"Docker 실행 실패: {ex.Message}", errorCode: "DKR-001");
            }
        }

        OpenExternalUrlSafe("https://www.docker.com/products/docker-desktop/");
    }

    private void LaunchOllamaOrGuide()
    {
        var localApp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Ollama", "ollama app.exe");

        if (File.Exists(localApp))
        {
            try
            {
                Process.Start(new ProcessStartInfo(localApp) { UseShellExecute = true });
                _notificationService.NotifyInfo("Ollama 실행 요청", "Ollama가 시작된 뒤 연결 테스트를 다시 진행해 주세요.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Recovery", "Ollama", "LaunchOllama", $"Ollama 실행 실패: {ex.Message}", errorCode: "LLM-001");
            }
        }

        OpenExternalUrlSafe("https://ollama.com/download");
    }

    private void CopyToClipboardSafe(string text, string successMessage)
    {
        try
        {
            Clipboard.SetText(text);
            _notificationService.NotifySuccess("복사 완료", successMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Recovery", "System", "CopyClipboard", $"클립보드 접근 실패: {ex.Message}", errorCode: "APP-999");
            _notificationService.NotifyInfo("클립보드 사용 중", "다른 프로그램이 클립보드를 점유 중입니다. 잠시 후 다시 시도해 주세요.");
        }
    }

    private void OpenExternalUrlSafe(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Recovery", "System", "OpenExternalUrl", $"브라우저 열기 실패 ({url}): {ex.Message}");
            _notificationService.NotifyInfo("브라우저 열기 실패", $"주소({url})를 직접 열어 주세요.");
        }
    }
}
