using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

public sealed record EvidenceChunkItem(
    string SourceFileName,
    int PageNumber,
    double Score,
    string Text,
    bool IsHighlighted)
{
    public string PageLabel => $"p.{PageNumber}";
    public string ScoreLabel => $"유사도 {Score * 100.0:0.0}%";
}

/// <summary>
/// ViewModel for the right Context Panel Evidence tab showing RAG trace metadata, retrieved chunks, and prompt.
/// </summary>
public sealed partial class EvidenceViewModel : ObservableObject
{
    private readonly IDogracApiClient _apiClient;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasTrace;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _modelName = "N/A";

    [ObservableProperty]
    private string _latencyLabel = "0 ms";

    [ObservableProperty]
    private string? _promptText;

    [ObservableProperty]
    private bool _showPrompt;

    [ObservableProperty]
    private string _chunkCountLabel = "0개 발췌";

    public EvidenceViewModel(IDogracApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<EvidenceChunkItem> Chunks { get; } = [];

    public event EventHandler<PreviewDocumentRequest>? PreviewRequested;

    public async Task LoadTraceAsync(EvidenceSelectionRequest request)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        ErrorMessage = null;
        ShowPrompt = false;

        try
        {
            var trace = await _apiClient.GetTraceAsync(request.TraceId, ct);
            RunOnUiThread(() => ApplyTrace(trace, request));
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer trace request.
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                HasTrace = false;
                Chunks.Clear();
                ErrorMessage = $"근거 데이터를 불러오지 못했습니다: {ex.Message}";
            });
        }
        finally
        {
            RunOnUiThread(() => IsLoading = false);
        }
    }

    public void Clear()
    {
        _loadCts?.Cancel();
        HasTrace = false;
        ErrorMessage = null;
        ModelName = "N/A";
        LatencyLabel = "0 ms";
        PromptText = null;
        ShowPrompt = false;
        ChunkCountLabel = "0개 발췌";
        Chunks.Clear();
    }

    [RelayCommand]
    private void TogglePrompt()
    {
        ShowPrompt = !ShowPrompt;
    }

    [RelayCommand]
    private void OpenInPreview(EvidenceChunkItem? chunk)
    {
        if (chunk is null)
        {
            return;
        }

        PreviewRequested?.Invoke(this, new PreviewDocumentRequest(chunk.SourceFileName, chunk.PageNumber, chunk.Text));
    }

    private void ApplyTrace(TraceDto trace, EvidenceSelectionRequest request)
    {
        HasTrace = true;
        ModelName = string.IsNullOrWhiteSpace(trace.ModelName) ? "N/A" : trace.ModelName;
        LatencyLabel = $"{trace.LatencyMs ?? 0} ms";
        PromptText = trace.Prompt;

        Chunks.Clear();
        foreach (var chunk in trace.RetrievedChunks)
        {
            var matchesFile = !string.IsNullOrWhiteSpace(request.HighlightFileName) &&
                              chunk.SourceFileName.Contains(request.HighlightFileName, StringComparison.OrdinalIgnoreCase);
            var matchesPage = request.HighlightPageNumber is null || chunk.PageNumber == request.HighlightPageNumber.Value;

            Chunks.Add(new EvidenceChunkItem(
                chunk.SourceFileName,
                chunk.PageNumber,
                chunk.Score,
                chunk.Text,
                matchesFile && matchesPage));
        }

        ChunkCountLabel = $"{Chunks.Count}개 발췌";
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
