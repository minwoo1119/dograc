using System.Windows;
using System.Windows.Input;
using Dograc.Core.Errors;
using Microsoft.Win32;

namespace Dograc.App.Views.Dialogs;

public partial class DiagnosticPreviewDialog : Window
{
    private readonly IDiagnosticService _diagnosticService;
    private readonly ApplicationError? _focusError;
    private readonly string _reportText;

    public DiagnosticPreviewDialog(IDiagnosticService diagnosticService, ApplicationError? focusError = null)
    {
        InitializeComponent();
        _diagnosticService = diagnosticService;
        _focusError = focusError;

        SummaryText.Text = _diagnosticService.GetIncludedDataSummary();
        _reportText = _diagnosticService.BuildSanitizedReport(_focusError);
        ReportPreviewBox.Text = _reportText;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_reportText);
            StatusText.Text = "진단 보고서를 클립보드에 복사했습니다.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"클립보드 복사 실패: {ex.Message}";
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "dograc 진단 보고서 저장",
            Filter = "텍스트 보고서 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            FileName = $"dograc-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        try
        {
            await _diagnosticService.ExportReportAsync(dialog.FileName, _focusError);
            StatusText.Text = $"저장 완료: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"보고서 저장 실패: {ex.Message}";
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
