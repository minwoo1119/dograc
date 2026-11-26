using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

public sealed record WorkspaceRowViewModel(
    WorkspaceDto Dto,
    bool IsActive,
    string Subtitle)
{
    public Guid Id => Dto.Id;
    public string Name => Dto.Name;
    public bool IsNotActive => !IsActive;
}

/// <summary>
/// Manages workspace listing, search filtering, creation, selection, and deletion against FastAPI.
/// </summary>
public sealed partial class WorkspacesViewModel : PageViewModelBase
{
    private readonly IWorkspaceContext _workspaceContext;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _showCurrentOnly;

    [ObservableProperty]
    private string _newWorkspaceName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private WorkspaceDto? _activeWorkspace;

    public WorkspacesViewModel(IWorkspaceContext workspaceContext)
    {
        _workspaceContext = workspaceContext;
        Workspaces = new ObservableCollection<WorkspaceDto>(_workspaceContext.Workspaces);
        FilteredWorkspaces = new ObservableCollection<WorkspaceRowViewModel>();
        SyncFromContext();

        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromContext);
    }

    public override string PageTitle => "Workspaces";
    public override string PageDescription => "프로젝트별 문서와 대화를 관리합니다.";

    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<WorkspaceDto> Workspaces { get; }

    public ObservableCollection<WorkspaceRowViewModel> FilteredWorkspaces { get; }

    public event EventHandler? NewWorkspaceDialogRequested;

    partial void OnSearchQueryChanged(string value) => RebuildFiltered();

    partial void OnShowCurrentOnlyChanged(bool value) => RebuildFiltered();

    public void ApplySidebarFilter(string filter)
    {
        ShowCurrentOnly = filter.Contains("현재", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void RequestNewWorkspaceDialog()
    {
        NewWorkspaceName = string.Empty;
        ErrorMessage = null;
        NewWorkspaceDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RunGuardedAsync(() => _workspaceContext.LoadWorkspacesAsync());
    }

    [RelayCommand]
    private async Task CreateWorkspaceAsync()
    {
        var trimmed = NewWorkspaceName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ErrorMessage = "워크스페이스 이름을 입력해 주세요.";
            return;
        }

        await RunGuardedAsync(async () =>
        {
            await _workspaceContext.CreateWorkspaceAsync(trimmed);
            NewWorkspaceName = string.Empty;
        });
    }

    [RelayCommand]
    private async Task SelectWorkspaceAsync(object? item)
    {
        var workspace = item switch
        {
            WorkspaceDto ws => ws,
            WorkspaceRowViewModel row => row.Dto,
            _ => null
        };
        if (workspace is null)
        {
            return;
        }

        await RunGuardedAsync(() => _workspaceContext.SelectWorkspaceAsync(workspace.Id));
    }

    [RelayCommand]
    private async Task DeleteWorkspaceAsync(object? item)
    {
        var workspace = item switch
        {
            WorkspaceDto ws => ws,
            WorkspaceRowViewModel row => row.Dto,
            _ => null
        };
        if (workspace is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"'{workspace.Name}' 워크스페이스를 삭제하시겠습니까?\n포함된 문서와 대화 기록도 함께 삭제됩니다.",
            "워크스페이스 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await RunGuardedAsync(() => _workspaceContext.DeleteWorkspaceAsync(workspace.Id));
    }

    private void SyncFromContext()
    {
        Workspaces.Clear();
        foreach (var ws in _workspaceContext.Workspaces)
        {
            Workspaces.Add(ws);
        }

        ActiveWorkspace = _workspaceContext.SelectedWorkspace;
        RebuildFiltered();
    }

    private void RebuildFiltered()
    {
        var q = SearchQuery.Trim();
        var activeId = ActiveWorkspace?.Id;
        var activeDocCount = _workspaceContext.Documents.Count;

        FilteredWorkspaces.Clear();
        foreach (var ws in Workspaces)
        {
            var isActive = activeId == ws.Id;
            if (ShowCurrentOnly && !isActive)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(q) &&
                !ws.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var subtitle = isActive
                ? $"현재 활성 워크스페이스 · 문서 {activeDocCount}개"
                : "워크스페이스";
            FilteredWorkspaces.Add(new WorkspaceRowViewModel(ws, isActive, subtitle));
        }
    }

    private async Task RunGuardedAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
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
