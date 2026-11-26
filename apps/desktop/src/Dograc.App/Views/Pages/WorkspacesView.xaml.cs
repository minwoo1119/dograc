using System.Windows;
using System.Windows.Controls;

namespace Dograc.App.Views.Pages;

public partial class WorkspacesView : UserControl
{
    public WorkspacesView()
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
}
