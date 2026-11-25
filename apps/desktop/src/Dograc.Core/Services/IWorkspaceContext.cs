using Dograc.Core.Models;

namespace Dograc.Core.Services;

/// <summary>
/// Shared reactive workspace state across Main Shell Sidebar, Context Panel, and independent pages.
/// </summary>
public interface IWorkspaceContext
{
    IReadOnlyList<WorkspaceDto> Workspaces { get; }
    WorkspaceDto? SelectedWorkspace { get; }

    IReadOnlyList<DocumentDto> Documents { get; }
    IReadOnlyList<ConversationDto> Conversations { get; }
    ConversationDto? SelectedConversation { get; }

    event EventHandler? StateChanged;

    Task LoadWorkspacesAsync(CancellationToken cancellationToken = default);
    Task SelectWorkspaceAsync(Guid? workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceDto> CreateWorkspaceAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    Task RefreshDocumentsAsync(CancellationToken cancellationToken = default);
    Task<DocumentDto> UploadAndProcessDocumentAsync(string filePath, CancellationToken cancellationToken = default);
    Task<DocumentDto> ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task RefreshConversationsAsync(CancellationToken cancellationToken = default);
    void SelectConversation(Guid? conversationId);
    Task<ConversationDto> CreateConversationAsync(string? title = null, CancellationToken cancellationToken = default);
    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
