using System.Net.Http.Headers;
using System.Text.Json;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Checks GitHub Releases API (minwoo1119/dograc) for desktop updates with air-gapped offline safety.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    public const string DefaultCurrentVersion = "0.1.0";
    public const string ReleasesApiUrl = "https://api.github.com/repos/minwoo1119/dograc/releases/latest";
    public const string ReleasesPageUrl = "https://github.com/minwoo1119/dograc/releases/latest";

    private readonly HttpClient _httpClient;

    [ActivatorUtilitiesConstructor]
    public GitHubUpdateService(HttpClient httpClient)
        : this(httpClient, DefaultCurrentVersion)
    {
    }

    public GitHubUpdateService(HttpClient httpClient, string currentVersion)
    {
        _httpClient = httpClient;
        CurrentVersion = string.IsNullOrWhiteSpace(currentVersion) ? DefaultCurrentVersion : currentVersion;
    }

    public string CurrentVersion { get; }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("dograc-desktop", CurrentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    false,
                    CurrentVersion,
                    CurrentVersion,
                    string.Empty,
                    ReleasesPageUrl,
                    null,
                    $"업데이트 서버 응답 코드: {(int)response.StatusCode}");
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var latestTag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString()?.Trim() ?? string.Empty : string.Empty;
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString()?.Trim() ?? string.Empty : string.Empty;
            var htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? ReleasesPageUrl : ReleasesPageUrl;

            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                        (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup", StringComparison.OrdinalIgnoreCase)))
                    {
                        installerUrl = asset.TryGetProperty("browser_download_url", out var dl) ? dl.GetString() : null;
                        break;
                    }
                }
            }

            var hasUpdate = CompareVersions(latestTag, CurrentVersion) > 0;
            var summaryNotes = body.Length > 600 ? body[..600] + "..." : body;

            return hasUpdate
                ? new UpdateCheckResult(
                    true,
                    CurrentVersion,
                    latestTag,
                    string.IsNullOrWhiteSpace(summaryNotes) ? "새로운 릴리스가 게시되었습니다." : summaryNotes,
                    htmlUrl,
                    installerUrl,
                    $"새 버전 ({latestTag})이 출시되었습니다!")
                : new UpdateCheckResult(
                    false,
                    CurrentVersion,
                    string.IsNullOrWhiteSpace(latestTag) ? CurrentVersion : latestTag,
                    string.Empty,
                    htmlUrl,
                    installerUrl,
                    "현재 최신 버전을 사용하고 있습니다.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult(
                false,
                CurrentVersion,
                CurrentVersion,
                string.Empty,
                ReleasesPageUrl,
                null,
                "오프라인 폐쇄망 환경이거나 GitHub 연결에 실패했습니다.");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                false,
                CurrentVersion,
                CurrentVersion,
                string.Empty,
                ReleasesPageUrl,
                null,
                $"업데이트 확인 중 오류: {ex.Message}");
        }
    }

    public static int CompareVersions(string left, string right)
    {
        var l = ParseVersionParts(left);
        var r = ParseVersionParts(right);
        var max = Math.Max(l.Length, r.Length);

        for (var i = 0; i < max; i++)
        {
            var lv = i < l.Length ? l[i] : 0;
            var rv = i < r.Length ? r[i] : 0;
            if (lv != rv)
            {
                return lv.CompareTo(rv);
            }
        }

        return 0;
    }

    private static int[] ParseVersionParts(string version)
    {
        var clean = (version ?? string.Empty).Trim().TrimStart('v', 'V');
        if (string.IsNullOrWhiteSpace(clean))
        {
            return [0];
        }

        return clean
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => int.TryParse(p, out var num) ? num : 0)
            .ToArray();
    }
}
