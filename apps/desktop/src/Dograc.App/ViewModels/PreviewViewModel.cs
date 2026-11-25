using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Models;
using Dograc.Core.Services;
using Microsoft.Win32;

namespace Dograc.App.ViewModels;

/// <summary>
/// ViewModel for the right Context Panel Preview tab supporting inline PDF (WebView2),
/// TXT content viewing, local file linking, and cited excerpt fallback.
/// </summary>
public sealed partial class PreviewViewModel : ObservableObject
{
    private const int MaxTextPreviewChars = 150_000;

    private readonly ISettingsStore _settingsStore;

    [ObservableProperty]
    private bool _hasDocument;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private int _pageNumber = 1;

    [ObservableProperty]
    private string? _localFilePath;

    [ObservableProperty]
    private bool _hasLocalPdf;

    [ObservableProperty]
    private bool _hasTextPreview;

    [ObservableProperty]
    private bool _hasFallbackOnly;

    [ObservableProperty]
    private string _textPreviewContent = string.Empty;

    [ObservableProperty]
    private string _statusNote = "문서 목록이나 답변 인용([파일명, p.숫자])을 선택하면 원문을 확인할 수 있습니다.";

    [ObservableProperty]
    private string? _pdfNavigationUri;

    private string? _lastExcerptText;

    public PreviewViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public string PageBadgeLabel => $"p.{PageNumber}";

    public bool HasLocalFile => !string.IsNullOrWhiteSpace(LocalFilePath) && File.Exists(LocalFilePath);

    public event EventHandler<string>? PdfUriChanged;

    public void LoadDocument(PreviewDocumentRequest request)
    {
        HasDocument = true;
        FileName = request.FileName;
        PageNumber = Math.Max(1, request.PageNumber);
        _lastExcerptText = request.ExcerptText;
        OnPropertyChanged(nameof(PageBadgeLabel));

        RefreshFromLocalPath();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (PageNumber <= 1)
        {
            return;
        }

        PageNumber--;
        OnPropertyChanged(nameof(PageBadgeLabel));
        UpdatePdfUri();
    }

    [RelayCommand]
    private void NextPage()
    {
        PageNumber++;
        OnPropertyChanged(nameof(PageBadgeLabel));
        UpdatePdfUri();
    }

    [RelayCommand]
    private void LinkLocalFile()
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"'{FileName}' 원본 로컬 파일 선택",
            Filter = "문서 파일 (*.pdf;*.txt)|*.pdf;*.txt|모든 파일 (*.*)|*.*",
            FileName = FileName
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        _settingsStore.RegisterDocumentLocalPath(FileName, dialog.FileName);
        RefreshFromLocalPath();
    }

    [RelayCommand]
    private void OpenExternal()
    {
        if (!HasLocalFile || string.IsNullOrWhiteSpace(LocalFilePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(LocalFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusNote = $"외부 앱 실행 실패: {ex.Message}";
        }
    }

    private void RefreshFromLocalPath()
    {
        LocalFilePath = _settingsStore.ResolveDocumentLocalPath(FileName);
        OnPropertyChanged(nameof(HasLocalFile));

        HasLocalPdf = false;
        HasTextPreview = false;
        HasFallbackOnly = false;
        TextPreviewContent = string.Empty;
        PdfNavigationUri = null;

        if (!string.IsNullOrWhiteSpace(LocalFilePath) && File.Exists(LocalFilePath))
        {
            var ext = Path.GetExtension(LocalFilePath).ToLowerInvariant();
            if (ext == ".pdf")
            {
                HasLocalPdf = true;
                StatusNote = $"로컬 PDF 연결됨 ({PageBadgeLabel})";
                UpdatePdfUri();
                return;
            }

            try
            {
                var content = File.ReadAllText(LocalFilePath);
                if (content.Length > MaxTextPreviewChars)
                {
                    content = content[..MaxTextPreviewChars] + "\n\n... (긴 텍스트 일부 생략)";
                }

                HasTextPreview = true;
                TextPreviewContent = content;
                StatusNote = $"로컬 텍스트 원문 표시 중 ({Path.GetFileName(LocalFilePath)})";
                return;
            }
            catch (Exception ex)
            {
                StatusNote = $"파일 읽기 실패: {ex.Message}";
            }
        }

        HasFallbackOnly = true;
        TextPreviewContent = string.IsNullOrWhiteSpace(_lastExcerptText)
            ? "서버에 저장된 문서의 로컬 원본 경로가 아직 연결되지 않았습니다.\n상단의 [로컬 파일 연결] 버튼으로 원본 PDF/TXT 파일을 지정하면 이 패널에서 즉시 원문과 인용 페이지를 열람할 수 있습니다."
            : _lastExcerptText;
        StatusNote = "로컬 원본 미연결 — 인용 발췌문 미리보기 표시 중";
    }

    private void UpdatePdfUri()
    {
        if (!HasLocalPdf || string.IsNullOrWhiteSpace(LocalFilePath))
        {
            return;
        }

        var fileUri = new Uri(Path.GetFullPath(LocalFilePath)).AbsoluteUri;
        PdfNavigationUri = $"{fileUri}#page={PageNumber}";
        StatusNote = $"로컬 PDF 표시 중 ({PageBadgeLabel})";
        PdfUriChanged?.Invoke(this, PdfNavigationUri);
    }
}
