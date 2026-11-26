using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Parsing;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

/// <summary>
/// Display model for a single chat message bubble with parsed citations and Evidence panel commands.
/// </summary>
public sealed class ChatMessageItemViewModel
{
    public ChatMessageItemViewModel(
        MessageDto dto,
        Action<EvidenceSelectionRequest> onEvidenceRequested)
    {
        Id = dto.Id;
        Role = dto.Role;
        Content = dto.Content;
        CreatedAt = dto.CreatedAt;
        TraceId = dto.TraceId;
        IsUser = string.Equals(dto.Role, "user", StringComparison.OrdinalIgnoreCase);
        IsAssistant = !IsUser;
        SenderLabel = IsUser ? "사용자" : "dograc";
        Citations = IsAssistant ? CitationParser.ExtractUniqueCitations(dto.Content) : [];

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
    }

    public Guid Id { get; }
    public string Role { get; }
    public bool IsUser { get; }
    public bool IsAssistant { get; }
    public string SenderLabel { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAt { get; }
    public Guid? TraceId { get; }
    public bool HasTrace => TraceId is not null;
    public IReadOnlyList<CitationReference> Citations { get; }
    public bool HasCitations => Citations.Count > 0;
    public bool ShowAssistantFooter => IsAssistant && (HasCitations || HasTrace);

    public ICommand OpenCitationCommand { get; }
    public ICommand OpenTraceCommand { get; }
}

/// <summary>
/// ViewModel for the central AI Chat Workspace with conversation loading, model selection,
/// optimistic messaging, cancellation (stop), retry/restore, and citation-to-evidence linking.
/// </summary>
public sealed partial class ChatViewModel : PageViewModelBase
{
    private readonly IDogracApiClient _apiClient;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly ISettingsStore _settingsStore;

    private Guid? _loadedConversationId;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _generationCts;
    private bool _isSyncingSettings;

    [ObservableProperty]
    private bool _hasWorkspace;

    [ObservableProperty]
    private string _activeWorkspaceName = "워크스페이스 미선택";

    [ObservableProperty]
    private string _activeConversationTitle = "새 대화 시작";

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _selectedModelName = "qwen2.5:7b";

    [ObservableProperty]
    private string _ollamaEndpointUrl = "http://localhost:11434/v1";

    [ObservableProperty]
    private bool _isLoadingHistory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotGenerating))]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _optimisticUserMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _failedQuestion;

    [ObservableProperty]
    private bool _isEmptyThread = true;

    public ChatViewModel(
        IDogracApiClient apiClient,
        IWorkspaceContext workspaceContext,
        ISettingsStore settingsStore)
    {
        _apiClient = apiClient;
        _workspaceContext = workspaceContext;
        _settingsStore = settingsStore;

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

    public ObservableCollection<string> AvailableModels { get; }

    public ObservableCollection<ChatMessageItemViewModel> Messages { get; } = [];

    public event EventHandler<EvidenceSelectionRequest>? EvidenceRequested;

    public event EventHandler? MessagesUpdated;

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
    private void InsertMarkdownSnippet(string? snippet)
    {
        if (string.IsNullOrEmpty(snippet))
        {
            return;
        }

        InputText = string.IsNullOrEmpty(InputText) ? snippet : $"{InputText}{snippet}";
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
        FailedQuestion = null;
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
        catch (OperationCanceledException)
        {
            OptimisticUserMessage = null;
            FailedQuestion = question;
            ErrorMessage = "응답 생성이 중지되었습니다. 다시 시도하거나 입력창으로 복원할 수 있습니다.";
        }
        catch (Exception ex)
        {
            OptimisticUserMessage = null;
            FailedQuestion = question;
            ErrorMessage = ex.Message;
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
        ActiveConversationTitle = conv?.Title ?? (ws is null ? "워크스페이스를 먼저 선택해 주세요" : "새 대화 (질문 시 자동 생성)");

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
                foreach (var msg in detail.Messages)
                {
                    Messages.Add(new ChatMessageItemViewModel(msg, req => EvidenceRequested?.Invoke(this, req)));
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
