using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dograc.App.ViewModels;

namespace Dograc.App.Views.Dialogs;

public partial class QuickCommandWindow : Window
{
    private readonly IReadOnlyList<QuickCommandEntry> _allItems;

    public QuickCommandWindow(IReadOnlyList<QuickCommandEntry> items)
    {
        InitializeComponent();
        _allItems = items;
        ApplyFilter(string.Empty);
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            e.Handled = true;
            ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            return;
        }

        if (e.Key == Key.Up && ResultsList.Items.Count > 0)
        {
            e.Handled = true;
            ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ExecuteSelected();
        }
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelected();
    }

    private void ApplyFilter(string query)
    {
        var q = query.Trim();
        var filtered = string.IsNullOrWhiteSpace(q)
            ? _allItems.ToList()
            : _allItems.Where(i =>
                i.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Category.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        ResultsList.ItemsSource = filtered;
        if (filtered.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
        }
    }

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is QuickCommandEntry entry)
        {
            Close();
            entry.Execute();
        }
    }
}
