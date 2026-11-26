using System.Text.Json;
using System.Text.Json.Serialization;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Settings;

/// <summary>
/// Stores dograc Desktop settings as JSON in %LOCALAPPDATA%\dograc\settings.json.
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
    private readonly object _lock = new();
    private AppSettings _current;

    public JsonSettingsStore()
        : this(GetDefaultSettingsPath())
    {
    }

    public JsonSettingsStore(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        _current = LoadInternal();
    }

    public AppSettings Current
    {
        get { lock (_lock) return _current; }
    }

    public event EventHandler<AppSettings>? SettingsChanged;

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
                return new AppSettings();
            }

            var normalizedMap = new Dictionary<string, string>(
                parsed.DocumentLocalPaths ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);

            return parsed with { DocumentLocalPaths = normalizedMap };
        }
        catch
        {
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
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Avoid crashing desktop UI if settings file is transiently locked.
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "dograc", "settings.json");
    }
}
