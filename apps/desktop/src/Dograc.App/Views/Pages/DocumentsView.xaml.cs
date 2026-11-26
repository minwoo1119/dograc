using System.Windows;
using System.Windows.Controls;
using Dograc.App.ViewModels;

namespace Dograc.App.Views.Pages;

public partial class DocumentsView : UserControl
{
    public DocumentsView()
    {
        InitializeComponent();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.IsOpen = true;
        }
    }

    private async void OnFilesDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not DocumentsViewModel vm)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Length > 0)
        {
            await vm.UploadFilePathsAsync(files);
        }
    }
}
