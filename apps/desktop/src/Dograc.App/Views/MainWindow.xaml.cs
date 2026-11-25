using System.Windows;
using Dograc.App.ViewModels;

namespace Dograc.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

    private async void OnContextDocumentsDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainShellViewModel shellVm)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Length > 0)
        {
            await shellVm.DocumentsPanel.UploadFilePathsAsync(files);
        }
    }
}
