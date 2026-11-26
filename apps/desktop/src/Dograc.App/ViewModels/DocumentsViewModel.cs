using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Microsoft.Win32;

namespace Dograc.App.ViewModels;

public sealed partial class DocumentRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public DocumentRowViewModel(DocumentDto dto, Action onSelectionChanged)
    {
        Dto = dto;
        _onSelectionChanged = onSelectionChanged;
    }

    private readonly Action _onSelectionChanged;

    public DocumentDto Dto { get; }
    public Guid Id => Dto.Id;
    public string SourceFileName => Dto.SourceFileName;
    public string Status => Dto.Status;
    public string StatusLabel => Dto.StatusLabel;
    public string? FailureCode => Dto.FailureCode;
    public string? FailureStageLabel => Dto.FailureStageLabel;
    public bool IsFailed => Dto.IsFailed;
    public bool CanTriggerProcess => Dto.CanTriggerProcess;
    public DateTimeOffset CreatedAt => Dto.CreatedAt;
    public long SizeBytes => Dto.SizeBytes ?? -1;
    public string SizeLabel => Dto.SizeLabel;
    public string PageCountLabel => Dto.PageCountLabel;
    public string ChunkCountLabel => Dto.ChunkCountLabel;
    public string UploadedAtLabel => Dto.UploadedAtLabel;

    public string FileFormat
    {
        get
        {
            var ext = Path.GetExtension(Dto.SourceFileName).TrimStart('.');
            return string.IsNullOrWhiteSpace(ext) ? "FILE" : ext.ToUpperInvariant();
        }
    }

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged();
}

/// <summary>
/// Manages document listing with real backend metadata, search, sorting, multi-select batch actions,
/// drag-and-drop/file-picker upload, stage-aware error diagnostics (no duplicate Toast), and deletion.
/// </summary>
public sealed partial class DocumentsViewModel : PageViewModelBase
{
    private readonly IWorkspaceContext _workspaceContext;
    private readonly IErrorHandlingService? _errorHandling;
    private readonly IRecoveryActionService? _recoveryService;
    private readonly IErrorMapper? _errorMapper;
    private readonly IServiceManager? _serviceManager;
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
    private string _sortColumn = "UploadedAt";

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectionSummaryLabel))]
    [NotifyPropertyChangedFor(nameof(AreAllSelected))]
    private int _selectedCount;

    public bool AreAllSelected => FilteredDocuments.Count > 0 && SelectedCount == FilteredDocuments.Count;

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
        IRecoveryActionService? recoveryService = null,
        IErrorMapper? errorMapper = null,
        IServiceManager? serviceManager = null)
    {
        _workspaceContext = workspaceContext;
        _errorHandling = errorHandling;
        _recoveryService = recoveryService;
        _errorMapper = errorMapper;
        _serviceManager = serviceManager;

        Documents = new ObservableCollection<DocumentDto>(_workspaceContext.Documents);
        FilteredDocuments = new ObservableCollection<DocumentRowViewModel>();
        SyncFromContext();

        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromContext);
    }

    public override string PageTitle => "Documents";
    public override string PageDescription => "워크스페이스 문서와 색인 상태를 관리합니다.";

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedCount > 0;
    public string SelectionSummaryLabel => $"{SelectedCount}개 선택됨";

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
    private void SortBy(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            return;
        }

        if (string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase))
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = column is "UploadedAt" or "Size";
        }

        RebuildFiltered();
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var shouldSelect = SelectedCount < FilteredDocuments.Count;
        foreach (var row in FilteredDocuments)
        {
            row.IsSelected = shouldSelect;
        }

        UpdateSelectionCount();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in FilteredDocuments)
        {
            row.IsSelected = false;
        }

        UpdateSelectionCount();
    }

    [RelayCommand]
    private async Task ReindexSelectedAsync()
    {
        var targets = FilteredDocuments.Where(r => r.IsSelected).Select(r => r.Dto).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        await RunGuardedAsync("선택 문서 재색인", async ct =>
        {
            foreach (var doc in targets)
            {
                ct.ThrowIfCancellationRequested();
                StatusMessage = $"'{doc.SourceFileName}' 재색인 중...";
                await _workspaceContext.ProcessDocumentAsync(doc.Id, ct);
            }

            StatusMessage = $"선택한 {targets.Count}개 문서의 색인이 완료되었습니다.";
        });
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var targets = FilteredDocuments.Where(r => r.IsSelected).Select(r => r.Dto).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"선택한 {targets.Count}개 문서를 삭제하시겠습니까?",
            "선택 문서 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await RunGuardedAsync("선택 문서 삭제", async ct =>
        {
            foreach (var doc in targets)
            {
                ct.ThrowIfCancellationRequested();
                await _workspaceContext.DeleteDocumentAsync(doc.Id, ct);
            }

            StatusMessage = $"선택한 {targets.Count}개 문서를 삭제했습니다.";
        });
    }

    [RelayCommand]
    private void InspectDocumentFailure(object? item)
    {
        var doc = item switch
        {
            DocumentDto d => d,
            DocumentRowViewModel r => r.Dto,
            _ => null
        };
        if (doc is null || !doc.IsFailed)
        {
            return;
        }

        _lastRetryCallback = () => ProcessDocumentAsync(doc);
        var ctx = new ErrorContext(
            "Documents", "API", $"문서 색인 ({doc.SourceFileName})",
            PipelineStage: doc.FailureStageLabel);
        var mapped = _errorMapper?.MapDocumentFailureStage(
            doc.FailureCode,
            $"'{doc.SourceFileName}' 문서의 색인이 [{doc.FailureStageLabel ?? "색인 처리"}] 단계에서 실패했습니다.",
            ctx);

        if (mapped is not null)
        {
            ActiveError = mapped;
            ErrorMessage = mapped.Description;
        }
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

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!HasWorkspace)
        {
            return;
        }

        await RunGuardedAsync("문서 목록 새로고침", async ct =>
        {
            await _workspaceContext.RefreshDocumentsAsync(ct);
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

        await RunGuardedAsync("문서 업로드 및 색인", async ct =>
        {
            var successCount = 0;
            foreach (var path in paths)
            {
                ct.ThrowIfCancellationRequested();
                StatusMessage = $"업로드 및 색인 중: {Path.GetFileName(path)}...";
                await _workspaceContext.UploadAndProcessDocumentAsync(path, ct);
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

        await RunGuardedAsync($"문서 색인 ({doc.SourceFileName})", async ct =>
        {
            StatusMessage = $"'{doc.SourceFileName}' 색인 처리 중...";
            await _workspaceContext.ProcessDocumentAsync(doc.Id, ct);
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

        await RunGuardedAsync($"문서 삭제 ({doc.SourceFileName})", async ct =>
        {
            await _workspaceContext.DeleteDocumentAsync(doc.Id, ct);
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
        var selectedIds = FilteredDocuments.Where(r => r.IsSelected).Select(r => r.Id).ToHashSet();
        var q = SearchQuery.Trim();

        IEnumerable<DocumentDto> query = Documents.Where(doc =>
        {
            if (SelectedStatusFilter != "전체 상태")
            {
                var matchesStatus = SelectedStatusFilter switch
                {
                    "색인 완료" => doc.IsReady,
                    "처리 중" => doc.IsProcessing,
                    "대기 중" => string.Equals(doc.Status, "uploaded", StringComparison.OrdinalIgnoreCase),
                    "실패" => doc.IsFailed,
                    _ => string.Equals(doc.StatusLabel, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase)
                };
                if (!matchesStatus)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(q) &&
                !doc.SourceFileName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !doc.StatusLabel.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        });

        query = (SortColumn, SortDescending) switch
        {
            ("Name", false) => query.OrderBy(d => d.SourceFileName, StringComparer.OrdinalIgnoreCase),
            ("Name", true) => query.OrderByDescending(d => d.SourceFileName, StringComparer.OrdinalIgnoreCase),
            ("Size", false) => query.OrderBy(d => d.SizeBytes ?? -1),
            ("Size", true) => query.OrderByDescending(d => d.SizeBytes ?? -1),
            ("Status", false) => query.OrderBy(d => d.Status, StringComparer.OrdinalIgnoreCase),
            ("Status", true) => query.OrderByDescending(d => d.Status, StringComparer.OrdinalIgnoreCase),
            ("UploadedAt", false) => query.OrderBy(d => d.CreatedAt),
            _ => query.OrderByDescending(d => d.CreatedAt)
        };

        FilteredDocuments.Clear();
        foreach (var doc in query)
        {
            var row = new DocumentRowViewModel(doc, UpdateSelectionCount)
            {
                IsSelected = selectedIds.Contains(doc.Id)
            };
            FilteredDocuments.Add(row);
        }

        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        SelectedCount = FilteredDocuments.Count(r => r.IsSelected);
    }

    private async Task RunGuardedAsync(string operationName, Func<CancellationToken, Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();
        var ct = _operationCts.Token;
        _lastRetryCallback = () => RunGuardedAsync(operationName, action);

        IsBusy = true;
        ErrorMessage = null;
        ActiveError = null;
        try
        {
            await action(ct);
        }
        catch (Exception ex)
        {
            if (ex is HttpRequestException && _serviceManager is not null)
            {
                _ = _serviceManager.RefreshStatusesAsync();
            }

            var ctx = new ErrorContext("Documents", "API", operationName);
            // Inline ErrorCard is displayed directly on DocumentsView -> suppress duplicate Toast!
            ActiveError = _errorHandling?.Handle(ex, ctx, _lastRetryCallback, notifyToast: false);
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
