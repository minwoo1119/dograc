using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Api;

/// <summary>
/// Typed HTTP client communicating with the existing FastAPI /api/v1 backend.
/// </summary>
public sealed class DogracApiClient : IDogracApiClient
{
    public const string DefaultLocalUserId = "00000000-0000-0000-0000-000000000001";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public DogracApiClient(HttpClient httpClient)
        : this(httpClient, null)
    {
    }

    public DogracApiClient(HttpClient httpClient, string? userId)
    {
        _httpClient = httpClient;
        UserId = string.IsNullOrWhiteSpace(userId) ? DefaultLocalUserId : userId;
    }

    public string UserId { get; }

    public Task<IReadOnlyList<WorkspaceDto>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<WorkspaceDto>>(HttpMethod.Get, "workspaces", null, cancellationToken);

    public Task<WorkspaceDto> CreateWorkspaceAsync(string name, CancellationToken cancellationToken = default)
        => SendAsync<WorkspaceDto>(HttpMethod.Post, "workspaces", new { name }, cancellationToken);

    public Task DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"workspaces/{workspaceId}", cancellationToken);

    public Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<DocumentDto>>(HttpMethod.Get, $"workspaces/{workspaceId}/documents", null, cancellationToken);

    public async Task<DocumentDto> UploadDocumentAsync(Guid workspaceId, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("업로드할 파일을 찾을 수 없습니다.", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        return await UploadDocumentAsync(workspaceId, Path.GetFileName(filePath), stream, cancellationToken);
    }

    public async Task<DocumentDto> UploadDocumentAsync(
        Guid workspaceId,
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        using var formData = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mediaType = ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        formData.Add(streamContent, "file", fileName);

        using var request = CreateRequest(HttpMethod.Post, $"workspaces/{workspaceId}/documents");
        request.Content = formData;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var doc = await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOptions, cancellationToken);
        return doc ?? throw new ApiException((int)response.StatusCode, "문서 업로드 응답을 해석할 수 없습니다.");
    }

    public Task<DocumentDto> ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        => SendAsync<DocumentDto>(HttpMethod.Post, $"documents/{documentId}/process", null, cancellationToken);

    public Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"documents/{documentId}", cancellationToken);

    public Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ConversationDto>>(HttpMethod.Get, $"workspaces/{workspaceId}/conversations", null, cancellationToken);

    public Task<ConversationDto> CreateConversationAsync(Guid workspaceId, string? title = null, CancellationToken cancellationToken = default)
        => SendAsync<ConversationDto>(HttpMethod.Post, $"workspaces/{workspaceId}/conversations", new { title }, cancellationToken);

    public Task<ConversationDetailDto> GetConversationDetailAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => SendAsync<ConversationDetailDto>(HttpMethod.Get, $"conversations/{conversationId}", null, cancellationToken);

    public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"conversations/{conversationId}", cancellationToken);

    public Task<MessageDto> SendMessageAsync(
        Guid conversationId,
        string content,
        SendMessageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            content,
            model_name = options?.ModelName,
            endpoint_url = options?.EndpointUrl
        };
        return SendAsync<MessageDto>(HttpMethod.Post, $"conversations/{conversationId}/messages", payload, cancellationToken);
    }

    public Task<TraceDto> GetTraceAsync(Guid traceId, CancellationToken cancellationToken = default)
        => SendAsync<TraceDto>(HttpMethod.Get, $"traces/{traceId}", null, cancellationToken);

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, relativePath.TrimStart('/'));
        request.Headers.Add("X-User-ID", UserId);
        return request;
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, relativePath);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new ApiException((int)response.StatusCode, "서버 응답 데이터가 비어 있습니다.");
    }

    private async Task SendNoContentAsync(
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, relativePath);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        var errorMessage = $"API 요청 실패 (HTTP {statusCode})";
        var errorCode = $"HTTP_{statusCode}";

        try
        {
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var errorObj) && errorObj.ValueKind == JsonValueKind.Object)
                {
                    if (errorObj.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                    {
                        errorMessage = msgProp.GetString() ?? errorMessage;
                    }
                    if (errorObj.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
                    {
                        errorCode = codeProp.GetString() ?? errorCode;
                    }
                }
                else if (root.TryGetProperty("detail", out var detailProp))
                {
                    errorMessage = detailProp.ValueKind == JsonValueKind.String
                        ? detailProp.GetString() ?? errorMessage
                        : detailProp.GetRawText();
                }
            }
        }
        catch
        {
            // Keep default message if body is non-JSON.
        }

        throw new ApiException(statusCode, errorMessage, errorCode);
    }
}
