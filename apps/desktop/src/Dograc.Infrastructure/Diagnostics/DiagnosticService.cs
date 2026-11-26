using System.Runtime.InteropServices;
using System.Text;
using Dograc.Core.Errors;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Diagnostics;

/// <summary>
/// Builds and exports sanitized diagnostic reports with explicit inclusion/exclusion transparency.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    private readonly IServiceManager _serviceManager;
    private readonly IStructuredLogger _logger;
    private readonly ISettingsStore _settingsStore;
    private readonly IUpdateService _updateService;

    public DiagnosticService(
        IServiceManager serviceManager,
        IStructuredLogger logger,
        ISettingsStore settingsStore,
        IUpdateService updateService)
    {
        _serviceManager = serviceManager;
        _logger = logger;
        _settingsStore = settingsStore;
        _updateService = updateService;
    }

    public string GetIncludedDataSummary()
    {
        return """
               진단 보고서에 포함되는 정보:
               • 앱 버전, OS 및 .NET 런타임 버전
               • Docker / Ollama / FastAPI 서비스 상태 및 소유권
               • 선택된 모델명·테마·엔드포인트 URL(호스트 정보)
               • 최근 구조화 오류 및 서비스 로그 (최대 100건)

               자동 마스킹 / 제외되는 민감 정보:
               • API 키, 인증 토큰(Bearer/sk-/ghp_), 비밀번호, 이메일 주소
               • 업로드한 문서 원문 및 사용자 질문·RAG 프롬프트 본문
               """;
    }

    public string BuildSanitizedReport(ApplicationError? focusError = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("============================================================");
        sb.AppendLine("dograc Desktop Diagnostic Report (Sanitized)");
        sb.AppendLine("============================================================");
        sb.AppendLine($"Generated At : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"App Version  : v{_updateService.CurrentVersion} (AutoUpdate={_updateService.IsAutoUpdateSupported})");
        sb.AppendLine($"OS           : {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($".NET Runtime : {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"API Base URL : {_serviceManager.Options.ApiBaseUrl}");
        sb.AppendLine($"Ollama URL   : {SensitiveDataSanitizer.Sanitize(_settingsStore.Current.OllamaEndpointUrl)}");
        sb.AppendLine($"Active Model : {_settingsStore.Current.SelectedModelName}");
        sb.AppendLine($"Theme Mode   : {_settingsStore.Current.ThemeMode}");
        sb.AppendLine($"Log File     : {_logger.CurrentLogFilePath}");
        sb.AppendLine();

        if (focusError is not null)
        {
            sb.AppendLine("[Focus Error]");
            sb.AppendLine($"Code          : {focusError.ErrorCode} ({focusError.Severity} / {focusError.Category})");
            sb.AppendLine($"CorrelationId : {focusError.CorrelationId}");
            sb.AppendLine($"Timestamp     : {focusError.FormattedTimestamp}");
            sb.AppendLine($"Title         : {focusError.Title}");
            sb.AppendLine($"Description   : {focusError.Description}");
            sb.AppendLine($"{focusError.CausePrefixLabel}: {focusError.Cause}");
            sb.AppendLine($"Impact        : {focusError.Impact}");
            sb.AppendLine($"Resolution    : {focusError.Resolution}");
            if (!string.IsNullOrWhiteSpace(focusError.PipelineStage))
            {
                sb.AppendLine($"Stage         : {focusError.PipelineStage}");
            }
            sb.AppendLine("Technical Details:");
            sb.AppendLine(SensitiveDataSanitizer.Sanitize(focusError.TechnicalDetails));
            sb.AppendLine();
        }

        sb.AppendLine("[Service Statuses]");
        foreach (var svc in _serviceManager.CurrentStatuses)
        {
            sb.AppendLine($"- {svc.DisplayName} ({svc.Key}): State={svc.State}, Ownership={svc.Ownership}, Status={svc.StatusText}, Detail={SensitiveDataSanitizer.Sanitize(svc.DetailMessage)}");
        }
        sb.AppendLine();

        sb.AppendLine("[Recent Structured Logs (Last 100)]");
        var structured = _logger.RecentEntries.TakeLast(100).ToList();
        if (structured.Count == 0)
        {
            sb.AppendLine("(구조화 로그 없음)");
        }
        else
        {
            foreach (var entry in structured)
            {
                sb.AppendLine(SensitiveDataSanitizer.Sanitize(entry.FormattedLine));
            }
        }
        sb.AppendLine();

        sb.AppendLine("[Recent Service Process Logs (Last 100)]");
        var procLogs = _serviceManager.LogHistory.TakeLast(100).ToList();
        if (procLogs.Count == 0)
        {
            sb.AppendLine("(서비스 프로세스 로그 없음)");
        }
        else
        {
            foreach (var entry in procLogs)
            {
                sb.AppendLine(SensitiveDataSanitizer.Sanitize(entry.FormattedLine));
            }
        }

        return sb.ToString();
    }

    public async Task<string> ExportReportAsync(string filePath, ApplicationError? focusError = null)
    {
        var report = BuildSanitizedReport(focusError);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(filePath, report, Encoding.UTF8);
        _logger.LogInfo("Diagnostics", "System", "ExportDiagnosticReport", $"진단 보고서를 내보냈습니다: {Path.GetFileName(filePath)}");
        return filePath;
    }

    public string SanitizeSensitiveText(string? input) => SensitiveDataSanitizer.Sanitize(input);
}
