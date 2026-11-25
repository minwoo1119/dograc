using System.Text.Json;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Connects to a local Ollama endpoint (GET /api/tags) to verify status and enumerate installed models.
/// </summary>
public sealed class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;

    public OllamaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OllamaConnectionResult> CheckAndListModelsAsync(
        string endpointUrl,
        CancellationToken cancellationToken = default)
    {
        var cleanBase = NormalizeBaseEndpoint(endpointUrl);
        var tagsUrl = $"{cleanBase}/api/tags";

        try
        {
            using var response = await _httpClient.GetAsync(tagsUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new OllamaConnectionResult(
                    false,
                    $"Ollama 응답 오류 (HTTP {(int)response.StatusCode}). 엔드포인트를 확인해 주세요.",
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
                : "연결은 성공했으나 설치된 모델이 없습니다. 터미널에서 'ollama run qwen2.5:7b'를 실행해 주세요.";

            return new OllamaConnectionResult(true, message, models);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new OllamaConnectionResult(
                false,
                $"로컬 Ollama 서버({cleanBase})에 연결할 수 없습니다. Ollama가 실행 중인지 확인해 주세요.",
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
