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
/// Coordinates the Main Shell layout, Navigation Rail, Sidebar, Context Panel, startup orchestration, and status bar.
/// </summary>
public sealed partial class MainShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly IServiceManager _serviceManager;
    private readonly IStartupOrchestrator _startupOrchestrator;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly IReadOnlyDictionary<NavigationPage, PageViewModelBase> _pages;

    private bool _isSyncingContext;
    private bool _hasLoadedInitialWorkspaces;

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
    [NotifyPropertyChangedFor(nameof(IsDocumentsContextTab))]
    [NotifyPropertyChangedFor(nameof(IsEvidenceContextTab))]
    [NotifyPropertyChangedFor(nameof(IsPreviewContextTab))]
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
    private WorkspaceDto? _selectedWorkspace;

    [ObservableProperty]
    private ConversationDto? _selectedConversation;

    public MainShellViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator,
        IWorkspaceContext workspaceContext,
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
        _workspaceContext = workspaceContext;
        DocumentsPanel = documentsViewModel;

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
        Workspaces = new ObservableCollection<WorkspaceDto>();
        FilteredConversations = new ObservableCollection<ConversationDto>();

        _serviceManager.StatusesChanged += (_, statuses) => RunOnUiThread(() =>
        {
            ServiceStatuses.Clear();
            foreach (var status in statuses)
            {
                ServiceStatuses.Add(status);
            }

            var readyNow = _serviceManager.IsApiReady;
            IsApiReady = readyNow;
            if (readyNow && !_hasLoadedInitialWorkspaces)
            {
                _ = LoadInitialWorkspaceDataAsync();
            }
        });

        _startupOrchestrator.StateChanged += (_, state) => RunOnUiThread(() => ApplyStartupState(state));
        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromWorkspaceContext);

        systemViewModel.SetupWizardRequested += (_, _) => RequestSetupWizard();
    }

    public DocumentsViewModel DocumentsPanel { get; }

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    public ObservableCollection<ServiceComponentStatus> ServiceStatuses { get; }

    public ObservableCollection<WorkspaceDto> Workspaces { get; }

    public ObservableCollection<ConversationDto> FilteredConversations { get; }

    public bool IsDocumentsContextTab => SelectedContextTab == ContextPanelTab.Documents;
    public bool IsEvidenceContextTab => SelectedContextTab == ContextPanelTab.Evidence;
    public bool IsPreviewContextTab => SelectedContextTab == ContextPanelTab.Preview;

    public event EventHandler<EnvironmentCheckSummary>? SetupWizardRequested;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _startupOrchestrator.RunStartupAsync(cancellationToken);
        if (_serviceManager.IsApiReady && !_hasLoadedInitialWorkspaces)
        {
            await LoadInitialWorkspaceDataAsync(cancellationToken);
        }
    }

    partial void OnConversationSearchQueryChanged(string value)
    {
        RebuildFilteredConversations();
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceDto? value)
    {
        if (_isSyncingContext)
        {
            return;
        }

        _ = SelectWorkspaceFromUiAsync(value);
    }

    partial void OnSelectedConversationChanged(ConversationDto? value)
    {
        if (_isSyncingContext)
        {
            return;
        }

        _workspaceContext.SelectConversation(value?.Id);
        if (value is not null && SelectedPage != NavigationPage.Chat)
        {
            _navigationService.NavigateTo(NavigationPage.Chat);
        }
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
    private void OpenWorkspacesPage()
    {
        _navigationService.NavigateTo(NavigationPage.Workspaces);
    }

    [RelayCommand]
    private async Task RetryStartupAsync()
    {
        await _startupOrchestrator.RunStartupAsync();
        if (_serviceManager.IsApiReady)
        {
            await LoadInitialWorkspaceDataAsync();
        }
    }

    [RelayCommand]
    private void OpenSetupWizard()
    {
        RequestSetupWizard();
    }

    [RelayCommand]
    private async Task CreateConversationAsync()
    {
        if (!IsApiReady || _workspaceContext.SelectedWorkspace is null)
        {
            StatusMessage = "먼저 워크스페이스를 선택해 주세요.";
            return;
        }

        try
        {
            var created = await _workspaceContext.CreateConversationAsync();
            StatusMessage = $"새 대화를 생성했습니다: {created.Title}";
            _navigationService.NavigateTo(NavigationPage.Chat);
        }
        catch (Exception ex)
        {
            StatusMessage = $"대화 생성 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(ConversationDto? conversation)
    {
        if (conversation is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"'{conversation.Title}' 대화를 삭제하시겠습니까?",
            "대화 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _workspaceContext.DeleteConversationAsync(conversation.Id);
            StatusMessage = $"대화를 삭제했습니다: {conversation.Title}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"대화 삭제 실패: {ex.Message}";
        }
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

    private async Task LoadInitialWorkspaceDataAsync(CancellationToken cancellationToken = default)
    {
        _hasLoadedInitialWorkspaces = true;
        try
        {
            await _workspaceContext.LoadWorkspacesAsync(cancellationToken);
            RunOnUiThread(() =>
            {
                StatusMessage = _workspaceContext.Workspaces.Count > 0
                    ? $"워크스페이스 {_workspaceContext.Workspaces.Count}개 로드 완료"
                    : "워크스페이스가 없습니다. Workspaces 페이지에서 새 워크스페이스를 만들어 주세요.";
            });
        }
        catch (Exception ex)
        {
            _hasLoadedInitialWorkspaces = false;
            RunOnUiThread(() => StatusMessage = $"초기 데이터 로드 실패: {ex.Message}");
        }
    }

    private async Task SelectWorkspaceFromUiAsync(WorkspaceDto? workspace)
    {
        try
        {
            await _workspaceContext.SelectWorkspaceAsync(workspace?.Id);
            if (workspace is not null)
            {
                StatusMessage = $"활성 워크스페이스: {workspace.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"워크스페이스 전환 실패: {ex.Message}";
        }
    }

    private void SyncFromWorkspaceContext()
    {
        _isSyncingContext = true;
        try
        {
            Workspaces.Clear();
            foreach (var ws in _workspaceContext.Workspaces)
            {
                Workspaces.Add(ws);
            }

            SelectedWorkspace = Workspaces.FirstOrDefault(w => w.Id == _workspaceContext.SelectedWorkspace?.Id);
            RebuildFilteredConversations();
        }
        finally
        {
            _isSyncingContext = false;
        }
    }

    private void RebuildFilteredConversations()
    {
        var query = ConversationSearchQuery.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _workspaceContext.Conversations
            : _workspaceContext.Conversations
                .Where(c => c.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        FilteredConversations.Clear();
        foreach (var conv in filtered)
        {
            FilteredConversations.Add(conv);
        }

        SelectedConversation = FilteredConversations.FirstOrDefault(c => c.Id == _workspaceContext.SelectedConversation?.Id);
    }

    private void ApplyStartupState(StartupState state)
    {
        StartupState = state;
        IsApiReady = _serviceManager.IsApiReady;
        BannerMessage = _startupOrchestrator.StatusBannerText;
        StatusMessage = _startupOrchestrator.StatusBannerText;
        ShowEnvironmentBanner = state != StartupState.Ready;
        CanRetryStartup = state is StartupState.Failed or StartupState.NeedsSetup;

        if (state == StartupState.Ready && !_hasLoadedInitialWorkspaces)
        {
            _ = LoadInitialWorkspaceDataAsync();
        }

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
