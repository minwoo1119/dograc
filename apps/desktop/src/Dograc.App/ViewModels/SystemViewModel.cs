using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

/// <summary>
/// ViewModel for the System page displaying the 10-state service table, ownership,
/// level/service-filterable live Output console, log file opener, and sanitized diagnostic report exporter.
/// </summary>
public sealed partial class SystemViewModel : PageViewModelBase
{
    private readonly IServiceManager _serviceManager;
    private readonly IStartupOrchestrator _startupOrchestrator;
    private readonly IStructuredLogger? _structuredLogger;
    private readonly List<(DateTimeOffset Ts, string Service, string Level, string Line)> _allLogLines = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _selectedLogServiceFilter = "모든 서비스";

    [ObservableProperty]
    private string _selectedLogLevelFilter = "모든 레벨";

    [ObservableProperty]
    private string _logSearchQuery = string.Empty;

    [ObservableProperty]
    private string _filteredLogText = string.Empty;

    [ObservableProperty]
    private string _operationSummary = "서비스 상태 및 실행 로그를 확인합니다.";

    public SystemViewModel(
        IServiceManager serviceManager,
        IStartupOrchestrator startupOrchestrator,
        IStructuredLogger? structuredLogger = null)
    {
        _serviceManager = serviceManager;
        _startupOrchestrator = startupOrchestrator;
        _structuredLogger = structuredLogger;

        Services = new ObservableCollection<ServiceComponentStatus>(_serviceManager.CurrentStatuses);

        foreach (var entry in _serviceManager.LogHistory)
        {
            _allLogLines.Add((entry.Timestamp, entry.Source, entry.LevelLabel, entry.FormattedLine));
        }

        if (_structuredLogger is not null)
        {
            foreach (var entry in _structuredLogger.RecentEntries)
            {
                if (!string.Equals(entry.Component, "ServiceManager", StringComparison.OrdinalIgnoreCase))
                {
                    _allLogLines.Add((entry.Timestamp, entry.Service, entry.Level, entry.FormattedLine));
                }
            }

            _structuredLogger.EntryLogged += (_, entry) => RunOnUiThread(() =>
            {
                if (string.Equals(entry.Component, "ServiceManager", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                AddLogLine(entry.Timestamp, entry.Service, entry.Level, entry.FormattedLine);
            });
        }

        RebuildLogText();

        _serviceManager.StatusesChanged += (_, statuses) => RunOnUiThread(() =>
        {
            Services.Clear();
            foreach (var status in statuses)
            {
                Services.Add(status);
            }
        });

        _serviceManager.LogAdded += (_, entry) => RunOnUiThread(() =>
            AddLogLine(entry.Timestamp, entry.Source, entry.LevelLabel, entry.FormattedLine));
    }

    public override string PageTitle => "System";
    public override string PageDescription => "서비스 상태 및 실행 로그를 확인하고 진단 보고서를 내보냅니다.";

    public bool IsNotBusy => !IsBusy;

    public string ProjectRoot => _serviceManager.Options.ProjectRoot;

    public IReadOnlyList<string> LogServiceFilters { get; } =
    [
        "모든 서비스",
        "System",
        "Infra",
        "Ollama",
        "API",
        "Updates",
        "Application"
    ];

    public IReadOnlyList<string> LogLevelFilters { get; } =
    [
        "모든 레벨",
        "Info",
        "Warning",
        "Error",
        "Critical"
    ];

    public ObservableCollection<ServiceComponentStatus> Services { get; }

    public event EventHandler? SetupWizardRequested;
    public event EventHandler? ExportDiagnosticsRequested;

    partial void OnSelectedLogServiceFilterChanged(string value) => RebuildLogText();
    partial void OnSelectedLogLevelFilterChanged(string value) => RebuildLogText();
    partial void OnLogSearchQueryChanged(string value) => RebuildLogText();

    [RelayCommand]
    private async Task RefreshStatusesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OperationSummary = "서비스 상태를 새로고침하는 중입니다...";
        try
        {
            await _serviceManager.RefreshStatusesAsync();
            OperationSummary = "서비스 상태 새로고침 완료.";
        }
        catch (Exception ex)
        {
            OperationSummary = $"상태 새로고침 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OperationSummary = "필요한 로컬 서비스를 기동하는 중입니다...";
        try
        {
            await _startupOrchestrator.RunStartupAsync();
            OperationSummary = _startupOrchestrator.StatusBannerText;
        }
        catch (Exception ex)
        {
            OperationSummary = $"서비스 시작 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopManagedAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OperationSummary = "앱이 시작한(Managed) 서비스를 중지하는 중입니다...";
        try
        {
            await _serviceManager.StopManagedServicesAsync();
            OperationSummary = "앱 관리 서비스 중지 완료 (외부 실행 서비스는 유지됨).";
        }
        catch (Exception ex)
        {
            OperationSummary = $"관리 서비스 중지 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyLogs()
    {
        try
        {
            Clipboard.SetText(FilteredLogText);
            OperationSummary = "필터링된 로그를 클립보드에 복사했습니다.";
        }
        catch (Exception ex)
        {
            OperationSummary = $"클립보드 복사 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenLogFile()
    {
        var path = _structuredLogger?.CurrentLogFilePath;
        var dir = _structuredLogger?.LogDirectory;
        var target = !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : dir;

        if (string.IsNullOrWhiteSpace(target))
        {
            OperationSummary = "로그 파일 경로가 아직 초기화되지 않았습니다.";
            return;
        }

        try
        {
            Directory.CreateDirectory(dir ?? Path.GetDirectoryName(target)!);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            OperationSummary = $"로그 위치를 열었습니다: {target}";
        }
        catch (Exception ex)
        {
            OperationSummary = $"로그 파일 열기 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportDiagnosticReport()
    {
        ExportDiagnosticsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _serviceManager.ClearLogs();
        _structuredLogger?.ClearInMemory();
        _allLogLines.Clear();
        RebuildLogText();
        OperationSummary = "화면 로그 버퍼를 비웠습니다 (디스크 로그 파일은 보존됨).";
    }

    [RelayCommand]
    private void OpenSetupWizard()
    {
        SetupWizardRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddLogLine(DateTimeOffset ts, string service, string level, string formatted)
    {
        _allLogLines.Add((ts, service, level, formatted));
        if (_allLogLines.Count > 1500)
        {
            _allLogLines.RemoveAt(0);
        }

        RebuildLogText();
    }

    private void RebuildLogText()
    {
        var q = LogSearchQuery.Trim();
        var lines = _allLogLines
            .OrderBy(l => l.Ts)
            .Where(l => SelectedLogServiceFilter == "모든 서비스" ||
                        string.Equals(l.Service, SelectedLogServiceFilter, StringComparison.OrdinalIgnoreCase))
            .Where(l => SelectedLogLevelFilter == "모든 레벨" ||
                        string.Equals(l.Level, SelectedLogLevelFilter, StringComparison.OrdinalIgnoreCase))
            .Where(l => string.IsNullOrWhiteSpace(q) ||
                        l.Line.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Line);

        FilteredLogText = string.Join(Environment.NewLine, lines);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.InvokeAsync(action);
        }
    }
}
