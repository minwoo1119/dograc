using System.Windows;
using System.Windows.Input;

namespace Dograc.App.Views.Dialogs;

public partial class PromptDialogWindow : Window
{
    private readonly string _promptText;

    public PromptDialogWindow(string promptText)
    {
        InitializeComponent();
        _promptText = promptText;
        PromptBox.Text = promptText;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_promptText);
        }
        catch
        {
            // Ignore clipboard contention.
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
