using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Errors;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

public enum ModelLifecycleState
{
    Available,
    Downloading,
    Installed,
    Active,
    Failed
}

public sealed record RecommendedModelItem(string Name, string Description, string Command, bool IsRecommended);

public sealed class RecommendedModelRowViewModel
{
    public RecommendedModelRowViewModel(RecommendedModelItem item, ModelLifecycleState state, int downloadPercent = 0)
    {
        Item = item;
        State = state;
        DownloadPercent = downloadPercent;
    }

    public RecommendedModelItem Item { get; }
    public string Name => Item.Name;
    public string Description => Item.Description;
    public string Command => Item.Command;
    public bool IsRecommended => Item.IsRecommended;
    public ModelLifecycleState State { get; }
    public int DownloadPercent { get; }

    public bool IsActive => State == ModelLifecycleState.Active;
    public bool IsInstalledOnly => State == ModelLifecycleState.Installed;
    public bool IsDownloading => State == ModelLifecycleState.Downloading;
    public bool IsFailed => State == ModelLifecycleState.Failed;
    public bool CanDownload => State is ModelLifecycleState.Available or ModelLifecycleState.Failed;
    public string DownloadButtonLabel => State == ModelLifecycleState.Failed ? "다시 시도" : "다운로드";
    public string StatusBadgeLabel => State switch
    {
        ModelLifecycleState.Active => "사용 중",
        ModelLifecycleState.Installed => "설치됨",
        ModelLifecycleState.Downloading => $"다운로드 중 ({DownloadPercent}%)",
        ModelLifecycleState.Failed => "실패",
        _ => "미설치"
    };
}

public sealed class InstalledModelRowViewModel
{
    public InstalledModelRowViewModel(OllamaModelInfo model, bool isActive)
    {
        Model = model;
        IsActive = isActive;
    }

    public OllamaModelInfo Model { get; }
    public string Name => Model.Name;
    public string SizeLabel => Model.SizeLabel;
    public string Family => Model.Family ?? "ollama";
    public string ParameterSize => Model.ParameterSize ?? "-";
    public string MetaLabel => ParameterSize != "-" ? $"{SizeLabel} · {ParameterSize} · {Family}" : $"{SizeLabel} · {Family}";
    public bool IsActive { get; }
    public bool IsNotActive => !IsActive;
}

/// <summary>
/// ViewModel for the Models page separating Ollama server connectivity from active model runnability,
/// supporting in-app Ollama model pull with live progress & cancellation, and command copy.
/// </summary>
public sealed partial class ModelsViewModel : PageViewModelBase
{
    private readonly IOllamaService _ollamaService;
    private readonly ISettingsStore _settingsStore;
    private readonly IErrorHandlingService? _errorHandling;
    private readonly IRecoveryActionService? _recoveryService;
    private readonly List<OllamaModelInfo> _rawModels = [];
    private readonly List<RecommendedModelItem> _catalogModels;
    private readonly HashSet<string> _failedPullModels = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _pullCts;
    private string? _lastPullModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExternalEndpoint))]
    [NotifyPropertyChangedFor(nameof(EndpointPrivacyLabel))]
    private string _ollamaEndpointUrl;

    [ObservableProperty]
    private string _selectedModelName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusBadge))]
    [NotifyPropertyChangedFor(nameof(IsSelectedModelRunnable))]
    [NotifyPropertyChangedFor(nameof(SelectedModelRunnableLabel))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusBadge))]
    [NotifyPropertyChangedFor(nameof(SelectedModelRunnableLabel))]
    private bool _hasCheckedConnection;

    [ObservableProperty]
    private string _connectionMessage = "연결 테스트 버튼을 눌러 로컬 Ollama 모델 목록을 확인하세요.";

    [ObservableProperty]
    private string? _copiedNotice;

    [ObservableProperty]
    private string _selectedSection = "설치된 모델";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotDownloadingModel))]
    private bool _isDownloadingModel;

    [ObservableProperty]
    private string? _downloadingModelName;

    [ObservableProperty]
    private int _downloadProgressPercent;

    [ObservableProperty]
    private string _downloadStatusText = string.Empty;

    [ObservableProperty]
    private ApplicationError? _activeError;

    public ModelsViewModel(
        IOllamaService ollamaService,
        ISettingsStore settingsStore,
        IErrorHandlingService? errorHandling = null,
        IRecoveryActionService? recoveryService = null)
    {
        _ollamaService = ollamaService;
        _settingsStore = settingsStore;
        _errorHandling = errorHandling;
        _recoveryService = recoveryService;

        var current = _settingsStore.Current;
        _ollamaEndpointUrl = current.OllamaEndpointUrl;
        _selectedModelName = current.SelectedModelName;

        InstalledModels = [];
        RecommendedModels = [];
        _catalogModels =
        [
            new RecommendedModelItem("qwen2.5:7b", "한국어 문서 질의응답 및 인용 정확도에 최적화된 추천 모델 (4.7 GB)", "ollama run qwen2.5:7b", true),
            new RecommendedModelItem("llama3.1:8b", "Meta 범용 오픈소스 언어 모델 (4.9 GB)", "ollama run llama3.1:8b", false),
            new RecommendedModelItem("gemma2:9b", "Google 고품질 요약·추론 모델 (5.4 GB)", "ollama run gemma2:9b", false),
            new RecommendedModelItem("deepseek-r1:8b", "단계별 추론 특화 로컬 모델 (4.9 GB)", "ollama run deepseek-r1:8b", false)
        ];
        RebuildRecommendedRows();

        _settingsStore.SettingsChanged += (_, updated) => RunOnUiThread(() =>
        {
            OllamaEndpointUrl = updated.OllamaEndpointUrl;
            SelectedModelName = updated.SelectedModelName;
            RebuildInstalledRows();
        });
    }

    public override string PageTitle => "Models";
    public override string PageDescription => "로컬 Ollama 서버 연결 상태와 실행 가능한 LLM 모델을 관리합니다.";

    public bool IsNotBusy => !IsBusy;
    public bool IsNotDownloadingModel => !IsDownloadingModel;
    public bool HasNoInstalledModels => InstalledModels.Count == 0;

    public bool IsExternalEndpoint => !IsLoopbackEndpoint(OllamaEndpointUrl);

    public string EndpointPrivacyLabel => IsExternalEndpoint
        ? "외부 모델 엔드포인트 감지됨 — 질문과 검색된 문서 발췌문이 해당 외부 서버로 전송됩니다. 기밀 문서 처리 시 로컬(localhost) 모드를 권장합니다."
        : "로컬 전용 모드 — 모든 문서와 질문이 이 PC 내부(localhost)에서만 처리되며 외부로 전송되지 않습니다.";

    public string ConnectionStatusBadge => !HasCheckedConnection
        ? "연결 점검 전"
        : IsConnected ? "Ollama 연결됨" : "Ollama 미연결";

    public bool IsSelectedModelRunnable =>
        IsConnected && _rawModels.Any(m => string.Equals(m.Name, SelectedModelName, StringComparison.OrdinalIgnoreCase));

    public string SelectedModelRunnableLabel => !HasCheckedConnection
        ? $"선택 모델: {SelectedModelName} (연결 테스트 후 실행 가능 여부 확인)"
        : !IsConnected
            ? $"선택 모델: {SelectedModelName} (Ollama 연결 필요)"
            : IsSelectedModelRunnable
                ? $"선택 모델: {SelectedModelName} — 설치됨 · 실행 가능"
                : $"선택 모델: {SelectedModelName} — 미설치 (다운로드 필요)";

    public ObservableCollection<InstalledModelRowViewModel> InstalledModels { get; }
    public ObservableCollection<RecommendedModelRowViewModel> RecommendedModels { get; }

    [RelayCommand]
    private async Task TestAndRefreshModelsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ActiveError = null;
        ConnectionMessage = "로컬 Ollama 서버에 연결하는 중입니다...";
        try
        {
            var result = await _ollamaService.CheckAndListModelsAsync(OllamaEndpointUrl);
            HasCheckedConnection = true;
            IsConnected = result.IsSuccess;
            ConnectionMessage = result.Message;

            _rawModels.Clear();
            _rawModels.AddRange(result.Models);

            if (result.IsSuccess &&
                result.Models.Count > 0 &&
                !result.Models.Any(m => string.Equals(m.Name, SelectedModelName, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedModelName = result.Models[0].Name;
                SaveModelSettings();
            }

            RebuildInstalledRows();
        }
        catch (Exception ex)
        {
            HasCheckedConnection = true;
            IsConnected = false;
            var ctx = new ErrorContext("Models", "Ollama", "Ollama 연결 테스트");
            ActiveError = _errorHandling?.Handle(ex, ctx, TestAndRefreshModelsAsync, notifyToast: false);
            ConnectionMessage = ActiveError?.Description ?? ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadModelAsync(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName) || IsDownloadingModel)
        {
            return;
        }

        _pullCts?.Cancel();
        _pullCts?.Dispose();
        _pullCts = new CancellationTokenSource();
        _lastPullModel = modelName;

        IsDownloadingModel = true;
        DownloadingModelName = modelName;
        DownloadProgressPercent = 0;
        DownloadStatusText = $"{modelName} 다운로드 준비 중...";
        _failedPullModels.Remove(modelName);
        ActiveError = null;
        RebuildRecommendedRows();

        try
        {
            var progress = new Progress<OllamaPullProgress>(p => RunOnUiThread(() =>
            {
                DownloadProgressPercent = p.Percent;
                DownloadStatusText = $"{modelName}: {p.Status} ({p.Percent}%)";
                RebuildRecommendedRows();
            }));

            await _ollamaService.PullModelAsync(OllamaEndpointUrl, modelName, progress, _pullCts.Token);
            DownloadProgressPercent = 100;
            DownloadStatusText = $"{modelName} 다운로드가 완료되었습니다.";
            _failedPullModels.Remove(modelName);
            await TestAndRefreshModelsAsync();
        }
        catch (OperationCanceledException oce)
        {
            _failedPullModels.Add(modelName);
            DownloadStatusText = $"{modelName} 다운로드가 취소되었습니다.";
            var ctx = new ErrorContext("Models", "Ollama", $"모델 다운로드 ({modelName})");
            ActiveError = _errorHandling?.Handle(oce, ctx, () => DownloadModelAsync(modelName), notifyToast: false);
        }
        catch (Exception ex)
        {
            _failedPullModels.Add(modelName);
            var ctx = new ErrorContext("Models", "Ollama", $"모델 다운로드 ({modelName})");
            ActiveError = _errorHandling?.Handle(ex, ctx, () => DownloadModelAsync(modelName), notifyToast: false);
            DownloadStatusText = ActiveError?.Description ?? ex.Message;
        }
        finally
        {
            IsDownloadingModel = false;
            DownloadingModelName = null;
            RebuildRecommendedRows();
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _pullCts?.Cancel();
    }

    [RelayCommand]
    private async Task ExecuteErrorActionAsync(RecoveryActionDescriptor? action)
    {
        if (action is null)
        {
            return;
        }

        if (action.Kind == RecoveryActionKind.Retry && !string.IsNullOrWhiteSpace(_lastPullModel))
        {
            await DownloadModelAsync(_lastPullModel);
            return;
        }

        if (_recoveryService is not null)
        {
            await _recoveryService.ExecuteAsync(action, ActiveError, TestAndRefreshModelsAsync);
        }
    }

    [RelayCommand]
    private void DismissError() => ActiveError = null;

    [RelayCommand]
    private void SelectInstalledModel(InstalledModelRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        SelectModelByName(row.Name);
    }

    [RelayCommand]
    private void SelectModelByName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return;
        }

        SelectedModelName = modelName.Trim();
        SaveModelSettings();
        RebuildInstalledRows();
        ConnectionMessage = $"활성 모델이 '{SelectedModelName}'(으)로 변경되었습니다.";
    }

    [RelayCommand]
    private void SaveModelSettings()
    {
        var updated = _settingsStore.Current with
        {
            OllamaEndpointUrl = string.IsNullOrWhiteSpace(OllamaEndpointUrl)
                ? "http://localhost:11434"
                : OllamaEndpointUrl.Trim(),
            SelectedModelName = string.IsNullOrWhiteSpace(SelectedModelName)
                ? "qwen2.5:7b"
                : SelectedModelName.Trim()
        };
        _settingsStore.Save(updated);
        OnPropertyChanged(nameof(IsSelectedModelRunnable));
        OnPropertyChanged(nameof(SelectedModelRunnableLabel));
        ConnectionMessage = $"설정이 저장되었습니다 ({updated.SelectedModelName} @ {updated.OllamaEndpointUrl}).";
    }

    [RelayCommand]
    private void CopyInstallCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        try
        {
            Clipboard.SetText(command);
            CopiedNotice = $"클립보드에 복사됨: {command}";
        }
        catch (Exception ex)
        {
            CopiedNotice = $"클립보드 복사 실패 ({ex.Message}) — 명령어: {command}";
        }
    }

    private void RebuildInstalledRows()
    {
        InstalledModels.Clear();
        foreach (var model in _rawModels)
        {
            var active = string.Equals(model.Name, SelectedModelName, StringComparison.OrdinalIgnoreCase);
            InstalledModels.Add(new InstalledModelRowViewModel(model, active));
        }

        RebuildRecommendedRows();
        OnPropertyChanged(nameof(HasNoInstalledModels));
        OnPropertyChanged(nameof(IsSelectedModelRunnable));
        OnPropertyChanged(nameof(SelectedModelRunnableLabel));
    }

    private void RebuildRecommendedRows()
    {
        RecommendedModels.Clear();
        foreach (var item in _catalogModels)
        {
            var isInstalled = _rawModels.Any(m => string.Equals(m.Name, item.Name, StringComparison.OrdinalIgnoreCase));
            var isActive = isInstalled && string.Equals(item.Name, SelectedModelName, StringComparison.OrdinalIgnoreCase);
            var isDownloading = IsDownloadingModel && string.Equals(item.Name, DownloadingModelName, StringComparison.OrdinalIgnoreCase);
            var isFailed = _failedPullModels.Contains(item.Name);

            var state = isDownloading
                ? ModelLifecycleState.Downloading
                : isActive
                    ? ModelLifecycleState.Active
                    : isInstalled
                        ? ModelLifecycleState.Installed
                        : isFailed
                            ? ModelLifecycleState.Failed
                            : ModelLifecycleState.Available;

            RecommendedModels.Add(new RecommendedModelRowViewModel(
                item,
                state,
                isDownloading ? DownloadProgressPercent : 0));
        }
    }

    public static bool IsLoopbackEndpoint(string? endpointUrl)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(endpointUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback ||
               string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
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
