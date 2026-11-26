using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Dograc.Core.Errors;
using Dograc.Core.Models;

namespace Dograc.Infrastructure.Diagnostics;

/// <summary>
/// Central classifier mapping CLR exceptions, HTTP status codes, FastAPI error payloads,
/// and pipeline stage codes into standardized, user-actionable ApplicationError instances.
/// </summary>
public sealed class ErrorMapper : IErrorMapper
{
    private const int HrDiskFull1 = unchecked((int)0x80070070);
    private const int HrDiskFull2 = unchecked((int)0x80070027);

    public ApplicationError Map(Exception exception, ErrorContext context)
    {
        if (exception is DogracApplicationException appEx)
        {
            return appEx.Error;
        }

        if (exception is ApiException apiEx)
        {
            return MapApiException(apiEx, context);
        }

        var cid = context.ResolvedCorrelationId;
        var now = DateTimeOffset.Now;
        var tech = BuildTechnicalDetails(exception, context, cid);

        // User-initiated cancellation vs Timeout
        if (exception is OperationCanceledException oce)
        {
            var isTimeout = exception is TimeoutException ||
                            (oce.InnerException is TimeoutException) ||
                            (context.Metadata?.ContainsKey("timeout_seconds") == true && !oce.CancellationToken.IsCancellationRequested);

            if (isTimeout)
            {
                var waitLabel = context.Metadata is not null && context.Metadata.TryGetValue("timeout_seconds", out var sec)
                    ? $"{sec}초"
                    : "지정된 제한 시간";
                var isOllama = string.Equals(context.Service, "Ollama", StringComparison.OrdinalIgnoreCase);

                return new ApplicationError(
                    ErrorCode: isOllama ? "LLM-005" : "API-004",
                    Severity: ErrorSeverity.Error,
                    Category: isOllama ? ErrorCategory.OllamaModel : ErrorCategory.BackendApi,
                    Title: isOllama ? "AI 모델 응답 시간 초과" : "요청 응답 시간 초과",
                    Description: $"{context.Operation} 작업이 {waitLabel} 동안 응답하지 않아 중단되었습니다.",
                    Cause: isOllama
                        ? "로컬 AI 모델 초기 로딩이 오래 걸리거나, 문맥 길이가 길어 추론이 지연되었을 수 있습니다."
                        : "대용량 문서 색인 또는 로컬 서버 과부하로 응답이 지연되었을 수 있습니다.",
                    IsConfirmedCause: false,
                    Impact: "현재 요청이 완료되지 않았습니다. 기존 데이터와 입력 내용은 안전하게 유지됩니다.",
                    Resolution: "잠시 후 [다시 시도]를 누르거나, System 페이지에서 서비스 상태를 확인해 주세요.",
                    RecoveryActions:
                    [
                        new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                        new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기"),
                        new(RecoveryActionKind.ViewErrorDetails, "상세 정보")
                    ],
                    TechnicalDetails: tech,
                    Timestamp: now,
                    CorrelationId: cid,
                    Context: context,
                    IsRetryable: true);
            }

            return new ApplicationError(
                ErrorCode: "CHAT-002",
                Severity: ErrorSeverity.Information,
                Category: ErrorCategory.RagChat,
                Title: "작업이 취소되었습니다",
                Description: $"사용자의 요청으로 {context.Operation} 작업을 중지했습니다.",
                Cause: "사용자가 중지 버튼을 눌러 진행 중인 작업을 취소했습니다.",
                IsConfirmedCause: true,
                Impact: "진행 중이던 요청만 중단되었으며 입력 내용은 그대로 복원할 수 있습니다.",
                Resolution: "필요한 경우 [다시 시도] 또는 [입력창 복원]으로 이어서 작업할 수 있습니다.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.RestoreInput, "입력창 복원")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: context,
                IsRetryable: true);
        }

        if (exception is UriFormatException)
        {
            return new ApplicationError(
                ErrorCode: "LLM-004",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.OllamaModel,
                Title: "연결 주소(URL) 형식 오류",
                Description: "입력한 서버 주소가 올바른 URL 형식이 아닙니다.",
                Cause: "http:// 또는 https:// 프로토콜이 누락되었거나 호스트·포트 형식이 잘못되었습니다.",
                IsConfirmedCause: true,
                Impact: "해당 엔드포인트로 연결하거나 모델 목록을 불러올 수 없습니다.",
                Resolution: "예시 형식(http://localhost:11434)에 맞게 주소를 수정한 뒤 다시 연결 테스트를 진행해 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.OpenModelSettings, "연결 설정 확인", IsPrimary: true)
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: context,
                IsRetryable: false);
        }

        if (exception is HttpRequestException || exception is SocketException)
        {
            if (string.Equals(context.Service, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                return new ApplicationError(
                    ErrorCode: "LLM-001",
                    Severity: ErrorSeverity.Error,
                    Category: ErrorCategory.OllamaModel,
                    Title: "로컬 Ollama 서버에 연결할 수 없습니다",
                    Description: "Ollama 서비스 응답을 받지 못했습니다.",
                    Cause: "Ollama 앱이 실행 중이 아니거나, 설정된 엔드포인트 주소/포트가 다를 수 있습니다.",
                    IsConfirmedCause: false,
                    Impact: "로컬 모델 목록 조회 및 AI 답변 생성 기능을 사용할 수 없습니다.",
                    Resolution: "Ollama를 실행하거나 Models 페이지에서 엔드포인트(기본값 http://localhost:11434)를 확인해 주세요.",
                    RecoveryActions:
                    [
                        new(RecoveryActionKind.LaunchOllama, "Ollama 실행", IsPrimary: true),
                        new(RecoveryActionKind.OpenModelSettings, "연결 설정 열기"),
                        new(RecoveryActionKind.Retry, "다시 시도")
                    ],
                    TechnicalDetails: tech,
                    Timestamp: now,
                    CorrelationId: cid,
                    Context: context,
                    IsRetryable: true);
            }

            if (string.Equals(context.Service, "Updates", StringComparison.OrdinalIgnoreCase))
            {
                return new ApplicationError(
                    ErrorCode: "UPD-001",
                    Severity: ErrorSeverity.Warning,
                    Category: ErrorCategory.UpdateInstall,
                    Title: "업데이트 서버에 연결할 수 없습니다",
                    Description: "GitHub 릴리스 정보를 확인하지 못했습니다.",
                    Cause: "인터넷 연결이 없는 폐쇄망 환경이거나 일시적인 네트워크 장애일 수 있습니다.",
                    IsConfirmedCause: false,
                    Impact: "최신 버전 확인만 보류되며, 현재 설치된 dograc은 정상적으로 계속 사용할 수 있습니다.",
                    Resolution: "네트워크 연결을 확인한 뒤 다시 시도하거나, GitHub 릴리스 페이지에서 수동 확인할 수 있습니다.",
                    RecoveryActions:
                    [
                        new(RecoveryActionKind.Retry, "다시 확인", IsPrimary: true),
                        new(RecoveryActionKind.OpenReleasePage, "릴리스 페이지 열기")
                    ],
                    TechnicalDetails: tech,
                    Timestamp: now,
                    CorrelationId: cid,
                    Context: context,
                    IsRetryable: true);
            }

            var isUpload = context.Operation.Contains("Upload", StringComparison.OrdinalIgnoreCase);
            return new ApplicationError(
                ErrorCode: isUpload ? "DOC-006" : "API-001",
                Severity: ErrorSeverity.Error,
                Category: isUpload ? ErrorCategory.DocumentIndexing : ErrorCategory.BackendApi,
                Title: isUpload ? "문서 업로드 중 연결이 끊어졌습니다" : "로컬 백엔드(FastAPI) 서버에 연결할 수 없습니다",
                Description: isUpload
                    ? "파일을 전송하는 도중 백엔드 서버와의 연결이 중단되었습니다."
                    : $"{context.Operation} 요청을 처리할 백엔드 서버에 연결하지 못했습니다.",
                Cause: "FastAPI 백엔드 서버가 중지되었거나 시작 도중 종료되었을 가능성이 높습니다.",
                IsConfirmedCause: false,
                Impact: "문서 관리, 워크스페이스 동기화 및 AI 질의응답 기능이 일시 중단됩니다.",
                Resolution: "[서비스 시작]을 눌러 백엔드를 다시 기동하거나, System 페이지에서 실행 로그를 확인해 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.StartServices, "서비스 시작", IsPrimary: true),
                    new(RecoveryActionKind.Retry, "다시 시도"),
                    new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: context,
                IsRetryable: true,
                PipelineStage: isUpload ? "upload" : null);
        }

        if (exception is JsonException)
        {
            var isSettings = string.Equals(context.Component, "Settings", StringComparison.OrdinalIgnoreCase);
            return new ApplicationError(
                ErrorCode: isSettings ? "SET-001" : "API-005",
                Severity: isSettings ? ErrorSeverity.Warning : ErrorSeverity.Error,
                Category: isSettings ? ErrorCategory.Startup : ErrorCategory.BackendApi,
                Title: isSettings ? "설정 파일이 손상되어 기본값으로 복원했습니다" : "서버 응답 형식을 해석할 수 없습니다",
                Description: isSettings
                    ? "로컬 settings.json 파일의 JSON 형식이 손상되어 읽을 수 없었습니다."
                    : $"{context.Operation} 응답 데이터가 예상된 JSON 구조와 일치하지 않습니다.",
                Cause: isSettings
                    ? "비정상 종료 중 파일 기록이 중단되었거나 수동 편집 오류가 발생했습니다."
                    : "백엔드와 데스크톱 클라이언트 버전이 다르거나, 중간 프록시에서 오류 페이지를 반환했을 수 있습니다.",
                IsConfirmedCause: isSettings,
                Impact: isSettings
                    ? "손상된 기존 파일은 .corrupt.bak으로 백업되었으며 기본 설정으로 실행됩니다."
                    : "해당 요청 결과를 화면에 표시할 수 없습니다.",
                Resolution: isSettings
                    ? "Settings 페이지에서 테마와 모델 설정을 다시 지정해 주세요."
                    : "다시 시도하거나 System 페이지에서 백엔드 상태를 확인해 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.ViewErrorDetails, "상세 정보"),
                    new(RecoveryActionKind.CopyDiagnostics, "진단 정보 복사")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: context,
                IsRetryable: !isSettings);
        }

        if (exception is UnauthorizedAccessException)
        {
            return new ApplicationError(
                ErrorCode: "ENV-006",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.Startup,
                Title: "파일 또는 폴더 접근 권한이 부족합니다",
                Description: $"{context.Operation} 중 필요한 파일에 접근하거나 기록하지 못했습니다.",
                Cause: "Windows 폴더 권한이 제한되어 있거나, 파일이 읽기 전용·다른 프로그램에서 잠겨 있을 수 있습니다.",
                IsConfirmedCause: false,
                Impact: "요청한 파일 읽기/저장 작업이 완료되지 않았습니다.",
                Resolution: "파일이 다른 프로그램에서 열려 있는지 확인하거나, 쓰기 권한이 있는 경로를 선택해 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.CopyDiagnostics, "진단 정보 복사")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: context,
                IsRetryable: true);
        }

        if (exception is FileNotFoundException || exception is DirectoryNotFoundException)
        {
            return new ApplicationError(
                ErrorCode: "DOC-002",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.DocumentIndexing,
                Title: "선택한 파일 또는 경로를 찾을 수 없습니다",
                Description: "지정된 파일이 이동되었거나 삭제되어 열 수 없습니다.",
                Cause: "파일 선택 이후 원본 위치가 변경되었거나 삭제되었습니다.",
                IsConfirmedCause: true,
                Impact: "해당 파일의 업로드 또는 미리보기를 진행할 수 없습니다.",
                Resolution: "파일 위치를 확인한 뒤 [파일 다시 선택]으로 다시 지정해 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.ReselectFile, "파일 다시 선택", IsPrimary: true)
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: context,
                IsRetryable: false);
        }

        if (exception is IOException ioEx)
        {
            var isDiskFull = ioEx.HResult == HrDiskFull1 || ioEx.HResult == HrDiskFull2 ||
                             ioEx.Message.Contains("space", StringComparison.OrdinalIgnoreCase);
            if (isDiskFull)
            {
                return new ApplicationError(
                    ErrorCode: "ENV-005",
                    Severity: ErrorSeverity.Critical,
                    Category: ErrorCategory.Startup,
                    Title: "디스크 저장 공간이 부족합니다",
                    Description: $"{context.Operation}에 필요한 파일을 저장할 여유 공간이 부족합니다.",
                    Cause: "로컬 드라이브의 남은 저장 용량이 부족합니다.",
                    IsConfirmedCause: true,
                    Impact: "문서 업로드, 설정 저장, 로그 기록 및 업데이트를 진행할 수 없습니다.",
                    Resolution: "불필요한 파일을 정리하여 디스크 여유 공간을 확보한 뒤 다시 시도해 주세요.",
                    RecoveryActions:
                    [
                        new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                        new(RecoveryActionKind.CopyDiagnostics, "진단 정보 복사")
                    ],
                    TechnicalDetails: tech,
                    Timestamp: now,
                    CorrelationId: cid,
                    Context: context,
                    IsRetryable: true);
            }

            return new ApplicationError(
                ErrorCode: "DOC-002",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.DocumentIndexing,
                Title: "파일 입출력 중 오류가 발생했습니다",
                Description: $"{context.Operation} 중 파일을 읽거나 쓰는 데 실패했습니다.",
                Cause: "파일이 다른 앱에서 점유 중이거나 저장 장치 연결이 일시 중단되었을 수 있습니다.",
                IsConfirmedCause: false,
                Impact: "요청한 파일 작업이 중단되었습니다.",
                Resolution: "파일을 사용 중인 다른 프로그램을 닫고 [다시 시도]를 눌러 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.ViewErrorDetails, "상세 정보")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: context,
                IsRetryable: true);
        }

        // Fallback: Unclassified unexpected exception (APP-999)
        return new ApplicationError(
            ErrorCode: "APP-999",
            Severity: ErrorSeverity.Error,
            Category: ErrorCategory.Application,
            Title: $"{context.Operation} 중 예상하지 못한 문제가 발생했습니다",
            Description: "작업을 처리하는 동안 분류되지 않은 내부 오류가 발생했습니다.",
            Cause: "내부 예외로 인해 정확한 원인이 자동 판별되지 않았습니다. 상세 진단 정보에서 예외 유형을 확인할 수 있습니다.",
            IsConfirmedCause: false,
            Impact: "요청한 작업만 중단되었으며 애플리케이션은 계속 사용할 수 있습니다.",
            Resolution: "[다시 시도]를 진행하거나, 문제가 반복되면 [진단 보고서 내보내기]로 로그를 확인해 주세요.",
            RecoveryActions:
            [
                new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                new(RecoveryActionKind.ViewErrorDetails, "상세 정보"),
                new(RecoveryActionKind.ExportDiagnosticReport, "진단 보고서 내보내기")
            ],
            TechnicalDetails: tech,
            Timestamp: now,
            CorrelationId: cid,
            Context: context,
            IsRetryable: true);
    }

    public ApplicationError MapApiException(ApiException exception, ErrorContext context)
    {
        var cid = !string.IsNullOrWhiteSpace(exception.CorrelationId)
            ? exception.CorrelationId!
            : context.ResolvedCorrelationId;
        var enrichedContext = context with
        {
            CorrelationId = cid,
            HttpStatusCode = exception.StatusCode,
            PipelineStage = exception.PipelineStage ?? context.PipelineStage
        };
        var now = DateTimeOffset.Now;
        var tech = BuildTechnicalDetails(exception, enrichedContext, cid);
        var codeUpper = (exception.ErrorCode ?? string.Empty).ToUpperInvariant();
        var rawLower = $"{exception.Message} {exception.RawDetails}".ToLowerInvariant();

        // Document parsing & indexing pipeline stages
        if (codeUpper is "DOCUMENT_PARSE_FAILED" or "DOCUMENT_PROCESSING_FAILED" or "UNSUPPORTED_FILE_TYPE" or "FILE_TOO_LARGE" or "EMPTY_FILE")
        {
            return MapDocumentApiFailure(codeUpper, exception.PipelineStage, exception.Message, exception.RawDetails, enrichedContext, tech, now, cid);
        }

        // Generation / LLM failures
        if (codeUpper == "GENERATION_FAILED")
        {
            if (rawLower.Contains("not found") || rawLower.Contains("pull") || rawLower.Contains("404"))
            {
                return new ApplicationError(
                    ErrorCode: "LLM-002",
                    Severity: ErrorSeverity.Error,
                    Category: ErrorCategory.OllamaModel,
                    Title: "선택한 AI 모델이 설치되어 있지 않습니다",
                    Description: "현재 설정된 모델을 로컬 Ollama에서 찾을 수 없어 답변을 생성하지 못했습니다.",
                    Cause: "선택한 모델이 아직 로컬에 다운로드되지 않았거나 이름이 일치하지 않습니다.",
                    IsConfirmedCause: true,
                    Impact: "질문 내용은 보존되었으나 답변이 생성되지 않았습니다.",
                    Resolution: "Models 페이지에서 설치된 다른 모델을 선택하거나, 추천 모델 설치 명령을 실행해 주세요.",
                    RecoveryActions:
                    [
                        new(RecoveryActionKind.OpenModelSettings, "Models 페이지 열기", IsPrimary: true),
                        new(RecoveryActionKind.RestoreInput, "입력창 복원")
                    ],
                    TechnicalDetails: tech,
                    Timestamp: now,
                    CorrelationId: cid,
                    Context: enrichedContext,
                    IsRetryable: false,
                    PipelineStage: "generation");
            }

            if (rawLower.Contains("memory") || rawLower.Contains("vram") || rawLower.Contains("oom") || rawLower.Contains("cuda"))
            {
                return new ApplicationError(
                    ErrorCode: "LLM-003",
                    Severity: ErrorSeverity.Error,
                    Category: ErrorCategory.OllamaModel,
                    Title: "메모리(RAM/VRAM)가 부족해 모델을 실행할 수 없습니다",
                    Description: "선택한 로컬 모델 로딩 또는 추론 중 메모리 부족 오류가 발생했습니다.",
                    Cause: "시스템 RAM 또는 GPU VRAM 여유 용량이 선택한 모델 요구량보다 부족할 수 있습니다.",
                    IsConfirmedCause: false,
                    Impact: "현재 모델로 답변을 생성할 수 없습니다. 질문 내용은 보존되었습니다.",
                    Resolution: "다른 대용량 프로그램을 닫거나 Models 페이지에서 더 가벼운 모델을 선택해 주세요.",
                    RecoveryActions:
                    [
                        new(RecoveryActionKind.OpenModelSettings, "Models 페이지 열기", IsPrimary: true),
                        new(RecoveryActionKind.RestoreInput, "입력창 복원"),
                        new(RecoveryActionKind.ViewErrorDetails, "상세 정보")
                    ],
                    TechnicalDetails: tech,
                    Timestamp: now,
                    CorrelationId: cid,
                    Context: enrichedContext,
                    IsRetryable: false,
                    PipelineStage: "generation");
            }

            return new ApplicationError(
                ErrorCode: "CHAT-001",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.RagChat,
                Title: "AI 답변 생성에 실패했습니다",
                Description: "문서 검색 후 로컬 AI 모델에서 답변을 생성하지 못했습니다.",
                Cause: "답변 생성 도중 Ollama 연결이 끊겼거나 모델 추론 오류가 발생했을 수 있습니다.",
                IsConfirmedCause: false,
                Impact: "답변이 저장되지 않았으며, 입력하신 질문은 안전하게 유지됩니다.",
                Resolution: "Ollama 실행 상태를 확인한 뒤 [다시 시도] 또는 [입력창 복원]을 눌러 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.RestoreInput, "입력창 복원"),
                    new(RecoveryActionKind.OpenModelSettings, "연결 설정 열기"),
                    new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: enrichedContext,
                IsRetryable: true,
                PipelineStage: "generation");
        }

        // Retrieval failures
        if (codeUpper.Contains("RETRIEVAL"))
        {
            return new ApplicationError(
                ErrorCode: "RAG-001",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.RagChat,
                Title: "관련 문서 근거 검색에 실패했습니다",
                Description: "벡터 데이터베이스 또는 임베딩 검색 단계에서 오류가 발생했습니다.",
                Cause: "Qdrant 벡터 DB 연결이 끊겼거나 임베딩 모델이 응답하지 않았을 수 있습니다.",
                IsConfirmedCause: false,
                Impact: "질문에 대한 근거 검색 및 답변 생성이 중단되었습니다.",
                Resolution: "System 페이지에서 Infra/API 상태를 확인하고 [다시 시도]를 눌러 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.StartServices, "서비스 시작"),
                    new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: enrichedContext,
                IsRetryable: true,
                PipelineStage: "retrieval");
        }

        // Status code mapping
        return exception.StatusCode switch
        {
            400 or 422 => new ApplicationError(
                ErrorCode: "API-006",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.BackendApi,
                Title: "입력값이 올바르지 않습니다",
                Description: string.IsNullOrWhiteSpace(exception.Message) ? "요청 데이터 검증에 실패했습니다." : exception.Message,
                Cause: "필수 항목이 비어 있거나 서버가 허용하지 않는 형식의 값이 전달되었습니다.",
                IsConfirmedCause: true,
                Impact: "요청이 저장되거나 실행되지 않았습니다.",
                Resolution: "입력 내용을 확인하고 수정한 뒤 다시 시도해 주세요.",
                RecoveryActions: [new(RecoveryActionKind.ViewErrorDetails, "상세 정보")],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: enrichedContext,
                IsRetryable: false),

            401 or 403 => new ApplicationError(
                ErrorCode: "API-007",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.BackendApi,
                Title: "요청 권한이 없습니다",
                Description: "해당 리소스에 접근할 수 있는 권한이 확인되지 않았습니다.",
                Cause: "사용자 식별 정보 또는 워크스페이스 소유권이 일치하지 않습니다.",
                IsConfirmedCause: true,
                Impact: "요청한 조회 또는 변경 작업이 차단되었습니다.",
                Resolution: "워크스페이스 목록을 새로고침하거나 System 페이지에서 백엔드 설정을 확인해 주세요.",
                RecoveryActions: [new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기", IsPrimary: true)],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: enrichedContext,
                IsRetryable: false),

            404 => new ApplicationError(
                ErrorCode: codeUpper == "TRACE_NOT_FOUND" ? "CHAT-003" : "API-008",
                Severity: ErrorSeverity.Error,
                Category: codeUpper == "TRACE_NOT_FOUND" ? ErrorCategory.RagChat : ErrorCategory.BackendApi,
                Title: codeUpper == "TRACE_NOT_FOUND" ? "답변 근거(Trace)를 찾을 수 없습니다" : "요청한 항목을 찾을 수 없습니다",
                Description: codeUpper == "TRACE_NOT_FOUND"
                    ? "이 답변에 연결된 근거 추적 데이터가 존재하지 않습니다."
                    : "대상 워크스페이스, 문서 또는 대화가 이미 삭제되었을 수 있습니다.",
                Cause: "서버에 해당 식별자의 데이터가 존재하지 않습니다.",
                IsConfirmedCause: true,
                Impact: "선택한 항목의 상세 정보를 불러올 수 없습니다.",
                Resolution: "목록을 새로고침하여 최신 상태를 반영해 주세요.",
                RecoveryActions: [new(RecoveryActionKind.Retry, "새로고침", IsPrimary: true)],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: enrichedContext,
                IsRetryable: false),

            409 or 413 or 429 => new ApplicationError(
                ErrorCode: exception.StatusCode == 413 ? "DOC-003" : "API-009",
                Severity: ErrorSeverity.Error,
                Category: exception.StatusCode == 413 ? ErrorCategory.DocumentIndexing : ErrorCategory.BackendApi,
                Title: exception.StatusCode switch
                {
                    409 => "이미 동일한 항목이 존재하거나 작업이 충돌했습니다",
                    413 => "파일 크기가 최대 허용 용량을 초과했습니다",
                    _ => "요청이 너무 많아 일시 제한되었습니다"
                },
                Description: exception.Message,
                Cause: exception.StatusCode switch
                {
                    409 => "중복된 이름 또는 이미 진행 중인 동일 요청과 충돌했습니다.",
                    413 => "업로드하려는 파일 용량이 서버 최대 제한을 초과했습니다.",
                    _ => "짧은 시간에 너무 많은 요청이 전송되었습니다."
                },
                IsConfirmedCause: true,
                Impact: "현재 요청이 거부되었습니다.",
                Resolution: exception.StatusCode switch
                {
                    413 => "파일 크기를 줄이거나 분할한 뒤 다시 선택해 주세요.",
                    429 => "잠시 기다린 뒤 [다시 시도]를 눌러 주세요.",
                    _ => "이름이나 조건을 변경한 뒤 다시 시도해 주세요."
                },
                RecoveryActions: exception.StatusCode == 413
                    ? [new(RecoveryActionKind.ReselectFile, "파일 다시 선택", IsPrimary: true)]
                    : [new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true)],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: enrichedContext,
                IsRetryable: exception.StatusCode == 429),

            _ => new ApplicationError(
                ErrorCode: "API-010",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.BackendApi,
                Title: $"백엔드 서버 오류 (HTTP {exception.StatusCode})",
                Description: string.IsNullOrWhiteSpace(exception.Message)
                    ? "로컬 백엔드 서버에서 요청을 처리하는 중 내부 오류가 발생했습니다."
                    : exception.Message,
                Cause: "백엔드 내부 예외 또는 하위 저장소(PostgreSQL/Qdrant/MinIO) 연결 문제일 수 있습니다.",
                IsConfirmedCause: false,
                Impact: $"{context.Operation} 작업이 완료되지 않았습니다.",
                Resolution: "System 페이지에서 서비스 상태와 로그를 확인한 뒤 [다시 시도]를 눌러 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기"),
                    new(RecoveryActionKind.ViewErrorDetails, "상세 정보")
                ],
                TechnicalDetails: tech,
                Timestamp: now,
                CorrelationId: cid,
                Context: enrichedContext,
                IsRetryable: true)
        };
    }

    public ApplicationError MapDocumentFailureStage(string? failureCode, string? message, ErrorContext context)
    {
        var cid = context.ResolvedCorrelationId;
        var now = DateTimeOffset.Now;
        var tech = $"failure_code={failureCode ?? "none"} | op={context.Operation} | cid={cid}";
        return MapDocumentApiFailure(
            (failureCode ?? "DOCUMENT_PROCESSING_FAILED").ToUpperInvariant(),
            context.PipelineStage,
            message ?? "문서 처리 중 오류가 발생했습니다.",
            null,
            context,
            tech,
            now,
            cid);
    }

    private static ApplicationError MapDocumentApiFailure(
        string codeUpper,
        string? stage,
        string message,
        string? rawDetails,
        ErrorContext context,
        string tech,
        DateTimeOffset now,
        string cid)
    {
        var detailsLower = $"{message} {rawDetails}".ToLowerInvariant();
        var stageNorm = (stage ?? string.Empty).ToLowerInvariant();

        if (codeUpper == "UNSUPPORTED_FILE_TYPE")
        {
            return new ApplicationError(
                "DOC-001", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
                "지원하지 않는 파일 형식입니다",
                "현재 dograc은 PDF(.pdf)와 UTF-8 텍스트(.txt) 문서만 색인할 수 있습니다.",
                "지원 목록에 없는 확장자 또는 미디어 형식의 파일이 선택되었습니다.", true,
                "해당 파일은 업로드되지 않았습니다.",
                "PDF 또는 TXT 형식으로 변환한 뒤 [파일 다시 선택]을 눌러 주세요.",
                [new(RecoveryActionKind.ReselectFile, "파일 다시 선택", IsPrimary: true)],
                tech, now, cid, context, IsRetryable: false, PipelineStage: "upload");
        }

        if (codeUpper is "FILE_TOO_LARGE" or "EMPTY_FILE")
        {
            return new ApplicationError(
                "DOC-003", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
                codeUpper == "EMPTY_FILE" ? "내용이 비어 있는 파일입니다" : "파일 크기 제한을 초과했습니다",
                message,
                codeUpper == "EMPTY_FILE" ? "파일 크기가 0바이트입니다." : "허용된 최대 업로드 용량을 초과했습니다.", true,
                "해당 파일은 업로드되지 않았습니다.",
                "유효한 크기와 내용을 가진 파일을 다시 선택해 주세요.",
                [new(RecoveryActionKind.ReselectFile, "파일 다시 선택", IsPrimary: true)],
                tech, now, cid, context, IsRetryable: false, PipelineStage: "upload");
        }

        if (detailsLower.Contains("encrypted") || detailsLower.Contains("암호"))
        {
            return new ApplicationError(
                "DOC-004", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
                "문서 색인 실패 [단계: 텍스트 추출]",
                "암호로 보호된 PDF 문서에서는 텍스트를 추출할 수 없습니다.",
                "PDF 파일에 열기 암호가 설정되어 있습니다.", true,
                "업로드는 보관되었으나 검색 색인 대상에서 제외되었습니다.",
                "PDF 암호를 해제한 새 파일로 다시 업로드해 주세요.",
                [new(RecoveryActionKind.ReselectFile, "파일 다시 선택", IsPrimary: true)],
                tech, now, cid, context, IsRetryable: false, PipelineStage: "텍스트 추출");
        }

        if (stageNorm == "chunking" || detailsLower.Contains("document_empty_text") || detailsLower.Contains("ocr") || detailsLower.Contains("빈 문서"))
        {
            return new ApplicationError(
                "IDX-002", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
                "문서 색인 실패 [단계: Chunking]",
                "문서에서 색인할 텍스트를 찾지 못했습니다.",
                "텍스트 레이어가 없는 스캔 이미지 PDF(OCR 필요)이거나 내용이 비어 있을 수 있습니다.", false,
                "청크가 생성되지 않아 AI 검색에 활용될 수 없습니다.",
                "텍스트가 포함된 PDF로 OCR 변환하거나 다른 파일을 선택해 주세요.",
                [new(RecoveryActionKind.ReselectFile, "파일 다시 선택", IsPrimary: true)],
                tech, now, cid, context, IsRetryable: false, PipelineStage: "Chunking");
        }

        if (stageNorm == "embedding" || detailsLower.Contains("embedding") || detailsLower.Contains("임베딩"))
        {
            return new ApplicationError(
                "IDX-003", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
                "문서 색인 실패 [단계: Embedding]",
                "문서 청크를 임베딩 벡터로 변환하는 단계에서 실패했습니다.",
                "임베딩 모델 로딩 오류, 메모리 부족 또는 차원 불일치가 발생했을 수 있습니다.", false,
                "문서 색인이 중단되었으며 검색에 반영되지 않았습니다.",
                "System 페이지에서 백엔드 로그를 확인한 뒤 [색인 다시 시작]을 눌러 주세요.",
                [
                    new(RecoveryActionKind.ReindexDocument, "색인 다시 시작", IsPrimary: true),
                    new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기"),
                    new(RecoveryActionKind.ViewErrorDetails, "상세 정보")
                ],
                tech, now, cid, context, IsRetryable: true, PipelineStage: "Embedding");
        }

        if (stageNorm == "vector_indexing" || detailsLower.Contains("qdrant") || detailsLower.Contains("vector"))
        {
            return new ApplicationError(
                "IDX-004", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
                "문서 색인 실패 [단계: Vector Indexing]",
                "벡터 데이터베이스(Qdrant)에 색인 데이터를 저장하지 못했습니다.",
                "색인 도중 Qdrant 연결이 중단되었거나 컬렉션 쓰기 오류가 발생했습니다.", true,
                "부분 색인 데이터는 자동 정리되었으며 해당 문서는 아직 검색되지 않습니다.",
                "Docker/Qdrant 상태를 확인하고 [서비스 시작] 후 [색인 다시 시작]을 눌러 주세요.",
                [
                    new(RecoveryActionKind.StartServices, "서비스 시작", IsPrimary: true),
                    new(RecoveryActionKind.ReindexDocument, "색인 다시 시작"),
                    new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기")
                ],
                tech, now, cid, context, IsRetryable: true, PipelineStage: "Vector Indexing");
        }

        if (stageNorm == "db_commit" || detailsLower.Contains("데이터베이스"))
        {
            return new ApplicationError(
                "IDX-005", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
                "문서 색인 실패 [단계: DB 저장]",
                "문서 메타데이터와 청크를 데이터베이스에 기록하는 단계에서 실패했습니다.",
                "데이터베이스 연결 중단 또는 트랜잭션 오류가 발생했습니다.", true,
                "트랜잭션이 안전하게 롤백되어 기존 데이터 손상은 없습니다.",
                "서비스 상태를 확인한 뒤 [색인 다시 시작]을 눌러 주세요.",
                [
                    new(RecoveryActionKind.ReindexDocument, "색인 다시 시작", IsPrimary: true),
                    new(RecoveryActionKind.OpenSystemPage, "System 페이지 열기")
                ],
                tech, now, cid, context, IsRetryable: true, PipelineStage: "DB 저장");
        }

        // Default text extraction / parse error
        return new ApplicationError(
            "IDX-001", ErrorSeverity.Error, ErrorCategory.DocumentIndexing,
            "문서 색인 실패 [단계: 텍스트 추출]",
            string.IsNullOrWhiteSpace(message) ? "문서에서 텍스트를 추출하지 못했습니다." : message,
            "손상된 PDF 파일이거나 지원하지 않는 인코딩(비 UTF-8)일 수 있습니다.", false,
            "해당 문서는 검색 색인에서 제외되었습니다.",
            "파일이 정상적으로 열리는지 확인한 뒤 [파일 다시 선택] 또는 [색인 다시 시작]을 진행해 주세요.",
            [
                new(RecoveryActionKind.ReselectFile, "파일 다시 선택", IsPrimary: true),
                new(RecoveryActionKind.ReindexDocument, "색인 다시 시작"),
                new(RecoveryActionKind.ViewErrorDetails, "상세 정보")
            ],
            tech, now, cid, context, IsRetryable: true, PipelineStage: "텍스트 추출");
    }

    private static string BuildTechnicalDetails(Exception ex, ErrorContext ctx, string cid)
    {
        var lines = new List<string>
        {
            $"CorrelationId: {cid}",
            $"Component: {ctx.Component} | Service: {ctx.Service} | Operation: {ctx.Operation}"
        };
        if (ctx.HttpStatusCode is not null)
        {
            lines.Add($"HTTP Status: {ctx.HttpStatusCode}");
        }
        if (ctx.ProcessExitCode is not null)
        {
            lines.Add($"Process Exit Code: {ctx.ProcessExitCode}");
        }
        if (!string.IsNullOrWhiteSpace(ctx.PipelineStage))
        {
            lines.Add($"Pipeline Stage: {ctx.PipelineStage}");
        }
        if (ex is ApiException apiEx && !string.IsNullOrWhiteSpace(apiEx.ErrorCode))
        {
            lines.Add($"Backend Code: {apiEx.ErrorCode}");
        }

        lines.Add($"Exception: {ex.GetType().FullName}: {SensitiveDataSanitizer.Sanitize(ex.Message)}");
        if (ex.InnerException is not null)
        {
            lines.Add($"Inner: {ex.InnerException.GetType().Name}: {SensitiveDataSanitizer.Sanitize(ex.InnerException.Message)}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
