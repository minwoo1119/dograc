using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

/// <summary>
/// ViewModel for the System page displaying service component health, ownership, controls, and live logs.
/// </summary>
public sealed partial class SystemViewModel : PageViewModelBase
{
    private readonly IServiceManager _serviceManager;
    private readonly IStartupOrchestrator _startupOrchestrator;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string _operationSummary = "서비스 상태를 확인하고 제어할 수 있습니다.";

    public SystemViewModel(
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator)
    {
        _serviceManager = serviceManager;
        _startupOrchestrator = startupOrchestrator;

        Services = new ObservableCollection<ServiceComponentStatus>(_serviceManager.CurrentStatuses);
        Logs = new ObservableCollection<ServiceLogEntry>(_serviceManager.LogHistory);

        _serviceManager.StatusesChanged += (_, statuses) => RunOnUiThread(() =>
        {
            Services.Clear();
            foreach (var status in statuses)
            {
                Services.Add(status);
            }
        });

        _serviceManager.LogAdded += (_, entry) => RunOnUiThread(() =>
        {
            Logs.Add(entry);
            if (Logs.Count > 1000)
            {
                Logs.RemoveAt(0);
            }
        });
    }

    public override string PageTitle => "System";
    public override string PageDescription => "Docker, Ollama, FastAPI 백엔드 상태·소유권 구분 및 실시간 시스템 로그를 확인합니다.";

    public string ProjectRoot => _serviceManager.Options.ProjectRoot;

    public string ApiBaseUrl => _serviceManager.Options.ApiBaseUrl;

    public ObservableCollection<ServiceComponentStatus> Services { get; }

    public ObservableCollection<ServiceLogEntry> Logs { get; }

    public event EventHandler? SetupWizardRequested;

    [RelayCommand]
    private async Task RefreshStatusesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OperationSummary = "서비스 상태를 새로고침하는 중입니다...";
        try
        {
            await _serviceManager.RefreshStatusesAsync();
            OperationSummary = "서비스 상태 새로고침 완료.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OperationSummary = "필요한 로컬 서비스를 기동하는 중입니다...";
        try
        {
            await _startupOrchestrator.RunStartupAsync();
            OperationSummary = _startupOrchestrator.StatusBannerText;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopManagedAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OperationSummary = "앱이 시작한(Managed) 서비스를 중지하는 중입니다...";
        try
        {
            await _serviceManager.StopManagedServicesAsync();
            OperationSummary = "앱 관리 서비스 중지 완료 (외부 실행 서비스는 유지됨).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _serviceManager.ClearLogs();
        Logs.Clear();
    }

    [RelayCommand]
    private void OpenSetupWizard()
    {
        SetupWizardRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.InvokeAsync(action);
        }
    }
}
