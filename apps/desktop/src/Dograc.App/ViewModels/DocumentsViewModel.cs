using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Microsoft.Win32;

namespace Dograc.App.ViewModels;

/// <summary>
/// Manages document listing, drag-and-drop/file-picker upload, indexing, and deletion.
/// Shared by both the Documents page and the right Context Panel Documents tab.
/// </summary>
public sealed partial class DocumentsViewModel : PageViewModelBase
{
    private readonly IWorkspaceContext _workspaceContext;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasWorkspace;

    [ObservableProperty]
    private string _activeWorkspaceName = "선택된 워크스페이스 없음";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusMessage = "PDF 또는 TXT 파일을 끌어다 놓거나 선택하여 업로드하세요.";

    public DocumentsViewModel(IWorkspaceContext workspaceContext)
    {
        _workspaceContext = workspaceContext;
        Documents = new ObservableCollection<DocumentDto>(_workspaceContext.Documents);
        SyncFromContext();

        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromContext);
    }

    public override string PageTitle => "Documents";
    public override string PageDescription => "워크스페이스별 문서를 업로드하고 색인 상태를 관리합니다.";

    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<DocumentDto> Documents { get; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!HasWorkspace)
        {
            return;
        }

        await RunGuardedAsync(async () =>
        {
            await _workspaceContext.RefreshDocumentsAsync();
            StatusMessage = $"문서 {Documents.Count}건을 새로고침했습니다.";
        });
    }

    [RelayCommand]
    private async Task BrowseAndUploadAsync()
    {
        if (!HasWorkspace)
        {
            ErrorMessage = "워크스페이스를 먼저 선택해 주세요.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "업로드할 문서 선택",
            Filter = "지원 문서 (*.pdf;*.txt)|*.pdf;*.txt|PDF 문서 (*.pdf)|*.pdf|텍스트 문서 (*.txt)|*.txt",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        await UploadFilePathsAsync(dialog.FileNames);
    }

    public async Task UploadFilePathsAsync(IEnumerable<string> filePaths)
    {
        var paths = filePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count == 0)
        {
            return;
        }

        await RunGuardedAsync(async () =>
        {
            var successCount = 0;
            foreach (var path in paths)
            {
                StatusMessage = $"업로드 및 색인 중: {System.IO.Path.GetFileName(path)}...";
                await _workspaceContext.UploadAndProcessDocumentAsync(path);
                successCount++;
            }

            StatusMessage = $"{successCount}개 문서의 업로드 및 색인이 완료되었습니다.";
        });
    }

    [RelayCommand]
    private async Task ProcessDocumentAsync(DocumentDto? doc)
    {
        if (doc is null)
        {
            return;
        }

        await RunGuardedAsync(async () =>
        {
            StatusMessage = $"'{doc.SourceFileName}' 색인 처리 중...";
            await _workspaceContext.ProcessDocumentAsync(doc.Id);
            StatusMessage = $"'{doc.SourceFileName}' 색인이 완료되었습니다.";
        });
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync(DocumentDto? doc)
    {
        if (doc is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"'{doc.SourceFileName}' 문서를 삭제하시겠습니까?",
            "문서 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await RunGuardedAsync(async () =>
        {
            await _workspaceContext.DeleteDocumentAsync(doc.Id);
            StatusMessage = $"'{doc.SourceFileName}' 문서를 삭제했습니다.";
        });
    }

    private void SyncFromContext()
    {
        var ws = _workspaceContext.SelectedWorkspace;
        HasWorkspace = ws is not null;
        ActiveWorkspaceName = ws?.Name ?? "선택된 워크스페이스 없음";

        Documents.Clear();
        foreach (var doc in _workspaceContext.Documents)
        {
            Documents.Add(doc);
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
