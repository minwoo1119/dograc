using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Navigation;
using Dograc.Core.Theme;

namespace Dograc.App.ViewModels;

/// <summary>
/// Coordinates the Main Shell layout, Navigation Rail, collapsible panels, and status bar.
/// </summary>
public sealed partial class MainShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
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
    private string _statusMessage = "Shell 초기화 완료 — Phase 3에서 서비스 자동 점검이 연결됩니다.";

    [ObservableProperty]
    private string _conversationSearchQuery = string.Empty;

    [ObservableProperty]
    private WorkspaceSummary? _selectedWorkspace;

    public MainShellViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        ChatViewModel chatViewModel,
        DocumentsViewModel documentsViewModel,
        WorkspacesViewModel workspacesViewModel,
        ModelsViewModel modelsViewModel,
        SystemViewModel systemViewModel,
        SettingsViewModel settingsViewModel)
    {
        _navigationService = navigationService;
        _themeService = themeService;

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

        ServiceStatuses =
        [
            new ServiceComponentStatus("docker", "Infra", false, "대기"),
            new ServiceComponentStatus("ollama", "Ollama", false, "대기"),
            new ServiceComponentStatus("api", "API", false, "대기")
        ];

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

    public bool ShowSidebar => IsSidebarVisible;

    public bool ShowContextPanel => IsContextPanelVisible;

    [RelayCommand]
    private void Navigate(NavigationPage page)
    {
        _navigationService.NavigateTo(page);
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
        OnPropertyChanged(nameof(ShowSidebar));
    }

    [RelayCommand]
    private void ToggleContextPanel()
    {
        IsContextPanelVisible = !IsContextPanelVisible;
        OnPropertyChanged(nameof(ShowContextPanel));
    }

    [RelayCommand]
    private void SelectContextTab(ContextPanelTab tab)
    {
        SelectedContextTab = tab;
        if (!IsContextPanelVisible)
        {
            IsContextPanelVisible = true;
            OnPropertyChanged(nameof(ShowContextPanel));
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
}
