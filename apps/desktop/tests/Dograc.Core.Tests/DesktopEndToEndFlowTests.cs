using System.Net;
using System.Text;
using System.Text.Json;
using Dograc.Core.Models;
using Dograc.Core.Navigation;
using Dograc.Core.Parsing;
using Dograc.Core.Services;
using Dograc.Infrastructure.Api;
using Dograc.Infrastructure.DependencyInjection;
using Dograc.Infrastructure.Settings;
using Dograc.Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dograc.Core.Tests;

public sealed class DesktopEndToEndFlowTests
{
    [Fact]
    public void AddDogracInfrastructure_ResolvesAllCoreDesktopServices()
    {
        var services = new ServiceCollection();
        services.AddDogracInfrastructure();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<INavigationService>());
        Assert.NotNull(provider.GetRequiredService<IProcessRunner>());
        Assert.NotNull(provider.GetRequiredService<IHealthProbe>());
        Assert.NotNull(provider.GetRequiredService<IServiceManager>());
        Assert.NotNull(provider.GetRequiredService<IStartupOrchestrator>());
        Assert.NotNull(provider.GetRequiredService<ISettingsStore>());
        Assert.NotNull(provider.GetRequiredService<IDogracApiClient>());
        Assert.NotNull(provider.GetRequiredService<IOllamaService>());
        Assert.NotNull(provider.GetRequiredService<IUpdateService>());
        Assert.NotNull(provider.GetRequiredService<IWorkspaceContext>());
    }

    [Fact]
    public async Task FullRagWorkflow_WorkspaceUploadIndexChatCitationAndTrace_SucceedsEndToEnd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dograc-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var settingsPath = Path.Combine(tempDir, "settings.json");
        var pdfPath = Path.Combine(tempDir, "security_policy.pdf");
        await File.WriteAllTextAsync(pdfPath, "%PDF-1.4 mock security policy");

        try
        {
            var fakeServer = new InMemoryFastApiHandler();
            using var httpClient = new HttpClient(fakeServer)
            {
                BaseAddress = new Uri("http://127.0.0.1:8000/api/v1/")
            };

            var apiClient = new DogracApiClient(httpClient);
            var settingsStore = new JsonSettingsStore(settingsPath);
            var workspaceContext = new WorkspaceContext(apiClient, settingsStore);

            // 1. Create workspace
            var ws = await workspaceContext.CreateWorkspaceAsync("보안 감사");
            Assert.Equal(ws.Id, workspaceContext.SelectedWorkspace?.Id);

            // 2. Upload and auto-index PDF (verifies local path caching for Preview tab)
            var processedDoc = await workspaceContext.UploadAndProcessDocumentAsync(pdfPath);
            Assert.Equal("ready", processedDoc.Status);
            Assert.Single(workspaceContext.Documents);
            Assert.Equal(Path.GetFullPath(pdfPath), settingsStore.ResolveDocumentLocalPath("security_policy.pdf"));

            // 3. Create conversation and send RAG question
            var conv = await workspaceContext.CreateConversationAsync("반출 규정 질문");
            var reply = await apiClient.SendMessageAsync(
                conv.Id,
                "문서 반출 규정이 어떻게 되나요?",
                new SendMessageOptions("qwen2.5:7b", "http://localhost:11434/v1"));

            Assert.NotNull(reply.TraceId);

            // 4. Parse inline citations & fetch Trace evidence
            var citations = CitationParser.ExtractUniqueCitations(reply.Content);
            var citation = Assert.Single(citations);
            Assert.Equal("security_policy.pdf", citation.FileName);
            Assert.Equal(4, citation.PageNumber);

            var trace = await apiClient.GetTraceAsync(reply.TraceId!.Value);
            Assert.Equal("qwen2.5:7b", trace.ModelName);
            Assert.Single(trace.RetrievedChunks);
            Assert.Equal(4, trace.RetrievedChunks[0].PageNumber);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private sealed class InMemoryFastApiHandler : HttpMessageHandler
    {
        private readonly List<WorkspaceDto> _workspaces = [];
        private readonly List<DocumentDto> _documents = [];
        private readonly List<ConversationDto> _conversations = [];
        private readonly Dictionary<Guid, TraceDto> _traces = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.True(request.Headers.Contains("X-User-ID"));
            var path = request.RequestUri!.AbsolutePath;
            var method = request.Method;

            if (method == HttpMethod.Post && path == "/api/v1/workspaces")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(body);
                var name = doc.RootElement.GetProperty("name").GetString()!;
                var ws = new WorkspaceDto(Guid.NewGuid(), name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                _workspaces.Add(ws);
                return Json(HttpStatusCode.Created, ws);
            }

            if (method == HttpMethod.Get && path.EndsWith("/documents", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, _documents);
            }

            if (method == HttpMethod.Post && path.EndsWith("/documents", StringComparison.Ordinal))
            {
                var wsId = _workspaces.Last().Id;
                var uploaded = new DocumentDto(
                    Guid.NewGuid(), wsId, "security_policy.pdf", "application/pdf",
                    "uploaded", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                _documents.Add(uploaded);
                return Json(HttpStatusCode.Created, uploaded);
            }

            if (method == HttpMethod.Post && path.EndsWith("/process", StringComparison.Ordinal))
            {
                var idx = _documents.Count - 1;
                _documents[idx] = _documents[idx] with { Status = "ready" };
                return Json(HttpStatusCode.OK, _documents[idx]);
            }

            if (method == HttpMethod.Post && path.EndsWith("/conversations", StringComparison.Ordinal))
            {
                var wsId = _workspaces.Last().Id;
                var conv = new ConversationDto(Guid.NewGuid(), wsId, "반출 규정 질문", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                _conversations.Add(conv);
                return Json(HttpStatusCode.Created, conv);
            }

            if (method == HttpMethod.Post && path.EndsWith("/messages", StringComparison.Ordinal))
            {
                var conv = _conversations.Last();
                var msgId = Guid.NewGuid();
                var traceId = Guid.NewGuid();
                _traces[traceId] = new TraceDto(
                    traceId,
                    msgId,
                    conv.WorkspaceId,
                    [new RetrievedChunkDto("c1", _documents.Last().Id.ToString(), "security_policy.pdf", 4, 0.94, "외부 반출 시 사전 승인 필수")],
                    "SYSTEM PROMPT",
                    "qwen2.5:7b",
                    420,
                    DateTimeOffset.UtcNow);

                var reply = new MessageDto(
                    msgId,
                    conv.Id,
                    "assistant",
                    "외부 반출은 사전 승인이 필요합니다 [security_policy.pdf, p.4].",
                    DateTimeOffset.UtcNow,
                    traceId);
                return Json(HttpStatusCode.Created, reply);
            }

            if (method == HttpMethod.Get && path.StartsWith("/api/v1/traces/", StringComparison.Ordinal))
            {
                var id = Guid.Parse(path.Split('/').Last());
                return Json(HttpStatusCode.OK, _traces[id]);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
            => new(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
    }
}
