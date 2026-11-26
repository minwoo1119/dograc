using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;

namespace Dograc.App.ViewModels;

public sealed record EvidenceChunkItem(
    int ChunkNumber,
    string SourceFileName,
    int PageNumber,
    double Score,
    string Text,
    bool IsHighlighted)
{
    public string MetaLabel => $"Page {PageNumber} · Chunk {ChunkNumber}";
    public string ScoreLabel => $"{Score * 100.0:0.0}%";
    public string SimilarityDisplayLabel => $"유사도 {ScoreLabel}";
}

/// <summary>
/// ViewModel for the flat, hairline-separated right Context Panel Evidence tab.
/// Prompt text is displayed in a dedicated modal dialog per design/COMPONENTS.md.
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
    private string _chunkCountLabel = "검색된 발췌문 0개";

    public EvidenceViewModel(IDogracApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<EvidenceChunkItem> Chunks { get; } = [];

    public event EventHandler<PreviewDocumentRequest>? PreviewRequested;

    public event EventHandler<string>? PromptDialogRequested;

    public event EventHandler<int>? HighlightedChunkChanged;

    public async Task LoadTraceAsync(EvidenceSelectionRequest request)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        ErrorMessage = null;

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
        ChunkCountLabel = "검색된 발췌문 0개";
        Chunks.Clear();
    }

    [RelayCommand]
    private void ShowPromptDialog()
    {
        if (!string.IsNullOrWhiteSpace(PromptText))
        {
            PromptDialogRequested?.Invoke(this, PromptText);
        }
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
        var matchedAny = false;
        var built = new List<EvidenceChunkItem>();
        for (var i = 0; i < trace.RetrievedChunks.Count; i++)
        {
            var chunk = trace.RetrievedChunks[i];
            var matchesFile = !string.IsNullOrWhiteSpace(request.HighlightFileName) &&
                              chunk.SourceFileName.Contains(request.HighlightFileName, StringComparison.OrdinalIgnoreCase);
            var matchesPage = request.HighlightPageNumber is null || chunk.PageNumber == request.HighlightPageNumber.Value;
            var isMatch = matchesFile && matchesPage;
            if (isMatch)
            {
                matchedAny = true;
            }

            built.Add(new EvidenceChunkItem(
                i + 1,
                chunk.SourceFileName,
                chunk.PageNumber,
                chunk.Score,
                chunk.Text,
                isMatch));
        }

        // If opened via general "답변 근거 보기 →" without a specific citation, highlight the top chunk.
        if (!matchedAny && built.Count > 0)
        {
            built[0] = built[0] with { IsHighlighted = true };
        }

        var highlightedIndex = built.FindIndex(item => item.IsHighlighted);
        foreach (var item in built)
        {
            Chunks.Add(item);
        }

        ChunkCountLabel = $"검색된 발췌문 {Chunks.Count}개";
        if (highlightedIndex >= 0)
        {
            HighlightedChunkChanged?.Invoke(this, highlightedIndex);
        }
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
