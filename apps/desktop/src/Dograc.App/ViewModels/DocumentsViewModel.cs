using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Microsoft.Win32;

namespace Dograc.App.ViewModels;

public sealed record DocumentRowViewModel(DocumentDto Dto)
{
    public Guid Id => Dto.Id;
    public string SourceFileName => Dto.SourceFileName;
    public string Status => Dto.Status;
    public string StatusLabel => Dto.StatusLabel;
    public bool CanTriggerProcess => Dto.CanTriggerProcess;
    public DateTimeOffset CreatedAt => Dto.CreatedAt;

    public string FileFormat
    {
        get
        {
            var ext = Path.GetExtension(Dto.SourceFileName).TrimStart('.');
            return string.IsNullOrWhiteSpace(ext) ? "FILE" : ext.ToUpperInvariant();
        }
    }
}

/// <summary>
/// Manages document listing, filtering, drag-and-drop/file-picker upload, indexing, and deletion.
/// Shared by both the Documents page and the right Context Panel Documents tab.
/// </summary>
public sealed partial class DocumentsViewModel : PageViewModelBase
{
    private readonly IWorkspaceContext _workspaceContext;
    private readonly IErrorHandlingService? _errorHandling;
    private readonly IRecoveryActionService? _recoveryService;
    private CancellationTokenSource? _operationCts;
    private Func<Task>? _lastRetryCallback;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasWorkspace;

    [ObservableProperty]
    private string _activeWorkspaceName = "선택된 워크스페이스 없음";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedStatusFilter = "전체 상태";

    [ObservableProperty]
    private int _indexedDocumentCount;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ApplicationError? _activeError;

    [ObservableProperty]
    private string _statusMessage = "PDF 또는 TXT 파일을 끌어다 놓거나 선택하여 업로드하세요.";

    public DocumentsViewModel(
        IWorkspaceContext workspaceContext,
        IErrorHandlingService? errorHandling = null,
        IRecoveryActionService? recoveryService = null)
    {
        _workspaceContext = workspaceContext;
        _errorHandling = errorHandling;
        _recoveryService = recoveryService;
        Documents = new ObservableCollection<DocumentDto>(_workspaceContext.Documents);
        FilteredDocuments = new ObservableCollection<DocumentRowViewModel>();
        SyncFromContext();

        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromContext);
    }

    [RelayCommand]
    private void CancelCurrentOperation()
    {
        _operationCts?.Cancel();
    }

    [RelayCommand]
    private async Task ExecuteErrorActionAsync(RecoveryActionDescriptor? action)
    {
        if (action is null)
        {
            return;
        }

        if (action.Kind == RecoveryActionKind.ReselectFile)
        {
            await BrowseAndUploadAsync();
            return;
        }

        if (action.Kind is RecoveryActionKind.Retry or RecoveryActionKind.ReindexDocument && _lastRetryCallback is not null)
        {
            await _lastRetryCallback();
            return;
        }

        if (_recoveryService is not null)
        {
            await _recoveryService.ExecuteAsync(action, ActiveError, _lastRetryCallback);
        }
    }

    [RelayCommand]
    private void DismissError()
    {
        ActiveError = null;
        ErrorMessage = null;
    }

    public override string PageTitle => "Documents";
    public override string PageDescription => "워크스페이스 문서와 색인 상태를 관리합니다.";

    public bool IsNotBusy => !IsBusy;

    public IReadOnlyList<string> StatusFilters { get; } =
    [
        "전체 상태",
        "색인 완료",
        "처리 중",
        "대기 중",
        "실패"
    ];

    public ObservableCollection<DocumentDto> Documents { get; }

    public ObservableCollection<DocumentRowViewModel> FilteredDocuments { get; }

    public event EventHandler<PreviewDocumentRequest>? PreviewRequested;

    partial void OnSearchQueryChanged(string value) => RebuildFiltered();

    partial void OnSelectedStatusFilterChanged(string value) => RebuildFiltered();

    public void ApplySidebarFilter(string filterLabel)
    {
        SelectedStatusFilter = filterLabel == "전체 문서" ? "전체 상태" : filterLabel;
    }

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
                StatusMessage = $"업로드 및 색인 중: {Path.GetFileName(path)}...";
                await _workspaceContext.UploadAndProcessDocumentAsync(path);
                successCount++;
            }

            StatusMessage = $"{successCount}개 문서의 업로드 및 색인이 완료되었습니다.";
        });
    }

    [RelayCommand]
    private void OpenInPreview(object? item)
    {
        var dto = item switch
        {
            DocumentDto d => d,
            DocumentRowViewModel r => r.Dto,
            _ => null
        };
        if (dto is null)
        {
            return;
        }

        PreviewRequested?.Invoke(this, new PreviewDocumentRequest(dto.SourceFileName, 1));
    }

    [RelayCommand]
    private async Task ProcessDocumentAsync(object? item)
    {
        var doc = item switch
        {
            DocumentDto d => d,
            DocumentRowViewModel r => r.Dto,
            _ => null
        };
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
    private async Task DeleteDocumentAsync(object? item)
    {
        var doc = item switch
        {
            DocumentDto d => d,
            DocumentRowViewModel r => r.Dto,
            _ => null
        };
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

        IndexedDocumentCount = Documents.Count(d => d.IsReady);
        RebuildFiltered();
    }

    private void RebuildFiltered()
    {
        var q = SearchQuery.Trim();
        FilteredDocuments.Clear();
        foreach (var doc in Documents)
        {
            if (SelectedStatusFilter != "전체 상태" &&
                !string.Equals(doc.StatusLabel, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(q) &&
                !doc.SourceFileName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !doc.StatusLabel.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredDocuments.Add(new DocumentRowViewModel(doc));
        }
    }

    private async Task RunGuardedAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();
        _lastRetryCallback = () => RunGuardedAsync(action);

        IsBusy = true;
        ErrorMessage = null;
        ActiveError = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            var ctx = new ErrorContext("Documents", "API", "문서 업로드 및 색인");
            ActiveError = _errorHandling?.Handle(ex, ctx, _lastRetryCallback, notifyToast: true);
            ErrorMessage = ActiveError?.Description ?? ex.Message;
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
