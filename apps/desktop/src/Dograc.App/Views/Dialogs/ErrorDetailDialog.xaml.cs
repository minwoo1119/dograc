using System.Windows;
using System.Windows.Input;
using Dograc.Core.Errors;

namespace Dograc.App.Views.Dialogs;

public partial class ErrorDetailDialog : Window
{
    private readonly ApplicationError _error;
    private readonly IDiagnosticService? _diagnosticService;

    public ErrorDetailDialog(
        ApplicationError error,
        IDiagnosticService? diagnosticService = null,
        bool allowSafeExit = false)
    {
        InitializeComponent();
        _error = error;
        _diagnosticService = diagnosticService;

        ErrorCodeText.Text = $"{error.ErrorCode} · cid:{error.CorrelationId}";
        TimestampText.Text = error.FormattedTimestamp;
        TitleText.Text = error.Title;
        DescriptionText.Text = error.Description;
        CauseLabelText.Text = error.CausePrefixLabel;
        CauseText.Text = error.Cause;
        ImpactText.Text = error.Impact;
        ResolutionText.Text = error.Resolution;
        TechnicalBox.Text = error.TechnicalDetails;

        if (allowSafeExit)
        {
            SafeExitButton.Visibility = Visibility.Visible;
        }
    }

    public bool ShouldSafeExit { get; private set; }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = _diagnosticService?.BuildSanitizedReport(_error) ?? _error.TechnicalDetails;
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard error: {ex.Message}");
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_diagnosticService is null)
        {
            return;
        }

        var preview = new DiagnosticPreviewDialog(_diagnosticService, _error) { Owner = this };
        preview.ShowDialog();
    }

    private void OnSafeExitClick(object sender, RoutedEventArgs e)
    {
        ShouldSafeExit = true;
        Close();
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
