using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Navigation;
using Dograc.Core.Services;
using Dograc.Core.Theme;

namespace Dograc.App.ViewModels;

public sealed class ConversationRowViewModel
{
    public ConversationRowViewModel(ConversationDto conversation, bool isSelected)
    {
        Conversation = conversation;
        IsSelected = isSelected;
    }

    public ConversationDto Conversation { get; }
    public Guid Id => Conversation.Id;
    public string Title => Conversation.Title;
    public bool IsSelected { get; }
}

public sealed class ConversationGroupViewModel
{
    public ConversationGroupViewModel(string header, IReadOnlyList<ConversationRowViewModel> items)
    {
        Header = header;
        Items = items;
    }

    public string Header { get; }
    public IReadOnlyList<ConversationRowViewModel> Items { get; }
}

public sealed class SidebarMenuItem
{
    public SidebarMenuItem(string label, bool isSelected)
    {
        Label = label;
        IsSelected = isSelected;
    }

    public string Label { get; }
    public bool IsSelected { get; }
}

public sealed record QuickCommandEntry(
    string Category,
    string Title,
    string Subtitle,
    Action Execute);

/// <summary>
/// Coordinates the Main Shell layout, Activity Bar, Context-Aware Sidebar, Right Context Panel,
/// startup orchestration, updates, quick command palette, and status bar.
/// </summary>
public sealed partial class MainShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly IServiceManager _serviceManager;
    private readonly IStartupOrchestrator _startupOrchestrator;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly ISettingsStore _settingsStore;
    private readonly IUpdateService _updateService;
    private readonly INotificationService? _notificationService;
    private readonly IErrorHandlingService? _errorHandling;
    private readonly IRecoveryActionService? _recoveryService;
    private readonly IReadOnlyDictionary<NavigationPage, PageViewModelBase> _pages;
    private readonly Dictionary<NavigationPage, bool> _contextVisibilityByPage = new()
    {
        [NavigationPage.Chat] = true,
        [NavigationPage.Documents] = false,
        [NavigationPage.Workspaces] = false,
        [NavigationPage.Models] = false,
        [NavigationPage.System] = false,
        [NavigationPage.Settings] = false
    };

    private bool _isSyncingContext;
    private bool _hasLoadedInitialWorkspaces;
    private double _savedSidebarPixels = 260;
    private double _savedContextPixels = 344;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChatPage))]
    [NotifyPropertyChangedFor(nameof(IsNonChatPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    private NavigationPage _selectedPage;

    [ObservableProperty]
    private PageViewModelBase _currentPageViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarColumnWidth))]
    [NotifyPropertyChangedFor(nameof(SidebarSplitterWidth))]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextColumnWidth))]
    [NotifyPropertyChangedFor(nameof(ContextSplitterWidth))]
    private bool _isContextPanelVisible = true;

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
    private bool _showEnvironmentBanner;

    [ObservableProperty]
    private bool _canRetryStartup;

    [ObservableProperty]
    private string _bannerMessage = "로컬 환경(Docker, Ollama, FastAPI) 상태를 점검하는 중입니다...";

    [ObservableProperty]
    private string _titleStatusText = "초기화 중...";

    [ObservableProperty]
    private ServiceState _overallServiceState = ServiceState.Checking;

    [ObservableProperty]
    private string _statusMessage = "초기화 중...";

    [ObservableProperty]
    private string _statusBarSummary = "초기화 중...";

    [ObservableProperty]
    private string _conversationSearchQuery = string.Empty;

    [ObservableProperty]
    private WorkspaceDto? _selectedWorkspace;

    [ObservableProperty]
    private ConversationDto? _selectedConversation;

    [ObservableProperty]
    private string _sidebarSectionTitle = string.Empty;

    [ObservableProperty]
    private string _activeModelLabel = "qwen2.5:7b";

    [ObservableProperty]
    private bool _hasAvailableUpdate;

    [ObservableProperty]
    private bool _canAutoInstallUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotInstallingUpdate))]
    private bool _isInstallingUpdate;

    [ObservableProperty]
    private string _updateButtonLabel = "업데이트";

    [ObservableProperty]
    private string _updateTooltip = "업데이트 확인 (Settings → Updates)";

    private string? _availableUpdateUrl;

    public MainShellViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator,
        IWorkspaceContext workspaceContext,
        ISettingsStore settingsStore,
        IUpdateService updateService,
        EvidenceViewModel evidenceViewModel,
        PreviewViewModel previewViewModel,
        ChatViewModel chatViewModel,
        DocumentsViewModel documentsViewModel,
        WorkspacesViewModel workspacesViewModel,
        ModelsViewModel modelsViewModel,
        SystemViewModel systemViewModel,
        SettingsViewModel settingsViewModel,
        INotificationService? notificationService = null,
        IErrorHandlingService? errorHandling = null,
        IRecoveryActionService? recoveryService = null)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _serviceManager = serviceManager;
        _startupOrchestrator = startupOrchestrator;
        _workspaceContext = workspaceContext;
        _settingsStore = settingsStore;
        _updateService = updateService;
        _notificationService = notificationService;
        _errorHandling = errorHandling;
        _recoveryService = recoveryService;

        ChatPanel = chatViewModel;
        DocumentsPanel = documentsViewModel;
        WorkspacesPanel = workspacesViewModel;
        ModelsPanel = modelsViewModel;
        SystemPanel = systemViewModel;
        SettingsPanel = settingsViewModel;
        EvidencePanel = evidenceViewModel;
        PreviewPanel = previewViewModel;

        ActiveModelLabel = settingsStore.Current.SelectedModelName;
        settingsStore.SettingsChanged += (_, updated) => RunOnUiThread(() =>
        {
            ActiveModelLabel = updated.SelectedModelName;
        });

        chatViewModel.EvidenceRequested += (_, req) => RunOnUiThread(() =>
        {
            SelectedContextTab = ContextPanelTab.Evidence;
            SetContextPanelVisible(true);
            _ = EvidencePanel.LoadTraceAsync(req);

            if (!string.IsNullOrWhiteSpace(req.HighlightFileName))
            {
                PreviewPanel.LoadDocument(new PreviewDocumentRequest(
                    req.HighlightFileName,
                    req.HighlightPageNumber ?? 1));
            }
        });

        chatViewModel.AttachDocumentRequested += (_, _) => RunOnUiThread(() =>
        {
            SelectedContextTab = ContextPanelTab.Documents;
            SetContextPanelVisible(true);
            if (DocumentsPanel.BrowseAndUploadCommand.CanExecute(null))
            {
                DocumentsPanel.BrowseAndUploadCommand.Execute(null);
            }
        });

        chatViewModel.OpenDocumentsTabRequested += (_, _) => RunOnUiThread(() =>
        {
            SelectedContextTab = ContextPanelTab.Documents;
            SetContextPanelVisible(true);
        });

        evidenceViewModel.PreviewRequested += (_, previewReq) => RunOnUiThread(() =>
        {
            SelectedContextTab = ContextPanelTab.Preview;
            SetContextPanelVisible(true);
            PreviewPanel.LoadDocument(previewReq);
            StatusMessage = $"문서 미리보기: {previewReq.FileName} (p.{previewReq.PageNumber})";
        });

        evidenceViewModel.PromptDialogRequested += (_, prompt) => RunOnUiThread(() =>
        {
            PromptDialogRequested?.Invoke(this, prompt);
        });

        documentsViewModel.PreviewRequested += (_, previewReq) => RunOnUiThread(() =>
        {
            SelectedContextTab = ContextPanelTab.Preview;
            SetContextPanelVisible(true);
            PreviewPanel.LoadDocument(previewReq);
            StatusMessage = $"문서 미리보기: {previewReq.FileName}";
        });

        workspacesViewModel.NewWorkspaceDialogRequested += (_, _) => RunOnUiThread(() =>
        {
            NewWorkspaceDialogRequested?.Invoke(this, EventArgs.Empty);
        });

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
        _isContextPanelVisible = _contextVisibilityByPage[_selectedPage];

        _navigationService.PageChanged += (_, page) => RunOnUiThread(() => OnNavigatedToPage(page));

        TopNavigationItems =
        [
            new NavigationItem(NavigationPage.Chat, "Chat", "Icon.Chat", "Chat"),
            new NavigationItem(NavigationPage.Documents, "Documents", "Icon.Documents", "Documents"),
            new NavigationItem(NavigationPage.Workspaces, "Workspaces", "Icon.Workspaces", "Workspaces"),
            new NavigationItem(NavigationPage.Models, "Models", "Icon.Models", "Models"),
            new NavigationItem(NavigationPage.System, "System", "Icon.System", "System")
        ];
        SettingsNavigationItem = new NavigationItem(NavigationPage.Settings, "Settings", "Icon.Settings", "Settings");

        NavigationItems = [.. TopNavigationItems, SettingsNavigationItem];

        ServiceStatuses = new ObservableCollection<ServiceComponentStatus>(_serviceManager.CurrentStatuses);
        Workspaces = new ObservableCollection<WorkspaceDto>();
        FilteredConversations = new ObservableCollection<ConversationDto>();
        ConversationGroups = new ObservableCollection<ConversationGroupViewModel>();
        SidebarMenuItems = new ObservableCollection<SidebarMenuItem>();

        _serviceManager.StatusesChanged += (_, statuses) => RunOnUiThread(() =>
        {
            ServiceStatuses.Clear();
            foreach (var status in statuses)
            {
                ServiceStatuses.Add(status);
            }

            var readyNow = _serviceManager.IsApiReady;
            IsApiReady = readyNow;
            UpdateHealthSummary();
            if (readyNow && !_hasLoadedInitialWorkspaces)
            {
                _ = LoadInitialWorkspaceDataAsync();
            }
        });

        _startupOrchestrator.StateChanged += (_, state) => RunOnUiThread(() => ApplyStartupState(state));
        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromWorkspaceContext);

        systemViewModel.SetupWizardRequested += (_, _) => RequestSetupWizard();

        ActiveToasts = new ObservableCollection<ToastNotificationItem>();
        if (_notificationService is not null)
        {
            _notificationService.NotificationAdded += (_, toast) => RunOnUiThread(() => AddToast(toast));
            _notificationService.NotificationDismissed += (_, id) => RunOnUiThread(() => RemoveToast(id));
        }

        RebuildContextSidebarMenu();
    }

    public ObservableCollection<ToastNotificationItem> ActiveToasts { get; }

    public ChatViewModel ChatPanel { get; }
    public DocumentsViewModel DocumentsPanel { get; }
    public WorkspacesViewModel WorkspacesPanel { get; }
    public ModelsViewModel ModelsPanel { get; }
    public SystemViewModel SystemPanel { get; }
    public SettingsViewModel SettingsPanel { get; }
    public EvidenceViewModel EvidencePanel { get; }
    public PreviewViewModel PreviewPanel { get; }

    public IReadOnlyList<NavigationItem> TopNavigationItems { get; }
    public NavigationItem SettingsNavigationItem { get; }
    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    public ObservableCollection<ServiceComponentStatus> ServiceStatuses { get; }
    public ObservableCollection<WorkspaceDto> Workspaces { get; }
    public ObservableCollection<ConversationDto> FilteredConversations { get; }
    public ObservableCollection<ConversationGroupViewModel> ConversationGroups { get; }
    public ObservableCollection<SidebarMenuItem> SidebarMenuItems { get; }

    public bool IsChatPage => SelectedPage == NavigationPage.Chat;
    public bool IsNonChatPage => SelectedPage != NavigationPage.Chat;
    public bool IsSettingsPage => SelectedPage == NavigationPage.Settings;

    public bool IsDocumentsContextTab => SelectedContextTab == ContextPanelTab.Documents;
    public bool IsEvidenceContextTab => SelectedContextTab == ContextPanelTab.Evidence;
    public bool IsPreviewContextTab => SelectedContextTab == ContextPanelTab.Preview;

    public GridLength SidebarColumnWidth
    {
        get => IsSidebarVisible ? new GridLength(_savedSidebarPixels) : new GridLength(0);
        set
        {
            if (IsSidebarVisible && value.IsAbsolute && value.Value >= 180 && value.Value <= 420)
            {
                _savedSidebarPixels = value.Value;
                OnPropertyChanged();
            }
        }
    }

    public GridLength SidebarSplitterWidth => IsSidebarVisible ? new GridLength(1) : new GridLength(0);

    public GridLength ContextColumnWidth
    {
        get => IsContextPanelVisible ? new GridLength(_savedContextPixels) : new GridLength(0);
        set
        {
            if (IsContextPanelVisible && value.IsAbsolute && value.Value >= 260 && value.Value <= 560)
            {
                _savedContextPixels = value.Value;
                OnPropertyChanged();
            }
        }
    }

    public GridLength ContextSplitterWidth => IsContextPanelVisible ? new GridLength(1) : new GridLength(0);

    public string AppVersionLabel => $"dograc Desktop · v{_updateService.CurrentVersion}";
    public string ShortVersionLabel => $"v{_updateService.CurrentVersion}";
    public bool IsNotInstallingUpdate => !IsInstallingUpdate;

    public event EventHandler<EnvironmentCheckSummary>? SetupWizardRequested;
    public event EventHandler? QuickCommandRequested;
    public event EventHandler<string>? PromptDialogRequested;
    public event EventHandler? NewWorkspaceDialogRequested;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = CheckForUpdatesSilentlyAsync(cancellationToken);
        await _startupOrchestrator.RunStartupAsync(cancellationToken);
        if (_serviceManager.IsApiReady && !_hasLoadedInitialWorkspaces)
        {
            await LoadInitialWorkspaceDataAsync(cancellationToken);
        }
    }

    public async Task CheckForUpdatesSilentlyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _updateService.CheckForUpdatesAsync(cancellationToken);
            RunOnUiThread(() =>
            {
                HasAvailableUpdate = result.HasUpdate;
                CanAutoInstallUpdate = result.CanAutoInstall;
                _availableUpdateUrl = result.ReleaseUrl;
                if (result.HasUpdate)
                {
                    UpdateButtonLabel = $"업데이트 {result.LatestVersion}";
                    UpdateTooltip = result.CanAutoInstall
                        ? $"{result.LatestVersion} 클릭하여 즉시 다운로드 및 재시작 설치"
                        : $"{result.LatestVersion} 릴리스 페이지 열기";
                }
            });
        }
        catch (Exception ex)
        {
            _errorHandling?.Handle(
                ex,
                new ErrorContext("Updates", "Updates", "SilentUpdateCheck"),
                notifyToast: false);
        }
    }

    [RelayCommand]
    private async Task InstallOrOpenUpdateAsync()
    {
        if (IsInstallingUpdate)
        {
            return;
        }

        if (!HasAvailableUpdate)
        {
            SettingsPanel.SelectSectionCommand.Execute("Updates");
            _navigationService.NavigateTo(NavigationPage.Settings);
            return;
        }

        if (!CanAutoInstallUpdate)
        {
            if (!string.IsNullOrWhiteSpace(_availableUpdateUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_availableUpdateUrl) { UseShellExecute = true });
                    StatusMessage = "GitHub 릴리스 페이지를 열었습니다.";
                }
                catch
                {
                    SettingsPanel.SelectSectionCommand.Execute("Updates");
                    _navigationService.NavigateTo(NavigationPage.Settings);
                }
            }
            else
            {
                SettingsPanel.SelectSectionCommand.Execute("Updates");
                _navigationService.NavigateTo(NavigationPage.Settings);
            }

            return;
        }

        IsInstallingUpdate = true;
        UpdateButtonLabel = "업데이트 0%";
        StatusMessage = "새 버전을 다운로드하고 있습니다...";
        try
        {
            var progress = new Progress<int>(pct => RunOnUiThread(() =>
            {
                UpdateButtonLabel = $"업데이트 {pct}%";
                StatusMessage = $"업데이트 다운로드 중 ({pct}%)... 완료 시 자동으로 재시작됩니다.";
            }));

            await _updateService.DownloadAndApplyUpdateAsync(progress);
        }
        catch (Exception ex)
        {
            var err = _errorHandling?.Handle(ex, new ErrorContext("Updates", "Updates", "InstallUpdate"), InstallOrOpenUpdateAsync, notifyToast: true);
            StatusMessage = err?.Description ?? $"자동 업데이트 실패: {ex.Message}";
            UpdateButtonLabel = "업데이트 재시도";
        }
        finally
        {
            IsInstallingUpdate = false;
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
        RebuildConversationGroups();
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
    private void OpenQuickCommandPalette()
    {
        QuickCommandRequested?.Invoke(this, EventArgs.Empty);
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
    private void SelectConversationRow(ConversationRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedConversation = row.Conversation;
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
        SetContextPanelVisible(!IsContextPanelVisible);
    }

    [RelayCommand]
    private void SelectContextTab(ContextPanelTab tab)
    {
        SelectedContextTab = tab;
        if (!IsContextPanelVisible)
        {
            SetContextPanelVisible(true);
        }
    }

    [RelayCommand]
    private void BackToEvidence()
    {
        SelectedContextTab = ContextPanelTab.Evidence;
    }

    [RelayCommand]
    private void SelectSidebarMenuItem(SidebarMenuItem? item)
    {
        if (item is null)
        {
            return;
        }

        switch (SelectedPage)
        {
            case NavigationPage.Documents:
                DocumentsPanel.ApplySidebarFilter(item.Label);
                break;
            case NavigationPage.Workspaces:
                WorkspacesPanel.ApplySidebarFilter(item.Label);
                break;
            case NavigationPage.Models:
                ModelsPanel.SelectedSection = item.Label;
                break;
            case NavigationPage.System:
                if (item.Label == "환경 초기 설정 마법사")
                {
                    RequestSetupWizard();
                }
                break;
            case NavigationPage.Settings:
                SettingsPanel.SelectSectionCommand.Execute(item.Label);
                break;
        }

        RebuildContextSidebarMenu(item.Label);
    }

    [RelayCommand]
    private void CycleTheme()
    {
        var next = _themeService.EffectiveTheme == AppThemeMode.Dark
            ? AppThemeMode.Light
            : AppThemeMode.Dark;
        _themeService.ApplyTheme(next);
        _settingsStore.Save(_settingsStore.Current with { ThemeMode = next });
        StatusMessage = $"테마 변경됨: {next}";
    }

    public IReadOnlyList<QuickCommandEntry> GetQuickCommandItems()
    {
        var list = new List<QuickCommandEntry>
        {
            new("페이지", "Chat", "AI Chat Workspace로 이동", () => _navigationService.NavigateTo(NavigationPage.Chat)),
            new("페이지", "Documents", "문서 업로드 및 색인 관리", () => _navigationService.NavigateTo(NavigationPage.Documents)),
            new("페이지", "Workspaces", "워크스페이스 관리 및 전환", () => _navigationService.NavigateTo(NavigationPage.Workspaces)),
            new("페이지", "Models", "로컬 Ollama 모델 선택 및 연결", () => _navigationService.NavigateTo(NavigationPage.Models)),
            new("페이지", "System", "서비스 상태 진단 및 실시간 로그", () => _navigationService.NavigateTo(NavigationPage.System)),
            new("페이지", "Settings", "테마, 로컬 환경, 업데이트 설정", () => _navigationService.NavigateTo(NavigationPage.Settings)),
            new("동작", "새 대화 시작", "현재 워크스페이스에 새 대화 생성", () => { if (CreateConversationCommand.CanExecute(null)) CreateConversationCommand.Execute(null); }),
            new("동작", "파일 추가", "문서 파일 선택 및 업로드", () => { if (DocumentsPanel.BrowseAndUploadCommand.CanExecute(null)) DocumentsPanel.BrowseAndUploadCommand.Execute(null); }),
            new("동작", "테마 전환 (Light / Dark)", "라이트 모드와 다크 모드 전환", CycleTheme),
            new("동작", "왼쪽 사이드바 토글", "사이드바 열기 또는 닫기", ToggleSidebar),
            new("동작", "오른쪽 컨텍스트 패널 토글", "Documents/Evidence/Preview 패널 열기 또는 닫기", ToggleContextPanel),
            new("동작", "초기 환경 설정 마법사 열기", "Docker / Ollama / FastAPI 점검", RequestSetupWizard)
        };

        foreach (var ws in Workspaces)
        {
            var captured = ws;
            list.Add(new QuickCommandEntry("워크스페이스", captured.Name, "워크스페이스 전환", () => SelectedWorkspace = captured));
        }

        foreach (var conv in FilteredConversations.Take(8))
        {
            var captured = conv;
            list.Add(new QuickCommandEntry("대화", captured.Title, "대화 열기", () => SelectedConversation = captured));
        }

        return list;
    }

    private void OnNavigatedToPage(NavigationPage page)
    {
        _contextVisibilityByPage[SelectedPage] = IsContextPanelVisible;
        SelectedPage = page;
        CurrentPageViewModel = _pages[page];
        SetContextPanelVisible(_contextVisibilityByPage.GetValueOrDefault(page, page == NavigationPage.Chat));
        RebuildContextSidebarMenu();
    }

    private void SetContextPanelVisible(bool visible)
    {
        _contextVisibilityByPage[SelectedPage] = visible;
        IsContextPanelVisible = visible;
    }

    private void RebuildContextSidebarMenu(string? explicitSelected = null)
    {
        SidebarMenuItems.Clear();

        string[] items;
        string selected;

        switch (SelectedPage)
        {
            case NavigationPage.Documents:
                SidebarSectionTitle = "문서 필터";
                items = ["전체 문서", "색인 완료", "처리 중", "실패"];
                selected = explicitSelected ?? (DocumentsPanel.SelectedStatusFilter == "전체 상태"
                    ? "전체 문서"
                    : DocumentsPanel.SelectedStatusFilter);
                break;

            case NavigationPage.Workspaces:
                SidebarSectionTitle = "워크스페이스";
                items = ["전체 워크스페이스", "현재 워크스페이스"];
                selected = explicitSelected ?? (WorkspacesPanel.ShowCurrentOnly ? "현재 워크스페이스" : "전체 워크스페이스");
                break;

            case NavigationPage.Models:
                SidebarSectionTitle = "모델 관리";
                items = ["설치된 모델", "추천 모델", "연결 설정"];
                selected = explicitSelected ?? ModelsPanel.SelectedSection;
                break;

            case NavigationPage.System:
                SidebarSectionTitle = "시스템 진단";
                items = ["서비스 상태", "실시간 로그", "환경 초기 설정 마법사"];
                selected = explicitSelected ?? "서비스 상태";
                break;

            case NavigationPage.Settings:
                SidebarSectionTitle = "설정";
                items = ["General", "Appearance", "Local Environment", "Updates", "About"];
                selected = explicitSelected ?? SettingsPanel.SelectedSection;
                break;

            default:
                SidebarSectionTitle = string.Empty;
                return;
        }

        foreach (var label in items)
        {
            SidebarMenuItems.Add(new SidebarMenuItem(label, string.Equals(label, selected, StringComparison.OrdinalIgnoreCase)));
        }
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
                UpdateHealthSummary();
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
        RebuildConversationGroups();
    }

    private void RebuildConversationGroups()
    {
        ConversationGroups.Clear();
        var today = DateTimeOffset.Now.Date;
        var yesterday = today.AddDays(-1);
        var selectedId = SelectedConversation?.Id;

        var todayItems = new List<ConversationRowViewModel>();
        var yesterdayItems = new List<ConversationRowViewModel>();
        var earlierItems = new List<ConversationRowViewModel>();

        foreach (var conv in FilteredConversations)
        {
            var localDate = conv.CreatedAt.LocalDateTime.Date;
            var row = new ConversationRowViewModel(conv, selectedId == conv.Id);
            if (localDate >= today)
            {
                todayItems.Add(row);
            }
            else if (localDate == yesterday)
            {
                yesterdayItems.Add(row);
            }
            else
            {
                earlierItems.Add(row);
            }
        }

        if (todayItems.Count > 0)
        {
            ConversationGroups.Add(new ConversationGroupViewModel("오늘", todayItems));
        }

        if (yesterdayItems.Count > 0)
        {
            ConversationGroups.Add(new ConversationGroupViewModel("어제", yesterdayItems));
        }

        if (earlierItems.Count > 0)
        {
            ConversationGroups.Add(new ConversationGroupViewModel("이전", earlierItems));
        }
    }

    private void ApplyStartupState(StartupState state)
    {
        StartupState = state;
        IsApiReady = _serviceManager.IsApiReady;
        BannerMessage = _startupOrchestrator.StatusBannerText;
        StatusMessage = _startupOrchestrator.StatusBannerText;
        ShowEnvironmentBanner = state is StartupState.Failed or StartupState.NeedsSetup;
        CanRetryStartup = state is StartupState.Failed or StartupState.NeedsSetup;
        UpdateHealthSummary();

        if (state == StartupState.Ready && !_hasLoadedInitialWorkspaces)
        {
            _ = LoadInitialWorkspaceDataAsync();
        }

        if (state == StartupState.NeedsSetup && _startupOrchestrator.LastEnvironmentCheck is not null)
        {
            SetupWizardRequested?.Invoke(this, _startupOrchestrator.LastEnvironmentCheck);
        }
    }

    [RelayCommand]
    private void DismissToast(ToastNotificationItem? toast)
    {
        if (toast is not null)
        {
            RemoveToast(toast.Id);
        }
    }

    [RelayCommand]
    private async Task ExecuteToastActionAsync(object? parameter)
    {
        if (parameter is not RecoveryActionDescriptor action)
        {
            return;
        }

        var parentToast = ActiveToasts.FirstOrDefault(t => t.Actions.Contains(action));
        if (parentToast is not null)
        {
            RemoveToast(parentToast.Id);
        }

        if (_recoveryService is not null)
        {
            await _recoveryService.ExecuteAsync(action, parentToast?.SourceError, parentToast?.RetryCallback);
        }
    }

    private void AddToast(ToastNotificationItem toast)
    {
        ActiveToasts.Insert(0, toast);
        while (ActiveToasts.Count > 3)
        {
            ActiveToasts.RemoveAt(ActiveToasts.Count - 1);
        }

        if (toast.Severity is ErrorSeverity.Information or ErrorSeverity.Warning)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(toast.Severity == ErrorSeverity.Information ? 4.5 : 8)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                RemoveToast(toast.Id);
            };
            timer.Start();
        }
    }

    private void RemoveToast(Guid id)
    {
        var existing = ActiveToasts.FirstOrDefault(t => t.Id == id);
        if (existing is not null)
        {
            ActiveToasts.Remove(existing);
        }
    }

    private void UpdateHealthSummary()
    {
        var apiStatus = ServiceStatuses.FirstOrDefault(s => s.Key == "api");
        var anyDegraded = ServiceStatuses.Any(s => s.State == ServiceState.Degraded);
        var anyError = ServiceStatuses.Any(s => s.State is ServiceState.Failed or ServiceState.Error or ServiceState.Unreachable);

        if (apiStatus?.State == ServiceState.Ready && !anyDegraded && !anyError)
        {
            TitleStatusText = "Local Ready";
            OverallServiceState = ServiceState.Ready;
            StatusBarSummary = "All services operational";
        }
        else if (apiStatus?.State == ServiceState.Degraded || (IsApiReady && (anyDegraded || anyError)))
        {
            TitleStatusText = "일부 서비스 확인 필요";
            OverallServiceState = ServiceState.Degraded;
            StatusBarSummary = "백엔드 응답 중 · 일부 하위 서비스(Docker/Ollama) 상태를 확인해 주세요";
        }
        else if (IsApiReady || StartupState == StartupState.Ready)
        {
            TitleStatusText = "Local Ready";
            OverallServiceState = ServiceState.Ready;
            StatusBarSummary = "All services operational";
        }
        else if (StartupState is StartupState.Failed or StartupState.NeedsSetup)
        {
            TitleStatusText = "환경 확인 필요";
            OverallServiceState = ServiceState.Error;
            StatusBarSummary = "로컬 환경 확인 필요";
        }
        else
        {
            TitleStatusText = "초기화 중...";
            OverallServiceState = ServiceState.Starting;
            StatusBarSummary = StatusMessage;
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
