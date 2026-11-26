using System.Text.Json.Serialization;

namespace Dograc.Core.Models;

/// <summary>
/// Exception representing a structured FastAPI error response with correlation and pipeline stage metadata.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(
        int statusCode,
        string message,
        string? errorCode = null,
        Exception? innerException = null,
        string? correlationId = null,
        string? pipelineStage = null,
        string? rawDetails = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        CorrelationId = correlationId;
        PipelineStage = pipelineStage;
        RawDetails = rawDetails;
    }

    public int StatusCode { get; }

    public string? ErrorCode { get; }

    public string? CorrelationId { get; }

    public string? PipelineStage { get; }

    public string? RawDetails { get; }
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
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("size_bytes")] long? SizeBytes = null,
    [property: JsonPropertyName("page_count")] int? PageCount = null,
    [property: JsonPropertyName("chunk_count")] int? ChunkCount = null)
{
    [JsonIgnore]
    public string SizeLabel => SizeBytes switch
    {
        null or < 0 => "-",
        >= 1024 * 1024 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{SizeBytes.Value / (1024.0 * 1024.0):0.0} MB"),
        >= 1024 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{SizeBytes.Value / 1024.0:0.0} KB"),
        _ => $"{SizeBytes.Value} B"
    };

    [JsonIgnore]
    public string PageCountLabel => PageCount is > 0 ? $"{PageCount.Value}p" : "-";

    [JsonIgnore]
    public string ChunkCountLabel => ChunkCount is > 0 ? $"{ChunkCount.Value}개" : "-";

    [JsonIgnore]
    public string UploadedAtLabel => CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    [JsonIgnore]
    public string StatusLabel => Status.ToLowerInvariant() switch
    {
        "ready" => "색인 완료",
        "processing" => "처리 중",
        "uploaded" => "대기 중",
        "failed" => string.IsNullOrWhiteSpace(FailureStageLabel) ? "실패" : $"실패 ({FailureStageLabel})",
        _ => Status
    };

    [JsonIgnore]
    public string? FailureStageLabel => FailureCode?.ToUpperInvariant() switch
    {
        "STORAGE_READ_FAILED" => "파일 읽기",
        "DOCUMENT_ENCRYPTED_PDF" => "암호화 PDF",
        "DOCUMENT_EMPTY_TEXT" => "빈 문서/스캔 PDF",
        "DOCUMENT_PARSE_FAILED" or "PARSE_ERROR" => "텍스트 추출",
        "DOCUMENT_CHUNKING_FAILED" => "Chunking",
        "DOCUMENT_EMBEDDING_FAILED" => "Embedding",
        "DOCUMENT_VECTOR_INDEX_FAILED" => "Vector Indexing",
        "DOCUMENT_DB_COMMIT_FAILED" => "DB 저장",
        "DOCUMENT_PROCESSING_FAILED" => "색인 처리",
        _ => null
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
