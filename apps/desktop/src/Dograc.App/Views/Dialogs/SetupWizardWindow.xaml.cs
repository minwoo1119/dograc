using System.Windows;
using Dograc.App.ViewModels;

namespace Dograc.App.Views.Dialogs;

public partial class SetupWizardWindow : Window
{
    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += (_, _) => Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();
}
