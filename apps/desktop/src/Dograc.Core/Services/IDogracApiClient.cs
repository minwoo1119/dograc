using Dograc.Core.Models;

namespace Dograc.Core.Services;

/// <summary>
/// Typed REST API client matching the existing FastAPI /api/v1 contract.
/// </summary>
public interface IDogracApiClient
{
    string UserId { get; }

    Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceDto> CreateWorkspaceAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<DocumentDto> UploadDocumentAsync(Guid workspaceId, string filePath, CancellationToken cancellationToken = default);
    Task<DocumentDto> UploadDocumentAsync(Guid workspaceId, string fileName, Stream fileStream, CancellationToken cancellationToken = default);
    Task<DocumentDto> ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<ConversationDto> CreateConversationAsync(Guid workspaceId, string? title = null, CancellationToken cancellationToken = default);
    Task<ConversationDetailDto> GetConversationDetailAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<MessageDto> SendMessageAsync(Guid conversationId, string content, SendMessageOptions? options = null, CancellationToken cancellationToken = default);
    Task<TraceDto> GetTraceAsync(Guid traceId, CancellationToken cancellationToken = default);
}
