using System.Windows;
using System.Windows.Input;
using Dograc.App.ViewModels;

namespace Dograc.App.Views.Dialogs;

public partial class NewWorkspaceWindow : Window
{
    private readonly WorkspacesViewModel _viewModel;

    public NewWorkspaceWindow(WorkspacesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => NameInput.Focus();
    }

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.NewWorkspaceName))
        {
            NameInput.Focus();
            return;
        }

        await _viewModel.CreateWorkspaceCommand.ExecuteAsync(null);
        if (string.IsNullOrWhiteSpace(_viewModel.ErrorMessage))
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnCreateClick(sender, e);
        }
    }
}
