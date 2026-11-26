using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Dograc.Core.Theme;

namespace Dograc.App.ViewModels;

/// <summary>
/// ViewModel for the Settings page covering persisted theme, shutdown options, runtime info, and one-click Velopack updates.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly ISettingsStore _settingsStore;
    private readonly IUpdateService _updateService;
    private readonly ServiceRuntimeOptions _runtimeOptions;

    [ObservableProperty]
    private AppThemeMode _selectedThemeMode;

    [ObservableProperty]
    private bool _stopManagedServicesOnExit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotCheckingUpdate))]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotInstallingUpdate))]
    private bool _isInstallingUpdate;

    [ObservableProperty]
    private int _downloadProgressPercent;

    [ObservableProperty]
    private string _updateStatusMessage = "최신 릴리스 여부를 확인할 수 있습니다.";

    [ObservableProperty]
    private bool _hasAvailableUpdate;

    [ObservableProperty]
    private bool _canAutoInstallUpdate;

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    [ObservableProperty]
    private string _releaseUrl = "https://github.com/minwoo1119/dograc/releases/latest";

    public SettingsViewModel(
        IThemeService themeService,
        ISettingsStore settingsStore,
        IUpdateService updateService,
        ServiceRuntimeOptions runtimeOptions)
    {
        _themeService = themeService;
        _settingsStore = settingsStore;
        _updateService = updateService;
        _runtimeOptions = runtimeOptions;

        var current = _settingsStore.Current;
        _selectedThemeMode = current.ThemeMode;
        _stopManagedServicesOnExit = current.StopManagedServicesOnExit;
    }

    public override string PageTitle => "Settings";
    public override string PageDescription => "화면 테마, 앱 종료 정책 및 버전별 자동 업데이트를 관리합니다.";

    public string CurrentVersionLabel => $"v{_updateService.CurrentVersion}";

    public string UpdateModeBadge => _updateService.IsAutoUpdateSupported
        ? "Velopack 자동 업데이트 지원"
        : "포터블 / 개발 모드 (릴리스 안내)";

    public string ApiBaseUrl => _runtimeOptions.ApiBaseUrl;

    public string ProjectRoot => _runtimeOptions.ProjectRoot;

    public bool IsNotCheckingUpdate => !IsCheckingUpdate;

    public bool IsNotInstallingUpdate => !IsInstallingUpdate;

    partial void OnSelectedThemeModeChanged(AppThemeMode value)
    {
        _themeService.ApplyTheme(value);
        if (_settingsStore.Current.ThemeMode != value)
        {
            _settingsStore.Save(_settingsStore.Current with { ThemeMode = value });
        }
    }

    partial void OnStopManagedServicesOnExitChanged(bool value)
    {
        if (_settingsStore.Current.StopManagedServicesOnExit != value)
        {
            _settingsStore.Save(_settingsStore.Current with { StopManagedServicesOnExit = value });
        }
    }

    [RelayCommand]
    private void SetTheme(AppThemeMode mode)
    {
        SelectedThemeMode = mode;
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdate || IsInstallingUpdate)
        {
            return;
        }

        IsCheckingUpdate = true;
        UpdateStatusMessage = "최신 릴리스 정보를 확인하는 중입니다...";
        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            HasAvailableUpdate = result.HasUpdate;
            CanAutoInstallUpdate = result.CanAutoInstall;
            LatestVersion = result.LatestVersion;
            ReleaseNotes = result.ReleaseNotes;
            ReleaseUrl = result.ReleaseUrl;
            UpdateStatusMessage = result.Message;
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (IsInstallingUpdate)
        {
            return;
        }

        if (!CanAutoInstallUpdate)
        {
            OpenReleasePage();
            return;
        }

        IsInstallingUpdate = true;
        DownloadProgressPercent = 0;
        UpdateStatusMessage = $"{LatestVersion} 업데이트 패키지를 다운로드하는 중입니다 (0%)...";
        try
        {
            var progress = new Progress<int>(pct =>
            {
                DownloadProgressPercent = pct;
                UpdateStatusMessage = $"{LatestVersion} 다운로드 중 ({pct}%)... 완료 시 자동으로 재시작됩니다.";
            });

            await _updateService.DownloadAndApplyUpdateAsync(progress);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"자동 업데이트 설치 실패: {ex.Message}";
        }
        finally
        {
            IsInstallingUpdate = false;
        }
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        if (string.IsNullOrWhiteSpace(ReleaseUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(ReleaseUrl) { UseShellExecute = true });
        }
        catch
        {
            // Ignore shell launch errors.
        }
    }
}
