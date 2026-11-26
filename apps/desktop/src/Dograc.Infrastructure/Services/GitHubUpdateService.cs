using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Velopack.Sources;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Checks Velopack (installed package) or GitHub Releases API (dev/portable fallback)
/// for dograc Desktop updates and supports one-click download & restart installation with safe rollback messaging.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    public const string DefaultCurrentVersion = "0.3.2";
    public const string GithubRepoUrl = "https://github.com/minwoo1119/dograc";
    public const string ReleasesApiUrl = "https://api.github.com/repos/minwoo1119/dograc/releases/latest";
    public const string ReleasesPageUrl = "https://github.com/minwoo1119/dograc/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly IStructuredLogger? _logger;
    private readonly UpdateManager? _velopackManager;
    private UpdateInfo? _pendingUpdate;

    [ActivatorUtilitiesConstructor]
    public GitHubUpdateService(HttpClient httpClient, IStructuredLogger? logger = null)
        : this(httpClient, ResolveAssemblyVersion(), enableVelopack: true, logger)
    {
    }

    public GitHubUpdateService(
        HttpClient httpClient,
        string currentVersion,
        bool enableVelopack = false,
        IStructuredLogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
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
            catch (Exception ex)
            {
                _logger?.LogInfo("Updates", "Updates", "InitVelopack",
                    $"포터블/개발 실행 모드로 감지되어 GitHub Releases API 폴백을 사용합니다 ({ex.GetType().Name}).");
                _velopackManager = null;
            }
        }
    }

    public string CurrentVersion { get; }

    public bool IsAutoUpdateSupported => _velopackManager is not null && _velopackManager.IsInstalled;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
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
            catch (Exception ex)
            {
                _logger?.LogWarning("Updates", "Updates", "VelopackCheck",
                    $"Velopack 피드 확인 실패로 GitHub Releases API로 폴백합니다: {ex.Message}", errorCode: "UPD-001");
            }
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("dograc-desktop", CurrentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode;
                _logger?.LogWarning("Updates", "Updates", "CheckForUpdates",
                    $"GitHub Releases API 응답 오류 (HTTP {code})", errorCode: "UPD-001");
                return new UpdateCheckResult(
                    false,
                    CurrentVersion,
                    CurrentVersion,
                    string.Empty,
                    ReleasesPageUrl,
                    null,
                    $"[UPD-001] 업데이트 서버 응답 오류(HTTP {code}). 현재 버전(v{CurrentVersion})은 정상 사용 가능합니다.",
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Updates", "Updates", "CheckForUpdates",
                "업데이트 서버 응답 시간 초과", errorCode: "UPD-001");
            return new UpdateCheckResult(
                false, CurrentVersion, CurrentVersion, string.Empty, ReleasesPageUrl, null,
                $"[UPD-001] 업데이트 서버 응답 시간이 초과되었습니다. 현재 버전(v{CurrentVersion})은 정상 사용 가능합니다.",
                CanAutoInstall: false);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning("Updates", "Updates", "CheckForUpdates",
                $"업데이트 서버 연결 불가(오프라인 가능): {ex.Message}", errorCode: "UPD-001");
            return new UpdateCheckResult(
                false,
                CurrentVersion,
                CurrentVersion,
                string.Empty,
                ReleasesPageUrl,
                null,
                $"[UPD-001] 오프라인 폐쇄망 환경이거나 GitHub에 연결할 수 없습니다. 현재 버전(v{CurrentVersion})은 정상 사용 가능합니다.",
                CanAutoInstall: false);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning("Updates", "Updates", "CheckForUpdates",
                $"업데이트 응답 JSON 파싱 실패: {ex.Message}", errorCode: "UPD-001");
            return new UpdateCheckResult(
                false, CurrentVersion, CurrentVersion, string.Empty, ReleasesPageUrl, null,
                $"[UPD-001] 릴리스 응답 형식을 해석하지 못했습니다. 현재 버전(v{CurrentVersion})은 정상 유지됩니다.",
                CanAutoInstall: false);
        }
    }

    public async Task DownloadAndApplyUpdateAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_velopackManager is null || !_velopackManager.IsInstalled)
        {
            throw new DogracApplicationException(new ApplicationError(
                ErrorCode: "UPD-003",
                Severity: ErrorSeverity.Warning,
                Category: ErrorCategory.UpdateInstall,
                Title: "현재 실행 모드에서는 자동 업데이트를 적용할 수 없습니다",
                Description: "포터블(ZIP) 또는 개발 실행 모드에서는 원클릭 자동 교체가 지원되지 않습니다.",
                Cause: "Velopack 설치 프로그램(Setup.exe)으로 설치된 환경이 아닙니다.",
                IsConfirmedCause: true,
                Impact: $"기존 실행 파일(v{CurrentVersion})은 변경 없이 안전하게 유지됩니다.",
                Resolution: "GitHub Releases 페이지에서 최신 dograc-win-Setup.exe 또는 Portable ZIP을 받아 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.OpenReleasePage, "릴리스 페이지 열기", IsPrimary: true)
                ],
                TechnicalDetails: $"CurrentVersion={CurrentVersion} | IsInstalled=false",
                Timestamp: DateTimeOffset.Now,
                CorrelationId: Guid.NewGuid().ToString("N")[..12],
                Context: new ErrorContext("Updates", "Updates", "DownloadAndApplyUpdate")));
        }

        try
        {
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DogracApplicationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var isPermissionOrLock = ex is UnauthorizedAccessException || ex is IOException;
            var err = new ApplicationError(
                ErrorCode: isPermissionOrLock ? "UPD-003" : "UPD-002",
                Severity: ErrorSeverity.Error,
                Category: ErrorCategory.UpdateInstall,
                Title: isPermissionOrLock ? "업데이트 설치 권한 또는 파일 잠금 오류" : "업데이트 파일 다운로드/검증에 실패했습니다",
                Description: "새 버전 패키지를 내려받거나 적용하는 도중 오류가 발생했습니다.",
                Cause: isPermissionOrLock
                    ? "설치 폴더 쓰기 권한이 부족하거나 다른 프로세스가 파일을 사용 중일 수 있습니다."
                    : "다운로드 중 네트워크가 끊어졌거나 파일 무결성 검증에 실패했을 수 있습니다.",
                IsConfirmedCause: false,
                Impact: $"기존 설치 버전(v{CurrentVersion})은 손상 없이 그대로 유지됩니다.",
                Resolution: "잠시 후 [다시 시도]를 누르거나, GitHub Releases에서 수동 설치기를 내려받아 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.Retry, "다시 시도", IsPrimary: true),
                    new(RecoveryActionKind.OpenReleasePage, "릴리스 페이지 열기"),
                    new(RecoveryActionKind.CopyDiagnostics, "진단 정보 복사")
                ],
                TechnicalDetails: $"CurrentVersion={CurrentVersion} | Exception={ex.GetType().Name}: {ex.Message}",
                Timestamp: DateTimeOffset.Now,
                CorrelationId: Guid.NewGuid().ToString("N")[..12],
                Context: new ErrorContext("Updates", "Updates", "DownloadAndApplyUpdate"),
                IsRetryable: true);

            _logger?.LogError(err, ex);
            throw new DogracApplicationException(err, ex);
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
