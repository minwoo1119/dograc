using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Dograc.Core.Theme;

namespace Dograc.App.ViewModels;

/// <summary>
/// ViewModel for the Settings page covering persisted theme, shutdown options, runtime info, and GitHub updates.
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
    private string _updateStatusMessage = "최신 릴리스 여부를 확인할 수 있습니다.";

    [ObservableProperty]
    private bool _hasAvailableUpdate;

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
    public override string PageDescription => "화면 테마, 앱 종료 정책 및 GitHub 릴리스 업데이트를 관리합니다.";

    public string CurrentVersionLabel => $"v{_updateService.CurrentVersion}";

    public string ApiBaseUrl => _runtimeOptions.ApiBaseUrl;

    public string ProjectRoot => _runtimeOptions.ProjectRoot;

    public bool IsNotCheckingUpdate => !IsCheckingUpdate;

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
        if (IsCheckingUpdate)
        {
            return;
        }

        IsCheckingUpdate = true;
        UpdateStatusMessage = "GitHub 릴리스 정보를 확인하는 중입니다...";
        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            HasAvailableUpdate = result.HasUpdate;
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
