using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

public sealed record RecommendedModelItem(string Name, string Description, string Command, bool IsRecommended);

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
    public string MetaLabel => $"{SizeLabel} · {Family}";
    public bool IsActive { get; }
    public bool IsNotActive => !IsActive;
}

/// <summary>
/// ViewModel for the Models page managing local Ollama connectivity, model discovery, and active model selection.
/// </summary>
public sealed partial class ModelsViewModel : PageViewModelBase
{
    private readonly IOllamaService _ollamaService;
    private readonly ISettingsStore _settingsStore;
    private readonly List<OllamaModelInfo> _rawModels = [];

    [ObservableProperty]
    private string _ollamaEndpointUrl;

    [ObservableProperty]
    private string _selectedModelName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionMessage = "연결 테스트 버튼을 눌러 로컬 Ollama 모델 목록을 확인하세요.";

    [ObservableProperty]
    private string? _copiedNotice;

    [ObservableProperty]
    private string _selectedSection = "설치된 모델";

    public ModelsViewModel(
        IOllamaService ollamaService,
        ISettingsStore settingsStore)
    {
        _ollamaService = ollamaService;
        _settingsStore = settingsStore;

        var current = _settingsStore.Current;
        _ollamaEndpointUrl = current.OllamaEndpointUrl;
        _selectedModelName = current.SelectedModelName;

        InstalledModels = [];
        RecommendedModels =
        [
            new RecommendedModelItem("qwen2.5:7b", "한국어 문서 질의응답 및 인용 정확도에 최적화된 추천 모델 (4.7 GB)", "ollama run qwen2.5:7b", true),
            new RecommendedModelItem("llama3.1:8b", "Meta 범용 오픈소스 언어 모델 (4.9 GB)", "ollama run llama3.1:8b", false),
            new RecommendedModelItem("gemma2:9b", "Google 고품질 요약·추론 모델 (5.4 GB)", "ollama run gemma2:9b", false),
            new RecommendedModelItem("deepseek-r1:8b", "단계별 추론 특화 로컬 모델 (4.9 GB)", "ollama run deepseek-r1:8b", false)
        ];

        _settingsStore.SettingsChanged += (_, updated) => RunOnUiThread(() =>
        {
            OllamaEndpointUrl = updated.OllamaEndpointUrl;
            SelectedModelName = updated.SelectedModelName;
            RebuildInstalledRows();
        });
    }

    public override string PageTitle => "Models";
    public override string PageDescription => "로컬 Ollama 서버를 연결하고 설치된 LLM 모델을 탐색·선택합니다.";

    public bool IsNotBusy => !IsBusy;

    public bool HasNoInstalledModels => InstalledModels.Count == 0;

    public ObservableCollection<InstalledModelRowViewModel> InstalledModels { get; }

    public IReadOnlyList<RecommendedModelItem> RecommendedModels { get; }

    [RelayCommand]
    private async Task TestAndRefreshModelsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ConnectionMessage = "로컬 Ollama 서버에 연결하는 중입니다...";
        try
        {
            var result = await _ollamaService.CheckAndListModelsAsync(OllamaEndpointUrl);
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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectInstalledModel(InstalledModelRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedModelName = row.Name;
        SaveModelSettings();
        RebuildInstalledRows();
        ConnectionMessage = $"활성 모델이 '{row.Name}'(으)로 변경되었습니다.";
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
        catch
        {
            CopiedNotice = $"명령어: {command}";
        }
    }

    public void SetInstalledModelsForDisplay(IEnumerable<OllamaModelInfo> models)
    {
        _rawModels.Clear();
        _rawModels.AddRange(models);
        IsConnected = _rawModels.Count > 0;
        RebuildInstalledRows();
    }

    private void RebuildInstalledRows()
    {
        InstalledModels.Clear();
        foreach (var model in _rawModels)
        {
            var active = string.Equals(model.Name, SelectedModelName, StringComparison.OrdinalIgnoreCase);
            InstalledModels.Add(new InstalledModelRowViewModel(model, active));
        }

        OnPropertyChanged(nameof(HasNoInstalledModels));
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
