using System.Text.Json;
using System.Text.Json.Serialization;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Settings;

/// <summary>
/// Stores dograc Desktop settings as JSON in %LOCALAPPDATA%\dograc\settings.json.
/// Backs up corrupt files automatically (.corrupt.bak) and surfaces load/save errors instead of silently ignoring them.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsFilePath;
    private readonly IStructuredLogger? _logger;
    private readonly object _lock = new();
    private AppSettings _current;

    public JsonSettingsStore(IStructuredLogger? logger = null)
        : this(GetDefaultSettingsPath(), logger)
    {
    }

    public JsonSettingsStore(string settingsFilePath, IStructuredLogger? logger = null)
    {
        _settingsFilePath = settingsFilePath;
        _logger = logger;
        _current = LoadInternal();
    }

    public AppSettings Current
    {
        get { lock (_lock) return _current; }
    }

    public ApplicationError? LastStorageError { get; private set; }

    public event EventHandler<AppSettings>? SettingsChanged;

    public event EventHandler<ApplicationError>? StorageErrorOccurred;

    public AppSettings Load()
    {
        var loaded = LoadInternal();
        lock (_lock)
        {
            _current = loaded;
        }

        return loaded;
    }

    public void Save(AppSettings settings)
    {
        lock (_lock)
        {
            _current = settings;
            PersistInternal(settings);
        }

        SettingsChanged?.Invoke(this, settings);
    }

    public void RegisterDocumentLocalPath(string fileName, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        AppSettings updated;
        lock (_lock)
        {
            var nextPaths = new Dictionary<string, string>(_current.DocumentLocalPaths, StringComparer.OrdinalIgnoreCase)
            {
                [fileName.Trim()] = fullPath
            };
            updated = _current with { DocumentLocalPaths = nextPaths };
            _current = updated;
            PersistInternal(updated);
        }

        SettingsChanged?.Invoke(this, updated);
    }

    public string? ResolveDocumentLocalPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        lock (_lock)
        {
            if (_current.DocumentLocalPaths.TryGetValue(fileName.Trim(), out var path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private AppSettings LoadInternal()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (parsed is null)
            {
                BackupCorruptSettings("empty_null_payload");
                return new AppSettings();
            }

            var normalizedMap = new Dictionary<string, string>(
                parsed.DocumentLocalPaths ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);

            LastStorageError = null;
            return parsed with { DocumentLocalPaths = normalizedMap };
        }
        catch (JsonException ex)
        {
            var backupPath = BackupCorruptSettings("corrupt_json");
            var err = new ApplicationError(
                ErrorCode: "SET-001",
                Severity: ErrorSeverity.Warning,
                Category: ErrorCategory.Startup,
                Title: "설정 파일이 손상되어 기본값으로 복원했습니다",
                Description: "로컬 settings.json 파일의 JSON 형식이 손상되어 읽을 수 없었습니다.",
                Cause: "이전 비정상 종료 중 쓰기가 중단되었거나 설정 파일이 수동 편집 중 손상되었습니다.",
                IsConfirmedCause: true,
                Impact: backupPath is not null
                    ? $"기본 설정으로 안전하게 시작했으며 손상본은 '{Path.GetFileName(backupPath)}'에 백업되었습니다."
                    : "기본 설정으로 안전하게 시작했습니다.",
                Resolution: "Settings 페이지에서 테마 및 모델 설정을 다시 선택해 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.OpenModelSettings, "설정 확인", IsPrimary: true),
                    new(RecoveryActionKind.CopyDiagnostics, "진단 정보 복사")
                ],
                TechnicalDetails: $"path={_settingsFilePath} | backup={backupPath ?? "none"} | ex={ex.Message}",
                Timestamp: DateTimeOffset.Now,
                CorrelationId: Guid.NewGuid().ToString("N")[..12],
                Context: new ErrorContext("Settings", "System", "LoadSettings"));

            RecordStorageError(err, ex);
            return new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var err = new ApplicationError(
                ErrorCode: "ENV-006",
                Severity: ErrorSeverity.Warning,
                Category: ErrorCategory.Startup,
                Title: "설정 파일을 읽을 권한이 없습니다",
                Description: "로컬 설정 파일에 접근하지 못해 기본 설정으로 시작했습니다.",
                Cause: "폴더 접근 권한이 부족하거나 다른 프로세스가 파일을 잠그고 있을 수 있습니다.",
                IsConfirmedCause: false,
                Impact: "저장된 테마·모델 설정이 불러와지지 않았습니다.",
                Resolution: "%LOCALAPPDATA%\\dograc 폴더의 읽기/쓰기 권한을 확인해 주세요.",
                RecoveryActions: [new(RecoveryActionKind.CopyDiagnostics, "진단 정보 복사")],
                TechnicalDetails: $"path={_settingsFilePath} | ex={ex.GetType().Name}: {ex.Message}",
                Timestamp: DateTimeOffset.Now,
                CorrelationId: Guid.NewGuid().ToString("N")[..12],
                Context: new ErrorContext("Settings", "System", "LoadSettings"));

            RecordStorageError(err, ex);
            return new AppSettings();
        }
    }

    private void PersistInternal(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tmpPath = $"{_settingsFilePath}.tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _settingsFilePath, overwrite: true);
            LastStorageError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var err = new ApplicationError(
                ErrorCode: "SET-002",
                Severity: ErrorSeverity.Warning,
                Category: ErrorCategory.WorkspaceData,
                Title: "설정을 디스크에 저장하지 못했습니다",
                Description: "변경한 설정이 현재 세션에는 반영되었으나 settings.json 파일 저장에 실패했습니다.",
                Cause: "파일 쓰기 권한 부족, 디스크 공간 부족 또는 다른 프로세스의 잠금일 수 있습니다.",
                IsConfirmedCause: false,
                Impact: "앱을 다시 시작하면 방금 변경한 설정이 유지되지 않을 수 있습니다.",
                Resolution: "폴더 쓰기 권한을 확인한 뒤 설정을 다시 저장해 주세요.",
                RecoveryActions:
                [
                    new(RecoveryActionKind.CopyDiagnostics, "진단 정보 복사")
                ],
                TechnicalDetails: $"path={_settingsFilePath} | ex={ex.GetType().Name}: {ex.Message}",
                Timestamp: DateTimeOffset.Now,
                CorrelationId: Guid.NewGuid().ToString("N")[..12],
                Context: new ErrorContext("Settings", "System", "SaveSettings"));

            RecordStorageError(err, ex);
        }
    }

    private string? BackupCorruptSettings(string tag)
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            var backupPath = $"{_settingsFilePath}.{tag}.{DateTime.Now:yyyyMMddHHmmss}.bak";
            File.Copy(_settingsFilePath, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Settings", "System", "BackupCorruptSettings",
                $"손상된 설정 파일 백업 실패: {ex.Message}", errorCode: "SET-001");
            return null;
        }
    }

    private void RecordStorageError(ApplicationError error, Exception ex)
    {
        LastStorageError = error;
        _logger?.LogError(error, ex);
        StorageErrorOccurred?.Invoke(this, error);
    }

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "dograc", "settings.json");
    }
}
