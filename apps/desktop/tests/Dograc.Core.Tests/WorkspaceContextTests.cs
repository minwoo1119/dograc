using Dograc.Core.Models;
using Dograc.Core.Services;
using Dograc.Infrastructure.State;
using Xunit;

namespace Dograc.Core.Tests;

public sealed class WorkspaceContextTests
{
    [Fact]
    public async Task LoadWorkspacesAsync_AutoSelectsFirstWorkspace_AndLoadsChildren()
    {
        var ws1 = new WorkspaceDto(Guid.NewGuid(), "연구 자료", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var doc1 = new DocumentDto(Guid.NewGuid(), ws1.Id, "paper.pdf", "application/pdf", "ready", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var conv1 = new ConversationDto(Guid.NewGuid(), ws1.Id, "첫 번째 대화", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var api = new FakeApiClient();
        api.Workspaces.Add(ws1);
        api.Documents[ws1.Id] = [doc1];
        api.Conversations[ws1.Id] = [conv1];

        var context = new WorkspaceContext(api);
        await context.LoadWorkspacesAsync();

        Assert.Equal(ws1.Id, context.SelectedWorkspace?.Id);
        Assert.Single(context.Documents);
        Assert.Single(context.Conversations);
        Assert.Equal(conv1.Id, context.SelectedConversation?.Id);
    }

    [Fact]
    public async Task UploadAndProcessDocumentAsync_RejectsUnsupportedExtension()
    {
        var ws = new WorkspaceDto(Guid.NewGuid(), "테스트", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var api = new FakeApiClient();
        api.Workspaces.Add(ws);

        var context = new WorkspaceContext(api);
        await context.LoadWorkspacesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.UploadAndProcessDocumentAsync("report.xlsx"));

        Assert.Contains("PDF", ex.Message);
    }

    [Fact]
    public async Task CreateAndDeleteConversation_UpdatesContextAndSelection()
    {
        var ws = new WorkspaceDto(Guid.NewGuid(), "테스트", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var api = new FakeApiClient();
        api.Workspaces.Add(ws);

        var context = new WorkspaceContext(api);
        await context.LoadWorkspacesAsync();

        var created = await context.CreateConversationAsync("신규 질문");
        Assert.Equal(created.Id, context.SelectedConversation?.Id);
        Assert.Single(context.Conversations);

        await context.DeleteConversationAsync(created.Id);
        Assert.Empty(context.Conversations);
        Assert.Null(context.SelectedConversation);
    }

    private sealed class FakeApiClient : IDogracApiClient
    {
        public string UserId => "test-user";
        public List<WorkspaceDto> Workspaces { get; } = [];
        public Dictionary<Guid, List<DocumentDto>> Documents { get; } = [];
        public Dictionary<Guid, List<ConversationDto>> Conversations { get; } = [];

        public Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkspaceDto>>(Workspaces.ToList());

        public Task<WorkspaceDto> CreateWorkspaceAsync(string name, CancellationToken ct = default)
        {
            var ws = new WorkspaceDto(Guid.NewGuid(), name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            Workspaces.Add(ws);
            return Task.FromResult(ws);
        }

        public Task DeleteWorkspaceAsync(Guid workspaceId, CancellationToken ct = default)
        {
            Workspaces.RemoveAll(w => w.Id == workspaceId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid workspaceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentDto>>(Documents.TryGetValue(workspaceId, out var list) ? list.ToList() : []);

        public Task<DocumentDto> UploadDocumentAsync(Guid workspaceId, string filePath, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<DocumentDto> UploadDocumentAsync(Guid workspaceId, string fileName, Stream fileStream, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<DocumentDto> ProcessDocumentAsync(Guid documentId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid workspaceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ConversationDto>>(Conversations.TryGetValue(workspaceId, out var list) ? list.ToList() : []);

        public Task<ConversationDto> CreateConversationAsync(Guid workspaceId, string? title = null, CancellationToken ct = default)
        {
            var conv = new ConversationDto(Guid.NewGuid(), workspaceId, title ?? "새 대화", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            if (!Conversations.ContainsKey(workspaceId))
            {
                Conversations[workspaceId] = [];
            }
            Conversations[workspaceId].Insert(0, conv);
            return Task.FromResult(conv);
        }

        public Task<ConversationDetailDto> GetConversationDetailAsync(Guid conversationId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
        {
            foreach (var list in Conversations.Values)
            {
                list.RemoveAll(c => c.Id == conversationId);
            }
            return Task.CompletedTask;
        }

        public Task<MessageDto> SendMessageAsync(Guid conversationId, string content, SendMessageOptions? options = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<TraceDto> GetTraceAsync(Guid traceId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
