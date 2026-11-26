using System.Windows;
using System.Windows.Controls;
using Dograc.App.ViewModels;

namespace Dograc.App.Views;

public partial class MainWindow : Window
{
    private readonly MainShellViewModel _viewModel;
    private bool _webViewInitialized;

    public MainWindow(MainShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.PreviewPanel.PdfUriChanged += (_, uri) =>
            _ = Dispatcher.InvokeAsync(() => NavigatePdfPreviewAsync(uri));
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.IsOpen = true;
        }
    }

    private async void OnContextDocumentsDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Length > 0)
        {
            await _viewModel.DocumentsPanel.UploadFilePathsAsync(files);
        }
    }

    private async Task NavigatePdfPreviewAsync(string uri)
    {
        try
        {
            if (!_webViewInitialized)
            {
                await PdfWebView.EnsureCoreWebView2Async();
                _webViewInitialized = true;
            }

            PdfWebView.CoreWebView2?.Navigate(uri);
        }
        catch (Exception ex)
        {
            _viewModel.PreviewPanel.StatusNote =
                $"내장 PDF 뷰어 초기화 실패({ex.Message}). 상단 [외부 앱] 버튼으로 열 수 있습니다.";
        }
    }
}
