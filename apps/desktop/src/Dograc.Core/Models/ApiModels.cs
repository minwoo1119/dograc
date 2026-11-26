using System.Text.Json.Serialization;

namespace Dograc.Core.Models;

/// <summary>
/// Exception representing a structured FastAPI error response.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string message, string? errorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public int StatusCode { get; }

    public string? ErrorCode { get; }
}

public sealed record WorkspaceDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record DocumentDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("workspace_id")] Guid WorkspaceId,
    [property: JsonPropertyName("source_file_name")] string SourceFileName,
    [property: JsonPropertyName("media_type")] string MediaType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("failure_code")] string? FailureCode,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt)
{
    [JsonIgnore]
    public string StatusLabel => Status.ToLowerInvariant() switch
    {
        "ready" => "색인 완료",
        "processing" => "처리 중",
        "uploaded" => "대기 중",
        "failed" => "실패",
        _ => Status
    };

    [JsonIgnore]
    public bool CanTriggerProcess =>
        string.Equals(Status, "uploaded", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "failed", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsReady => string.Equals(Status, "ready", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsFailed => string.Equals(Status, "failed", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsProcessing => string.Equals(Status, "processing", StringComparison.OrdinalIgnoreCase);
}

public sealed record ConversationDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("workspace_id")] Guid WorkspaceId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record MessageDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("conversation_id")] Guid ConversationId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("trace_id")] Guid? TraceId);

public sealed record ConversationDetailDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("workspace_id")] Guid WorkspaceId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("messages")] IReadOnlyList<MessageDto> Messages);

public sealed record RetrievedChunkDto(
    [property: JsonPropertyName("chunk_id")] string? ChunkId,
    [property: JsonPropertyName("document_id")] string? DocumentId,
    [property: JsonPropertyName("source_file_name")] string SourceFileName,
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("text")] string Text);

public sealed record TraceDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("message_id")] Guid MessageId,
    [property: JsonPropertyName("workspace_id")] Guid WorkspaceId,
    [property: JsonPropertyName("retrieved_chunks")] IReadOnlyList<RetrievedChunkDto> RetrievedChunks,
    [property: JsonPropertyName("prompt")] string? Prompt,
    [property: JsonPropertyName("model_name")] string? ModelName,
    [property: JsonPropertyName("latency_ms")] int? LatencyMs,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record SendMessageOptions(
    string? ModelName = null,
    string? EndpointUrl = null);
