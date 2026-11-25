using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.State;

/// <summary>
/// Centralized workspace, document, and conversation state synchronized via IDogracApiClient.
/// </summary>
public sealed class WorkspaceContext : IWorkspaceContext
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".txt"
    };

    private readonly IDogracApiClient _apiClient;
    private readonly object _lock = new();

    private List<WorkspaceDto> _workspaces = [];
    private WorkspaceDto? _selectedWorkspace;
    private List<DocumentDto> _documents = [];
    private List<ConversationDto> _conversations = [];
    private ConversationDto? _selectedConversation;

    public WorkspaceContext(IDogracApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<WorkspaceDto> Workspaces
    {
        get { lock (_lock) return _workspaces.ToList(); }
    }

    public WorkspaceDto? SelectedWorkspace
    {
        get { lock (_lock) return _selectedWorkspace; }
    }

    public IReadOnlyList<DocumentDto> Documents
    {
        get { lock (_lock) return _documents.ToList(); }
    }

    public IReadOnlyList<ConversationDto> Conversations
    {
        get { lock (_lock) return _conversations.ToList(); }
    }

    public ConversationDto? SelectedConversation
    {
        get { lock (_lock) return _selectedConversation; }
    }

    public event EventHandler? StateChanged;

    public async Task LoadWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var list = (await _apiClient.GetWorkspacesAsync(cancellationToken)).ToList();

        Guid? targetWorkspaceId;
        lock (_lock)
        {
            _workspaces = list;
            if (_selectedWorkspace is not null && list.Any(w => w.Id == _selectedWorkspace.Id))
            {
                _selectedWorkspace = list.First(w => w.Id == _selectedWorkspace.Id);
            }
            else
            {
                _selectedWorkspace = list.FirstOrDefault();
            }

            targetWorkspaceId = _selectedWorkspace?.Id;
        }

        if (targetWorkspaceId is not null)
        {
            await LoadWorkspaceChildrenAsync(targetWorkspaceId.Value, cancellationToken);
        }
        else
        {
            lock (_lock)
            {
                _documents = [];
                _conversations = [];
                _selectedConversation = null;
            }
        }

        NotifyChanged();
    }

    public async Task SelectWorkspaceAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        WorkspaceDto? selected;
        lock (_lock)
        {
            selected = workspaceId is null
                ? null
                : _workspaces.FirstOrDefault(w => w.Id == workspaceId.Value);

            if (_selectedWorkspace?.Id == selected?.Id && selected is not null)
            {
                return;
            }

            _selectedWorkspace = selected;
            _selectedConversation = null;
        }

        if (selected is null)
        {
            lock (_lock)
            {
                _documents = [];
                _conversations = [];
            }

            NotifyChanged();
            return;
        }

        await LoadWorkspaceChildrenAsync(selected.Id, cancellationToken);
        NotifyChanged();
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(string name, CancellationToken cancellationToken = default)
    {
        var created = await _apiClient.CreateWorkspaceAsync(name.Trim(), cancellationToken);
        lock (_lock)
        {
            _workspaces.Insert(0, created);
            _selectedWorkspace = created;
            _documents = [];
            _conversations = [];
            _selectedConversation = null;
        }

        NotifyChanged();
        return created;
    }

    public async Task DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await _apiClient.DeleteWorkspaceAsync(workspaceId, cancellationToken);

        Guid? nextWorkspaceId = null;
        lock (_lock)
        {
            _workspaces.RemoveAll(w => w.Id == workspaceId);
            if (_selectedWorkspace?.Id == workspaceId)
            {
                _selectedWorkspace = _workspaces.FirstOrDefault();
                _selectedConversation = null;
                nextWorkspaceId = _selectedWorkspace?.Id;
                if (nextWorkspaceId is null)
                {
                    _documents = [];
                    _conversations = [];
                }
            }
        }

        if (nextWorkspaceId is not null)
        {
            await LoadWorkspaceChildrenAsync(nextWorkspaceId.Value, cancellationToken);
        }

        NotifyChanged();
    }

    public async Task RefreshDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var workspaceId = SelectedWorkspace?.Id;
        if (workspaceId is null)
        {
            return;
        }

        var docs = (await _apiClient.GetDocumentsAsync(workspaceId.Value, cancellationToken)).ToList();
        lock (_lock)
        {
            _documents = docs;
        }

        NotifyChanged();
    }

    public async Task<DocumentDto> UploadAndProcessDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var workspaceId = SelectedWorkspace?.Id
            ?? throw new InvalidOperationException("워크스페이스를 먼저 선택해 주세요.");

        var ext = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(ext))
        {
            throw new InvalidOperationException("현재는 PDF 및 TXT 문서만 업로드할 수 있습니다.");
        }

        var uploaded = await _apiClient.UploadDocumentAsync(workspaceId, filePath, cancellationToken);
        await RefreshDocumentsAsync(cancellationToken);

        try
        {
            var processed = await _apiClient.ProcessDocumentAsync(uploaded.Id, cancellationToken);
            await RefreshDocumentsAsync(cancellationToken);
            return processed;
        }
        catch
        {
            await RefreshDocumentsAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DocumentDto> ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var processed = await _apiClient.ProcessDocumentAsync(documentId, cancellationToken);
        await RefreshDocumentsAsync(cancellationToken);
        return processed;
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _apiClient.DeleteDocumentAsync(documentId, cancellationToken);
        await RefreshDocumentsAsync(cancellationToken);
    }

    public async Task RefreshConversationsAsync(CancellationToken cancellationToken = default)
    {
        var workspaceId = SelectedWorkspace?.Id;
        if (workspaceId is null)
        {
            return;
        }

        var convs = (await _apiClient.GetConversationsAsync(workspaceId.Value, cancellationToken)).ToList();
        lock (_lock)
        {
            _conversations = convs;
            if (_selectedConversation is not null && convs.Any(c => c.Id == _selectedConversation.Id))
            {
                _selectedConversation = convs.First(c => c.Id == _selectedConversation.Id);
            }
            else
            {
                _selectedConversation = convs.FirstOrDefault();
            }
        }

        NotifyChanged();
    }

    public void SelectConversation(Guid? conversationId)
    {
        lock (_lock)
        {
            _selectedConversation = conversationId is null
                ? null
                : _conversations.FirstOrDefault(c => c.Id == conversationId.Value);
        }

        NotifyChanged();
    }

    public async Task<ConversationDto> CreateConversationAsync(string? title = null, CancellationToken cancellationToken = default)
    {
        var workspaceId = SelectedWorkspace?.Id
            ?? throw new InvalidOperationException("워크스페이스를 먼저 선택해 주세요.");

        var created = await _apiClient.CreateConversationAsync(workspaceId, title, cancellationToken);
        lock (_lock)
        {
            _conversations.Insert(0, created);
            _selectedConversation = created;
        }

        NotifyChanged();
        return created;
    }

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await _apiClient.DeleteConversationAsync(conversationId, cancellationToken);
        lock (_lock)
        {
            _conversations.RemoveAll(c => c.Id == conversationId);
            if (_selectedConversation?.Id == conversationId)
            {
                _selectedConversation = _conversations.FirstOrDefault();
            }
        }

        NotifyChanged();
    }

    private async Task LoadWorkspaceChildrenAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var docsTask = _apiClient.GetDocumentsAsync(workspaceId, cancellationToken);
        var convsTask = _apiClient.GetConversationsAsync(workspaceId, cancellationToken);
        await Task.WhenAll(docsTask, convsTask);

        lock (_lock)
        {
            _documents = docsTask.Result.ToList();
            _conversations = convsTask.Result.ToList();
            _selectedConversation = _conversations.FirstOrDefault();
        }
    }

    private void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
