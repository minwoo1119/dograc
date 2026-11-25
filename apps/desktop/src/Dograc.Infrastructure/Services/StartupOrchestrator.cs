using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Orchestrates non-blocking startup verification and auto-start after Main Shell appears.
/// </summary>
public sealed class StartupOrchestrator : IStartupOrchestrator
{
    private readonly IServiceManager _serviceManager;

    public StartupOrchestrator(IServiceManager serviceManager)
    {
        _serviceManager = serviceManager;
    }

    public StartupState State { get; private set; } = StartupState.Idle;

    public string StatusBannerText { get; private set; } = "로컬 환경을 확인하는 중입니다...";

    public string? FailureReason { get; private set; }

    public EnvironmentCheckSummary? LastEnvironmentCheck { get; private set; }

    public event EventHandler<StartupState>? StateChanged;

    public async Task RunStartupAsync(CancellationToken cancellationToken = default)
    {
        FailureReason = null;
        SetState(StartupState.Checking, "Docker, Ollama, FastAPI 상태를 점검하는 중입니다...");

        var env = await _serviceManager.InspectEnvironmentAsync(cancellationToken);
        LastEnvironmentCheck = env;

        await _serviceManager.RefreshStatusesAsync(cancellationToken);

        if (env.NeedsSetupWizard)
        {
            FailureReason = string.Join(" ", env.MissingRequirements);
            SetState(StartupState.NeedsSetup, "초기 로컬 환경 구성이 필요합니다. Setup Wizard를 확인해 주세요.");
            return;
        }

        if (_serviceManager.IsApiReady)
        {
            SetState(StartupState.Ready, "모든 필수 서비스가 준비되었습니다.");
            return;
        }

        SetState(StartupState.Starting, "필요한 로컬 서비스를 자동으로 시작하고 있습니다...");

        try
        {
            var started = await _serviceManager.StartAllAsync(cancellationToken);
            if (started || _serviceManager.IsApiReady)
            {
                SetState(StartupState.Ready, "로컬 AI 워크스페이스 준비 완료.");
            }
            else
            {
                FailureReason = "FastAPI 백엔드가 응답하지 않습니다. System 페이지의 실행 로그를 확인한 뒤 재시도해 주세요.";
                SetState(StartupState.Failed, FailureReason);
            }
        }
        catch (OperationCanceledException)
        {
            FailureReason = "서비스 시작 작업이 취소되었습니다.";
            SetState(StartupState.Failed, FailureReason);
        }
        catch (Exception ex)
        {
            FailureReason = $"서비스 시작 중 오류가 발생했습니다: {ex.Message}";
            SetState(StartupState.Failed, FailureReason);
        }
    }

    private void SetState(StartupState nextState, string bannerText)
    {
        State = nextState;
        StatusBannerText = bannerText;
        StateChanged?.Invoke(this, nextState);
    }
}
