using System.Text.Json;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Connects to a local Ollama endpoint (GET /api/tags) to verify status, validate URLs, and enumerate installed models.
/// Distinguishes invalid URL, timeout, unreachable server, HTTP error, and malformed JSON.
/// </summary>
public sealed class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly IStructuredLogger? _logger;

    public OllamaService(HttpClient httpClient, IStructuredLogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OllamaConnectionResult> CheckAndListModelsAsync(
        string endpointUrl,
        CancellationToken cancellationToken = default)
    {
        var cleanBase = NormalizeBaseEndpoint(endpointUrl);
        if (!Uri.TryCreate(cleanBase, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            _logger?.LogWarning("OllamaService", "Ollama", "CheckAndListModels",
                $"잘못된 Ollama 엔드포인트 URL 형식: {endpointUrl}", errorCode: "LLM-004");
            return new OllamaConnectionResult(
                false,
                "[LLM-004] 올바른 URL 형식이 아닙니다. 예: http://localhost:11434",
                []);
        }

        var tagsUrl = $"{cleanBase}/api/tags";

        try
        {
            using var response = await _httpClient.GetAsync(tagsUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                _logger?.LogWarning("OllamaService", "Ollama", "CheckAndListModels",
                    $"Ollama HTTP 오류 응답 ({status})", errorCode: "LLM-001");
                return new OllamaConnectionResult(
                    false,
                    $"[LLM-001] Ollama 응답 오류 (HTTP {status}). 엔드포인트 주소({cleanBase})를 확인해 주세요.",
                    []);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var models = new List<OllamaModelInfo>();
            if (doc.RootElement.TryGetProperty("models", out var modelsProp) && modelsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in modelsProp.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var size = item.TryGetProperty("size", out var s) && s.TryGetInt64(out var parsedSize) ? parsedSize : 0L;
                    string? paramSize = null;
                    string? family = null;
                    if (item.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object)
                    {
                        paramSize = details.TryGetProperty("parameter_size", out var ps) ? ps.GetString() : null;
                        family = details.TryGetProperty("family", out var fm) ? fm.GetString() : null;
                    }

                    models.Add(new OllamaModelInfo(name, size, paramSize, family));
                }
            }

            var message = models.Count > 0
                ? $"연결 성공! {models.Count}개의 로컬 모델이 감지되었습니다."
                : "[LLM-002] 연결은 성공했으나 설치된 모델이 없습니다. 터미널에서 'ollama run qwen2.5:7b'를 실행해 주세요.";

            return new OllamaConnectionResult(true, message, models);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("OllamaService", "Ollama", "CheckAndListModels",
                $"Ollama 응답 시간 초과 ({cleanBase})", errorCode: "LLM-005");
            return new OllamaConnectionResult(
                false,
                $"[LLM-005] Ollama 서버({cleanBase}) 응답 대기 시간이 초과되었습니다. 서비스가 초기화 중인지 확인해 주세요.",
                []);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning("OllamaService", "Ollama", "CheckAndListModels",
                $"Ollama JSON 응답 파싱 실패: {ex.Message}", errorCode: "API-005");
            return new OllamaConnectionResult(
                false,
                $"[API-005] {cleanBase} 응답이 올바른 Ollama JSON 형식이 아닙니다. 엔드포인트 주소를 확인해 주세요.",
                []);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning("OllamaService", "Ollama", "CheckAndListModels",
                $"Ollama 연결 실패 ({cleanBase}): {ex.Message}", errorCode: "LLM-001");
            return new OllamaConnectionResult(
                false,
                $"[LLM-001] 로컬 Ollama 서버({cleanBase})에 연결할 수 없습니다. Ollama가 실행 중인지 확인해 주세요.",
                []);
        }
    }

    public static string NormalizeBaseEndpoint(string endpointUrl)
    {
        var trimmed = (endpointUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "http://localhost:11434";
        }

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3].TrimEnd('/');
        }

        return trimmed;
    }
}
