using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Velopack.Sources;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Checks Velopack (installed package) or GitHub Releases API (dev/portable fallback)
/// for dograc Desktop updates and supports one-click download & restart installation.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    public const string DefaultCurrentVersion = "0.1.0";
    public const string GithubRepoUrl = "https://github.com/minwoo1119/dograc";
    public const string ReleasesApiUrl = "https://api.github.com/repos/minwoo1119/dograc/releases/latest";
    public const string ReleasesPageUrl = "https://github.com/minwoo1119/dograc/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly UpdateManager? _velopackManager;
    private UpdateInfo? _pendingUpdate;

    [ActivatorUtilitiesConstructor]
    public GitHubUpdateService(HttpClient httpClient)
        : this(httpClient, ResolveAssemblyVersion(), enableVelopack: true)
    {
    }

    public GitHubUpdateService(HttpClient httpClient, string currentVersion, bool enableVelopack = false)
    {
        _httpClient = httpClient;
        CurrentVersion = string.IsNullOrWhiteSpace(currentVersion) ? DefaultCurrentVersion : currentVersion;

        if (enableVelopack)
        {
            try
            {
                var source = new GithubSource(GithubRepoUrl, accessToken: null, prerelease: false);
                var manager = new UpdateManager(source);
                if (manager.IsInstalled)
                {
                    _velopackManager = manager;
                    if (manager.CurrentVersion is not null)
                    {
                        CurrentVersion = manager.CurrentVersion.ToString();
                    }
                }
            }
            catch
            {
                // Unpackaged / dev execution — fall back cleanly to GitHub Releases API.
                _velopackManager = null;
            }
        }
    }

    public string CurrentVersion { get; }

    public bool IsAutoUpdateSupported => _velopackManager is not null && _velopackManager.IsInstalled;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Installed Velopack package: check delta/full release feed directly.
        if (_velopackManager is not null && _velopackManager.IsInstalled)
        {
            try
            {
                var info = await _velopackManager.CheckForUpdatesAsync();
                _pendingUpdate = info;

                if (info is not null)
                {
                    var targetVersion = $"v{info.TargetFullRelease.Version}";
                    var notes = string.IsNullOrWhiteSpace(info.TargetFullRelease.NotesMarkdown)
                        ? "새로운 버전이 준비되었습니다. 클릭하여 자동 설치 및 재시작할 수 있습니다."
                        : info.TargetFullRelease.NotesMarkdown;

                    return new UpdateCheckResult(
                        HasUpdate: true,
                        CurrentVersion: CurrentVersion,
                        LatestVersion: targetVersion,
                        ReleaseNotes: notes.Length > 600 ? notes[..600] + "..." : notes,
                        ReleaseUrl: ReleasesPageUrl,
                        InstallerUrl: $"{GithubRepoUrl}/releases/download/{targetVersion}/dograc-win-Setup.exe",
                        Message: $"새 버전 ({targetVersion})이 출시되었습니다! 클릭하여 바로 설치할 수 있습니다.",
                        CanAutoInstall: true);
                }

                return new UpdateCheckResult(
                    HasUpdate: false,
                    CurrentVersion: CurrentVersion,
                    LatestVersion: CurrentVersion,
                    ReleaseNotes: string.Empty,
                    ReleaseUrl: ReleasesPageUrl,
                    InstallerUrl: null,
                    Message: "현재 최신 버전을 사용하고 있습니다. (Velopack 자동 업데이트 활성)",
                    CanAutoInstall: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fall back to GitHub Releases API when Velopack feed check fails.
            }
        }

        // 2. Fallback: GitHub Releases API (dev / portable / air-gapped safe)
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
                    $"업데이트 서버 응답 코드: {(int)response.StatusCode}",
                    CanAutoInstall: false);
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
                    $"새 버전 ({latestTag})이 출시되었습니다!",
                    CanAutoInstall: false)
                : new UpdateCheckResult(
                    false,
                    CurrentVersion,
                    string.IsNullOrWhiteSpace(latestTag) ? CurrentVersion : latestTag,
                    string.Empty,
                    htmlUrl,
                    installerUrl,
                    "현재 최신 버전을 사용하고 있습니다.",
                    CanAutoInstall: false);
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
                "오프라인 폐쇄망 환경이거나 GitHub 연결에 실패했습니다.",
                CanAutoInstall: false);
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
                $"업데이트 확인 중 오류: {ex.Message}",
                CanAutoInstall: false);
        }
    }

    public async Task DownloadAndApplyUpdateAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_velopackManager is null || !_velopackManager.IsInstalled)
        {
            throw new InvalidOperationException("포터블/개발 실행 모드에서는 자동 설치가 지원되지 않습니다. Setup.exe 설치 버전을 사용해 주세요.");
        }

        _pendingUpdate ??= await _velopackManager.CheckForUpdatesAsync();
        if (_pendingUpdate is null)
        {
            throw new InvalidOperationException("설치할 새 업데이트가 없습니다.");
        }

        await _velopackManager.DownloadUpdatesAsync(
            _pendingUpdate,
            percent => progress?.Report(Math.Clamp(percent, 0, 100)),
            cancellationToken);

        _velopackManager.ApplyUpdatesAndRestart(_pendingUpdate);
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

    private static string ResolveAssemblyVersion()
    {
        var ver = Assembly.GetEntryAssembly()?.GetName().Version;
        if (ver is null || (ver.Major == 0 && ver.Minor == 0 && ver.Build == 0))
        {
            return DefaultCurrentVersion;
        }

        return $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}";
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
