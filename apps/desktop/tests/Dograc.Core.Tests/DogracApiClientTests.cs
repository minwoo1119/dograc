using System.Net;
using System.Text;
using Dograc.Core.Models;
using Dograc.Infrastructure.Api;
using Xunit;

namespace Dograc.Core.Tests;

public sealed class DogracApiClientTests
{
    [Fact]
    public async Task GetWorkspacesAsync_SendsUserIdHeader_AndDeserializesSnakeCase()
    {
        HttpRequestMessage? captured = null;
        var json = """
        [
          {
            "id": "11111111-1111-1111-1111-111111111111",
            "name": "사내 규정",
            "created_at": "2026-11-25T00:00:00Z",
            "updated_at": "2026-11-25T01:00:00Z"
          }
        ]
        """;

        var client = CreateClient((req, _) =>
        {
            captured = req;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, json));
        });

        var workspaces = await client.GetWorkspacesAsync();

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal("/api/v1/workspaces", captured.RequestUri!.AbsolutePath);
        Assert.Equal(DogracApiClient.DefaultLocalUserId, captured.Headers.GetValues("X-User-ID").Single());

        var ws = Assert.Single(workspaces);
        Assert.Equal("사내 규정", ws.Name);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), ws.Id);
    }

    [Fact]
    public async Task UploadDocumentAsync_SendsMultipartFormData_AndParsesDocumentStatus()
    {
        string? capturedContentType = null;
        string? capturedPath = null;
        var wsId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var responseJson = $$"""
        {
          "id": "33333333-3333-3333-3333-333333333333",
          "workspace_id": "{{wsId}}",
          "source_file_name": "policy.pdf",
          "media_type": "application/pdf",
          "status": "uploaded",
          "failure_code": null,
          "created_at": "2026-11-25T00:00:00Z",
          "updated_at": "2026-11-25T00:00:00Z"
        }
        """;

        var client = CreateClient((req, _) =>
        {
            capturedPath = req.RequestUri!.AbsolutePath;
            capturedContentType = req.Content!.Headers.ContentType!.MediaType;
            return Task.FromResult(JsonResponse(HttpStatusCode.Created, responseJson));
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 sample"));
        var doc = await client.UploadDocumentAsync(wsId, "policy.pdf", stream);

        Assert.Equal($"/api/v1/workspaces/{wsId}/documents", capturedPath);
        Assert.Equal("multipart/form-data", capturedContentType);
        Assert.Equal("policy.pdf", doc.SourceFileName);
        Assert.Equal("대기 중", doc.StatusLabel);
        Assert.True(doc.CanTriggerProcess);
    }

    [Fact]
    public async Task ErrorResponse_ParsesStructuredFastApiErrorIntoApiException()
    {
        var errorJson = """
        {
          "error": {
            "code": "WORKSPACE_NOT_FOUND",
            "message": "워크스페이스를 찾을 수 없습니다."
          }
        }
        """;

        var client = CreateClient((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.NotFound, errorJson)));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            client.DeleteWorkspaceAsync(Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("WORKSPACE_NOT_FOUND", ex.ErrorCode);
        Assert.Equal("워크스페이스를 찾을 수 없습니다.", ex.Message);
    }

    [Fact]
    public async Task SendMessageAsync_SendsModelOptions_AndDeserializesTraceId()
    {
        string? capturedBody = null;
        var convId = Guid.NewGuid();
        var traceId = Guid.NewGuid();
        var responseJson = $$"""
        {
          "id": "{{Guid.NewGuid()}}",
          "conversation_id": "{{convId}}",
          "role": "assistant",
          "content": "근거 답변입니다 [policy.pdf, p.2].",
          "created_at": "2026-11-25T02:00:00Z",
          "trace_id": "{{traceId}}"
        }
        """;

        var client = CreateClient(async (req, ct) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(ct);
            return JsonResponse(HttpStatusCode.Created, responseJson);
        });

        var reply = await client.SendMessageAsync(
            convId,
            "보안 규정은?",
            new SendMessageOptions("qwen2.5:7b", "http://localhost:11434/v1"));

        Assert.NotNull(capturedBody);
        Assert.Contains("qwen2.5:7b", capturedBody!);
        Assert.Contains("http://localhost:11434/v1", capturedBody!);
        Assert.Equal(traceId, reply.TraceId);
        Assert.Equal("assistant", reply.Role);
    }

    [Fact]
    public async Task GetTraceAsync_DeserializesModelLatencyAndChunks()
    {
        var traceId = Guid.NewGuid();
        var responseJson = $$"""
        {
          "id": "{{traceId}}",
          "message_id": "{{Guid.NewGuid()}}",
          "workspace_id": "{{Guid.NewGuid()}}",
          "model_name": "qwen2.5:7b",
          "latency_ms": 842,
          "prompt": "SYSTEM PROMPT",
          "created_at": "2026-11-25T02:00:00Z",
          "retrieved_chunks": [
            {
              "chunk_id": "chunk-1",
              "document_id": "doc-1",
              "source_file_name": "policy.pdf",
              "page_number": 2,
              "score": 0.912,
              "text": "외부 반출 금지 조항"
            }
          ]
        }
        """;

        var client = CreateClient((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, responseJson)));

        var trace = await client.GetTraceAsync(traceId);

        Assert.Equal("qwen2.5:7b", trace.ModelName);
        Assert.Equal(842, trace.LatencyMs);
        var chunk = Assert.Single(trace.RetrievedChunks);
        Assert.Equal("policy.pdf", chunk.SourceFileName);
        Assert.Equal(2, chunk.PageNumber);
        Assert.Equal(0.912, chunk.Score, 3);
    }

    private static DogracApiClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://127.0.0.1:8000/api/v1/")
        };
        return new DogracApiClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode code, string json)
        => new(code)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
