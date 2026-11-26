using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Dograc.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dograc.Infrastructure.Api;

/// <summary>
/// Typed HTTP client communicating with the FastAPI /api/v1 backend.
/// Propagates X-Correlation-ID, parses structured error stages, and applies bounded retries only to idempotent GET calls.
/// </summary>
public sealed class DogracApiClient : IDogracApiClient
{
    public const string DefaultLocalUserId = "00000000-0000-0000-0000-000000000001";
    private const long MaxUploadSizeBytes = 50L * 1024 * 1024; // 50 MB client-side safeguard

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IStructuredLogger? _logger;

    [ActivatorUtilitiesConstructor]
    public DogracApiClient(HttpClient httpClient, IStructuredLogger? logger = null)
        : this(httpClient, null, logger)
    {
    }

    public DogracApiClient(HttpClient httpClient, string? userId, IStructuredLogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
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

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            throw new ApiException(400, "내용이 비어 있는 파일(0 byte)은 업로드할 수 없습니다.", "EMPTY_FILE", pipelineStage: "upload");
        }

        if (fileInfo.Length > MaxUploadSizeBytes)
        {
            throw new ApiException(413, $"파일 크기({fileInfo.Length / (1024 * 1024)} MB)가 최대 허용 용량(50 MB)을 초과했습니다.", "FILE_TOO_LARGE", pipelineStage: "upload");
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
        var correlationId = Guid.NewGuid().ToString("N")[..12];
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

        using var request = CreateRequest(HttpMethod.Post, $"workspaces/{workspaceId}/documents", correlationId);
        request.Content = formData;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, correlationId, cancellationToken);

        var doc = await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOptions, cancellationToken);
        return doc ?? throw new ApiException((int)response.StatusCode, "문서 업로드 응답을 해석할 수 없습니다.", "EMPTY_RESPONSE", correlationId: correlationId);
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

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativePath.TrimStart('/'));
        request.Headers.Add("X-User-ID", UserId);
        request.Headers.Add("X-Correlation-ID", correlationId);
        return request;
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? payload,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..12];

        async Task<T> ExecuteOnce(CancellationToken ct)
        {
            using var request = CreateRequest(method, relativePath, correlationId);
            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload, options: JsonOptions);
            }

            using var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessAsync(response, correlationId, ct);

            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return result ?? throw new ApiException((int)response.StatusCode, "서버 응답 데이터가 비어 있습니다.", "EMPTY_RESPONSE", correlationId: correlationId);
        }

        // Only retry idempotent GET requests on transient transport errors (max 1 retry).
        if (method == HttpMethod.Get)
        {
            return await RetryPolicy.ExecuteAsync(
                ExecuteOnce,
                maxRetries: 1,
                baseDelay: TimeSpan.FromMilliseconds(300),
                shouldRetry: ex => ex is HttpRequestException,
                cancellationToken: cancellationToken);
        }

        return await ExecuteOnce(cancellationToken);
    }

    private async Task SendNoContentAsync(
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..12];
        using var request = CreateRequest(method, relativePath, correlationId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, correlationId, cancellationToken);
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string requestCorrelationId,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        var errorMessage = $"API 요청 실패 (HTTP {statusCode})";
        var errorCode = $"HTTP_{statusCode}";
        var correlationId = requestCorrelationId;
        string? stage = null;
        string? rawDetails = null;

        if (response.Headers.TryGetValues("X-Correlation-ID", out var headerValues))
        {
            var fromHeader = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromHeader))
            {
                correlationId = fromHeader;
            }
        }

        var rawBody = string.Empty;
        try
        {
            rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
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
                    if (errorObj.TryGetProperty("correlation_id", out var cidProp) && cidProp.ValueKind == JsonValueKind.String)
                    {
                        correlationId = cidProp.GetString() ?? correlationId;
                    }
                    if (errorObj.TryGetProperty("stage", out var stageProp) && stageProp.ValueKind == JsonValueKind.String)
                    {
                        stage = stageProp.GetString();
                    }
                    if (errorObj.TryGetProperty("details", out var detailsProp))
                    {
                        rawDetails = detailsProp.GetRawText();
                        if (string.IsNullOrWhiteSpace(stage) &&
                            detailsProp.ValueKind == JsonValueKind.Object &&
                            detailsProp.TryGetProperty("stage", out var nestedStage) &&
                            nestedStage.ValueKind == JsonValueKind.String)
                        {
                            stage = nestedStage.GetString();
                        }
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
        catch (JsonException jsonEx)
        {
            _logger?.LogWarning("ApiClient", "API", "ParseErrorBody",
                $"비표준 오류 응답 본문(HTTP {statusCode}): {jsonEx.Message}",
                errorCode: "API-005", correlationId: correlationId);
            rawDetails = rawBody.Length > 300 ? rawBody[..300] : rawBody;
        }

        throw new ApiException(
            statusCode,
            errorMessage,
            errorCode,
            innerException: null,
            correlationId: correlationId,
            pipelineStage: stage,
            rawDetails: rawDetails);
    }
}
