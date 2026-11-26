using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

/// <summary>
/// Manages workspace listing, creation, selection, and deletion against FastAPI.
/// </summary>
public sealed partial class WorkspacesViewModel : PageViewModelBase
{
    private readonly IWorkspaceContext _workspaceContext;

    [ObservableProperty]
    private string _newWorkspaceName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusMessage = "워크스페이스를 생성하거나 선택하여 문서를 분리 관리하세요.";

    [ObservableProperty]
    private WorkspaceDto? _activeWorkspace;

    public WorkspacesViewModel(IWorkspaceContext workspaceContext)
    {
        _workspaceContext = workspaceContext;
        Workspaces = new ObservableCollection<WorkspaceDto>(_workspaceContext.Workspaces);
        _activeWorkspace = _workspaceContext.SelectedWorkspace;

        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromContext);
    }

    public override string PageTitle => "Workspaces";
    public override string PageDescription => "주제별 지식 워크스페이스를 생성, 선택 및 삭제합니다.";

    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<WorkspaceDto> Workspaces { get; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RunGuardedAsync(async () =>
        {
            await _workspaceContext.LoadWorkspacesAsync();
            StatusMessage = $"총 {Workspaces.Count}개의 워크스페이스를 불러왔습니다.";
        });
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
            var created = await _workspaceContext.CreateWorkspaceAsync(trimmed);
            NewWorkspaceName = string.Empty;
            StatusMessage = $"'{created.Name}' 워크스페이스를 생성했습니다.";
        });
    }

    [RelayCommand]
    private async Task SelectWorkspaceAsync(WorkspaceDto? workspace)
    {
        if (workspace is null)
        {
            return;
        }

        await RunGuardedAsync(async () =>
        {
            await _workspaceContext.SelectWorkspaceAsync(workspace.Id);
            StatusMessage = $"'{workspace.Name}' 워크스페이스가 활성화되었습니다.";
        });
    }

    [RelayCommand]
    private async Task DeleteWorkspaceAsync(WorkspaceDto? workspace)
    {
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

        await RunGuardedAsync(async () =>
        {
            await _workspaceContext.DeleteWorkspaceAsync(workspace.Id);
            StatusMessage = $"'{workspace.Name}' 워크스페이스를 삭제했습니다.";
        });
    }

    private void SyncFromContext()
    {
        Workspaces.Clear();
        foreach (var ws in _workspaceContext.Workspaces)
        {
            Workspaces.Add(ws);
        }

        ActiveWorkspace = _workspaceContext.SelectedWorkspace;
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
