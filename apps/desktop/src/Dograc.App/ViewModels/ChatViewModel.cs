using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Parsing;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

/// <summary>
/// Display model for a single chat message with parsed citations, Sources summary, copy/regenerate actions, and Evidence panel linking.
/// </summary>
public sealed class ChatMessageItemViewModel
{
    public ChatMessageItemViewModel(
        MessageDto dto,
        string modelName,
        bool canRegenerate,
        Action<EvidenceSelectionRequest> onEvidenceRequested,
        Action<string> onCopyRequested,
        Action? onRegenerateRequested = null)
    {
        Id = dto.Id;
        Role = dto.Role;
        Content = dto.Content;
        CreatedAt = dto.CreatedAt;
        TraceId = dto.TraceId;
        ModelName = modelName;
        CanRegenerate = canRegenerate;
        IsUser = string.Equals(dto.Role, "user", StringComparison.OrdinalIgnoreCase);
        IsAssistant = !IsUser;
        SenderLabel = IsUser ? "사용자" : "dograc";
        TimestampLabel = dto.CreatedAt.LocalDateTime.ToString("HH:mm");
        Citations = IsAssistant ? CitationParser.ExtractUniqueCitations(dto.Content) : [];

        var distinctDocs = Citations
            .Select(c => c.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        SourcesSummaryLabel = Citations.Count > 0
            ? $"참고 문서 {distinctDocs}개 · 인용 {Citations.Count}개"
            : "답변 근거 확인";

        OpenCitationCommand = new RelayCommand<CitationReference>(citation =>
        {
            if (TraceId is not null && citation is not null)
            {
                onEvidenceRequested(new EvidenceSelectionRequest(TraceId.Value, citation.FileName, citation.PageNumber));
            }
        });

        OpenTraceCommand = new RelayCommand(() =>
        {
            if (TraceId is not null)
            {
                onEvidenceRequested(new EvidenceSelectionRequest(TraceId.Value));
            }
        });

        CopyCommand = new RelayCommand(() => onCopyRequested(Content));

        RegenerateCommand = new RelayCommand(() => onRegenerateRequested?.Invoke(), () => CanRegenerate);
    }

    public Guid Id { get; }
    public string Role { get; }
    public bool IsUser { get; }
    public bool IsAssistant { get; }
    public string SenderLabel { get; }
    public string ModelName { get; }
    public string TimestampLabel { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAt { get; }
    public Guid? TraceId { get; }
    public bool HasTrace => TraceId is not null;
    public IReadOnlyList<CitationReference> Citations { get; }
    public bool HasCitations => Citations.Count > 0;
    public bool ShowAssistantFooter => IsAssistant && (HasCitations || HasTrace);
    public string SourcesSummaryLabel { get; }
    public bool CanRegenerate { get; }

    public ICommand OpenCitationCommand { get; }
    public ICommand OpenTraceCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand RegenerateCommand { get; }
}

/// <summary>
/// ViewModel for the central AI Chat Workspace with conversation loading, model selection,
/// optimistic messaging, cancellation (stop), retry/restore, regenerate, copy, and citation-to-evidence linking.
/// </summary>
public sealed partial class ChatViewModel : PageViewModelBase
{
    private readonly IDogracApiClient _apiClient;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly ISettingsStore _settingsStore;
    private readonly IErrorHandlingService? _errorHandling;
    private readonly IRecoveryActionService? _recoveryService;
    private readonly IServiceManager? _serviceManager;

    private Guid? _loadedConversationId;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _generationCts;
    private bool _isSyncingSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoIndexedDocsHint))]
    [NotifyPropertyChangedFor(nameof(SendButtonTooltip))]
    private bool _hasWorkspace;

    [ObservableProperty]
    private string _activeWorkspaceName = "워크스페이스 미선택";

    [ObservableProperty]
    private string _activeConversationTitle = "새 대화";

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _selectedModelName = "qwen2.5:7b";

    [ObservableProperty]
    private string _ollamaEndpointUrl = "http://localhost:11434/v1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoIndexedDocsHint))]
    private int _indexedDocumentCount;

    [ObservableProperty]
    private string _indexedDocumentsLabel = "색인된 문서 없음";

    [ObservableProperty]
    private bool _isLoadingHistory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotGenerating))]
    [NotifyPropertyChangedFor(nameof(ShowNoIndexedDocsHint))]
    [NotifyPropertyChangedFor(nameof(SendButtonTooltip))]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _optimisticUserMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ApplicationError? _activeError;

    [ObservableProperty]
    private string? _failedQuestion;

    [ObservableProperty]
    private string? _statusNotice;

    [ObservableProperty]
    private bool _isEmptyThread = true;

    public ChatViewModel(
        IDogracApiClient apiClient,
        IWorkspaceContext workspaceContext,
        ISettingsStore settingsStore,
        IErrorHandlingService? errorHandling = null,
        IRecoveryActionService? recoveryService = null,
        IServiceManager? serviceManager = null)
    {
        _apiClient = apiClient;
        _workspaceContext = workspaceContext;
        _settingsStore = settingsStore;
        _errorHandling = errorHandling;
        _recoveryService = recoveryService;
        _serviceManager = serviceManager;

        AvailableModels =
        [
            "qwen2.5:7b",
            "llama3.1:8b",
            "gemma2:9b",
            "deepseek-r1:8b"
        ];

        ApplySettings(_settingsStore.Current);
        _settingsStore.SettingsChanged += (_, updated) => RunOnUiThread(() => ApplySettings(updated));

        SyncFromWorkspaceContext();
        _workspaceContext.StateChanged += (_, _) => RunOnUiThread(SyncFromWorkspaceContext);
    }

    partial void OnSelectedModelNameChanged(string value)
    {
        if (_isSyncingSettings || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (!string.Equals(_settingsStore.Current.SelectedModelName, trimmed, StringComparison.Ordinal))
        {
            _settingsStore.Save(_settingsStore.Current with { SelectedModelName = trimmed });
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        _isSyncingSettings = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.SelectedModelName) &&
                !AvailableModels.Contains(settings.SelectedModelName))
            {
                AvailableModels.Insert(0, settings.SelectedModelName);
            }

            SelectedModelName = settings.SelectedModelName;
            OllamaEndpointUrl = settings.NormalizedOllamaV1Endpoint;
        }
        finally
        {
            _isSyncingSettings = false;
        }
    }

    public override string PageTitle => "AI Chat Workspace";
    public override string PageDescription => "선택한 워크스페이스의 문서를 근거로 질의응답을 수행합니다.";

    public bool IsNotGenerating => !IsGenerating;

    public bool ShowNoIndexedDocsHint => HasWorkspace && IndexedDocumentCount == 0 && !IsGenerating;

    public string SendButtonTooltip => !HasWorkspace
        ? "워크스페이스를 먼저 선택해 주세요"
        : IsGenerating
            ? "답변을 생성하고 있습니다"
            : "전송 (Enter)";

    [RelayCommand]
    private async Task ExecuteErrorActionAsync(RecoveryActionDescriptor? action)
    {
        if (action is null)
        {
            return;
        }

        if (action.Kind == RecoveryActionKind.RestoreInput)
        {
            RestoreFailedInput();
            return;
        }

        if (action.Kind == RecoveryActionKind.Retry)
        {
            await RetryFailedAsync();
            return;
        }

        if (_recoveryService is not null)
        {
            await _recoveryService.ExecuteAsync(action, ActiveError, RetryFailedAsync);
        }
    }

    [RelayCommand]
    private void DismissError()
    {
        ActiveError = null;
        ErrorMessage = null;
    }

    public ObservableCollection<string> AvailableModels { get; }

    public ObservableCollection<ChatMessageItemViewModel> Messages { get; } = [];

    public event EventHandler<EvidenceSelectionRequest>? EvidenceRequested;

    public event EventHandler? MessagesUpdated;

    public event EventHandler? AttachDocumentRequested;

    public event EventHandler? OpenDocumentsTabRequested;

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var question = InputText.Trim();
        if (string.IsNullOrWhiteSpace(question) || IsGenerating || !HasWorkspace)
        {
            return;
        }

        InputText = string.Empty;
        await ExecuteSendAsync(question);
    }

    [RelayCommand]
    private void StopGeneration()
    {
        _generationCts?.Cancel();
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        if (string.IsNullOrWhiteSpace(FailedQuestion) || IsGenerating)
        {
            return;
        }

        var retryText = FailedQuestion;
        await ExecuteSendAsync(retryText);
    }

    [RelayCommand]
    private void RestoreFailedInput()
    {
        if (!string.IsNullOrWhiteSpace(FailedQuestion))
        {
            InputText = FailedQuestion;
            ErrorMessage = null;
            FailedQuestion = null;
        }
    }

    [RelayCommand]
    private void RequestAttachDocument()
    {
        AttachDocumentRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestOpenDocumentsTab()
    {
        OpenDocumentsTabRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void InsertMarkdownSnippet(string? snippet)
    {
        if (string.IsNullOrEmpty(snippet))
        {
            return;
        }

        InputText = string.IsNullOrEmpty(InputText) ? snippet : $"{InputText}{snippet}";
    }

    private void CopyMessageText(string content)
    {
        try
        {
            Clipboard.SetText(content);
            StatusNotice = "답변을 클립보드에 복사했습니다.";
        }
        catch
        {
            StatusNotice = "클립보드 복사에 실패했습니다.";
        }
    }

    private async Task RegenerateLastQuestionAsync()
    {
        if (IsGenerating)
        {
            return;
        }

        var lastUser = Messages.LastOrDefault(m => m.IsUser);
        if (lastUser is null || string.IsNullOrWhiteSpace(lastUser.Content))
        {
            return;
        }

        await ExecuteSendAsync(lastUser.Content);
    }

    private async Task ExecuteSendAsync(string question)
    {
        _generationCts?.Cancel();
        _generationCts?.Dispose();
        _generationCts = new CancellationTokenSource();
        var ct = _generationCts.Token;

        IsGenerating = true;
        OptimisticUserMessage = question;
        ErrorMessage = null;
        ActiveError = null;
        FailedQuestion = null;
        StatusNotice = null;
        UpdateEmptyState();
        MessagesUpdated?.Invoke(this, EventArgs.Empty);

        try
        {
            var conversation = _workspaceContext.SelectedConversation;
            if (conversation is null)
            {
                conversation = await _workspaceContext.CreateConversationAsync(null, ct);
                _loadedConversationId = conversation.Id;
            }

            var options = new SendMessageOptions(
                ModelName: string.IsNullOrWhiteSpace(SelectedModelName) ? null : SelectedModelName.Trim(),
                EndpointUrl: string.IsNullOrWhiteSpace(OllamaEndpointUrl) ? null : OllamaEndpointUrl.Trim());

            var assistantReply = await _apiClient.SendMessageAsync(conversation.Id, question, options, ct);
            await LoadConversationMessagesAsync(conversation.Id, ct);

            OptimisticUserMessage = null;
            if (assistantReply.TraceId is not null)
            {
                EvidenceRequested?.Invoke(this, new EvidenceSelectionRequest(assistantReply.TraceId.Value));
            }
        }
        catch (OperationCanceledException oce)
        {
            OptimisticUserMessage = null;
            FailedQuestion = question;
            var ctx = new ErrorContext("Chat", "Ollama", "답변 생성", Metadata: new Dictionary<string, string> { ["timeout_seconds"] = "120" });
            ActiveError = _errorHandling?.Handle(oce, ctx, RetryFailedAsync, notifyToast: false)
                ?? new ApplicationError(
                    "CHAT-002", ErrorSeverity.Information, ErrorCategory.RagChat,
                    "답변 생성이 중지되었습니다", "사용자의 요청으로 응답 생성을 중단했습니다.",
                    "사용자가 중지 버튼을 눌렀습니다.", true,
                    "입력하신 질문은 안전하게 보존되었습니다.",
                    "[다시 시도] 또는 [입력창 복원]으로 이어서 질문할 수 있습니다.",
                    [new(RecoveryActionKind.Retry, "다시 시도", true), new(RecoveryActionKind.RestoreInput, "입력창 복원")],
                    oce.Message, DateTimeOffset.Now, ctx.ResolvedCorrelationId, ctx, IsRetryable: true);
            ErrorMessage = ActiveError.Description;
        }
        catch (Exception ex)
        {
            OptimisticUserMessage = null;
            FailedQuestion = question;
            if (ex is HttpRequestException && _serviceManager is not null)
            {
                _ = _serviceManager.RefreshStatusesAsync();
            }

            var ctx = new ErrorContext("Chat", "API", "답변 생성");
            // Inline ErrorCard is shown directly inside ChatView -> suppress duplicate Toast!
            ActiveError = _errorHandling?.Handle(ex, ctx, RetryFailedAsync, notifyToast: false);
            ErrorMessage = ActiveError?.Description ?? ex.Message;
        }
        finally
        {
            IsGenerating = false;
            UpdateEmptyState();
            MessagesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SyncFromWorkspaceContext()
    {
        var ws = _workspaceContext.SelectedWorkspace;
        var conv = _workspaceContext.SelectedConversation;

        HasWorkspace = ws is not null;
        ActiveWorkspaceName = ws?.Name ?? "워크스페이스 미선택";
        ActiveConversationTitle = conv?.Title ?? (ws is null ? "워크스페이스를 먼저 선택해 주세요" : "새 대화");

        var readyDocs = _workspaceContext.Documents.Count(d => d.IsReady);
        IndexedDocumentCount = readyDocs;
        IndexedDocumentsLabel = readyDocs > 0
            ? $"문서 {readyDocs}개 색인 완료"
            : _workspaceContext.Documents.Count > 0
                ? $"문서 {_workspaceContext.Documents.Count}개 등록됨"
                : "색인된 문서 없음";

        if (conv is null)
        {
            _loadedConversationId = null;
            Messages.Clear();
            ErrorMessage = null;
            FailedQuestion = null;
            UpdateEmptyState();
            return;
        }

        if (_loadedConversationId != conv.Id)
        {
            _loadedConversationId = conv.Id;
            _ = LoadConversationMessagesAsync(conv.Id);
        }
    }

    private async Task LoadConversationMessagesAsync(Guid conversationId, CancellationToken externalCt = default)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _loadCts.Token;

        IsLoadingHistory = true;
        try
        {
            var detail = await _apiClient.GetConversationDetailAsync(conversationId, ct);
            RunOnUiThread(() =>
            {
                if (_workspaceContext.SelectedConversation?.Id != conversationId)
                {
                    return;
                }

                ActiveConversationTitle = detail.Title;
                Messages.Clear();
                var list = detail.Messages.ToList();
                var lastAssistantIndex = list.FindLastIndex(m => !string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
                for (var idx = 0; idx < list.Count; idx++)
                {
                    var msg = list[idx];
                    var canRegen = idx == lastAssistantIndex;
                    Messages.Add(new ChatMessageItemViewModel(
                        msg,
                        SelectedModelName,
                        canRegen,
                        req => EvidenceRequested?.Invoke(this, req),
                        CopyMessageText,
                        () => _ = RegenerateLastQuestionAsync()));
                }

                UpdateEmptyState();
                MessagesUpdated?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (OperationCanceledException)
        {
            // Ignore superseded load.
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => ErrorMessage = $"대화 내역을 불러오지 못했습니다: {ex.Message}");
        }
        finally
        {
            RunOnUiThread(() => IsLoadingHistory = false);
        }
    }

    private void UpdateEmptyState()
    {
        IsEmptyThread = Messages.Count == 0 && string.IsNullOrWhiteSpace(OptimisticUserMessage);
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
