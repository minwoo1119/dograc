using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Navigation;
using Dograc.Core.Services;
using Dograc.Core.Theme;

namespace Dograc.App.ViewModels;

/// <summary>
/// Coordinates the Main Shell layout, Navigation Rail, collapsible panels, startup orchestration, and status bar.
/// </summary>
public sealed partial class MainShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly IServiceManager _serviceManager;
    private readonly IStartupOrchestrator _startupOrchestrator;
    private readonly IReadOnlyDictionary<NavigationPage, PageViewModelBase> _pages;

    [ObservableProperty]
    private NavigationPage _selectedPage;

    [ObservableProperty]
    private PageViewModelBase _currentPageViewModel;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private GridLength _sidebarWidth = new(260);

    [ObservableProperty]
    private bool _isContextPanelVisible = true;

    [ObservableProperty]
    private GridLength _contextPanelWidth = new(340);

    [ObservableProperty]
    private ContextPanelTab _selectedContextTab = ContextPanelTab.Documents;

    [ObservableProperty]
    private StartupState _startupState = StartupState.Idle;

    [ObservableProperty]
    private bool _isApiReady;

    [ObservableProperty]
    private bool _showEnvironmentBanner = true;

    [ObservableProperty]
    private bool _canRetryStartup;

    [ObservableProperty]
    private string _bannerMessage = "로컬 환경(Docker, Ollama, FastAPI) 상태를 점검하는 중입니다...";

    [ObservableProperty]
    private string _statusMessage = "초기화 중...";

    [ObservableProperty]
    private string _conversationSearchQuery = string.Empty;

    [ObservableProperty]
    private WorkspaceSummary? _selectedWorkspace;

    public MainShellViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator,
        ChatViewModel chatViewModel,
        DocumentsViewModel documentsViewModel,
        WorkspacesViewModel workspacesViewModel,
        ModelsViewModel modelsViewModel,
        SystemViewModel systemViewModel,
        SettingsViewModel settingsViewModel)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _serviceManager = serviceManager;
        _startupOrchestrator = startupOrchestrator;

        _pages = new Dictionary<NavigationPage, PageViewModelBase>
        {
            [NavigationPage.Chat] = chatViewModel,
            [NavigationPage.Documents] = documentsViewModel,
            [NavigationPage.Workspaces] = workspacesViewModel,
            [NavigationPage.Models] = modelsViewModel,
            [NavigationPage.System] = systemViewModel,
            [NavigationPage.Settings] = settingsViewModel
        };

        _selectedPage = _navigationService.CurrentPage;
        _currentPageViewModel = _pages[_selectedPage];

        _navigationService.PageChanged += (_, page) =>
        {
            SelectedPage = page;
            CurrentPageViewModel = _pages[page];
        };

        NavigationItems =
        [
            new NavigationItem(NavigationPage.Chat, "Chat", "\uE8BD", "AI Chat Workspace"),
            new NavigationItem(NavigationPage.Documents, "Docs", "\uE8A5", "Documents"),
            new NavigationItem(NavigationPage.Workspaces, "Spaces", "\uE8F1", "Workspaces"),
            new NavigationItem(NavigationPage.Models, "Models", "\uE99A", "Models"),
            new NavigationItem(NavigationPage.System, "System", "\uE9D9", "System & Logs"),
            new NavigationItem(NavigationPage.Settings, "Settings", "\uE713", "Settings")
        ];

        ServiceStatuses = new ObservableCollection<ServiceComponentStatus>(_serviceManager.CurrentStatuses);

        _serviceManager.StatusesChanged += (_, statuses) => RunOnUiThread(() =>
        {
            ServiceStatuses.Clear();
            foreach (var status in statuses)
            {
                ServiceStatuses.Add(status);
            }

            IsApiReady = _serviceManager.IsApiReady;
        });

        _startupOrchestrator.StateChanged += (_, state) => RunOnUiThread(() => ApplyStartupState(state));

        systemViewModel.SetupWizardRequested += (_, _) => RequestSetupWizard();

        Workspaces =
        [
            new WorkspaceSummary(Guid.Empty, "워크스페이스 선택 (Phase 4 연결 예정)")
        ];
        _selectedWorkspace = Workspaces[0];

        Conversations = [];
    }

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    public ObservableCollection<ServiceComponentStatus> ServiceStatuses { get; }

    public ObservableCollection<WorkspaceSummary> Workspaces { get; }

    public ObservableCollection<ConversationSummary> Conversations { get; }

    public event EventHandler<EnvironmentCheckSummary>? SetupWizardRequested;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _startupOrchestrator.RunStartupAsync(cancellationToken);
    }

    [RelayCommand]
    private void Navigate(NavigationPage page)
    {
        _navigationService.NavigateTo(page);
    }

    [RelayCommand]
    private void OpenSystemPage()
    {
        _navigationService.NavigateTo(NavigationPage.System);
    }

    [RelayCommand]
    private async Task RetryStartupAsync()
    {
        await _startupOrchestrator.RunStartupAsync();
    }

    [RelayCommand]
    private void OpenSetupWizard()
    {
        RequestSetupWizard();
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    [RelayCommand]
    private void ToggleContextPanel()
    {
        IsContextPanelVisible = !IsContextPanelVisible;
    }

    [RelayCommand]
    private void SelectContextTab(ContextPanelTab tab)
    {
        SelectedContextTab = tab;
        if (!IsContextPanelVisible)
        {
            IsContextPanelVisible = true;
        }
    }

    [RelayCommand]
    private void CycleTheme()
    {
        var next = _themeService.CurrentMode switch
        {
            AppThemeMode.System => AppThemeMode.Light,
            AppThemeMode.Light => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };
        _themeService.ApplyTheme(next);
        StatusMessage = $"테마 변경됨: {next}";
    }

    private void ApplyStartupState(StartupState state)
    {
        StartupState = state;
        IsApiReady = _serviceManager.IsApiReady;
        BannerMessage = _startupOrchestrator.StatusBannerText;
        StatusMessage = _startupOrchestrator.StatusBannerText;
        ShowEnvironmentBanner = state != StartupState.Ready;
        CanRetryStartup = state is StartupState.Failed or StartupState.NeedsSetup;

        if (state == StartupState.NeedsSetup && _startupOrchestrator.LastEnvironmentCheck is not null)
        {
            SetupWizardRequested?.Invoke(this, _startupOrchestrator.LastEnvironmentCheck);
        }
    }

    private async void RequestSetupWizard()
    {
        var summary = _startupOrchestrator.LastEnvironmentCheck ?? await _serviceManager.InspectEnvironmentAsync();
        SetupWizardRequested?.Invoke(this, summary);
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
