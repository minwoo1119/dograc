using System.IO;
using System.Windows;
using System.Windows.Threading;
using Dograc.App.Services;
using Dograc.App.ViewModels;
using Dograc.App.Views;
using Dograc.App.Views.Dialogs;
using Dograc.Core.Errors;
using Dograc.Core.Services;
using Dograc.Core.Theme;
using Dograc.Infrastructure.DependencyInjection;
using Dograc.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dograc.App;

public partial class App : Application
{
    private readonly IHost _host;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _isHandlingGlobalCrash;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDogracInfrastructure();

                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
                services.AddSingleton<RecoveryActionService>();
                services.AddSingleton<IRecoveryActionService>(sp => sp.GetRequiredService<RecoveryActionService>());

                services.AddSingleton<EvidenceViewModel>();
                services.AddSingleton<PreviewViewModel>();
                services.AddSingleton<ChatViewModel>();
                services.AddSingleton<DocumentsViewModel>();
                services.AddSingleton<WorkspacesViewModel>();
                services.AddSingleton<ModelsViewModel>();
                services.AddSingleton<SystemViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddTransient<SetupWizardViewModel>();
                services.AddSingleton<MainShellViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        RegisterGlobalExceptionHandlers();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var settingsStore = _host.Services.GetRequiredService<ISettingsStore>();
        var themeService = _host.Services.GetRequiredService<IThemeService>();
        var errorHandling = _host.Services.GetRequiredService<IErrorHandlingService>();
        var recoveryService = _host.Services.GetRequiredService<RecoveryActionService>();
        var diagnosticService = _host.Services.GetRequiredService<IDiagnosticService>();

        themeService.ApplyTheme(settingsStore.Current.ThemeMode);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var shellViewModel = _host.Services.GetRequiredService<MainShellViewModel>();

        // Surface any settings load/save corruption or permission errors immediately (never silent!)
        if (settingsStore is JsonSettingsStore jsonStore)
        {
            if (jsonStore.LastStorageError is not null)
            {
                errorHandling.HandleError(jsonStore.LastStorageError, notifyToast: true);
            }

            jsonStore.StorageErrorOccurred += (_, err) => Dispatcher.InvokeAsync(() =>
                errorHandling.HandleError(err, notifyToast: true));
        }

        shellViewModel.SetupWizardRequested += (_, summary) =>
        {
            var wizardVm = _host.Services.GetRequiredService<SetupWizardViewModel>();
            wizardVm.LoadSummary(summary);
            var wizard = new SetupWizardWindow(wizardVm) { Owner = mainWindow };
            wizard.ShowDialog();
        };

        shellViewModel.QuickCommandRequested += (_, _) =>
        {
            var palette = new QuickCommandWindow(shellViewModel.GetQuickCommandItems()) { Owner = mainWindow };
            palette.ShowDialog();
        };

        shellViewModel.PromptDialogRequested += (_, prompt) =>
        {
            var dialog = new PromptDialogWindow(prompt) { Owner = mainWindow };
            dialog.ShowDialog();
        };

        shellViewModel.NewWorkspaceDialogRequested += (_, _) =>
        {
            var dialog = new NewWorkspaceWindow(shellViewModel.WorkspacesPanel) { Owner = mainWindow };
            dialog.ShowDialog();
        };

        shellViewModel.SystemPanel.ExportDiagnosticsRequested += (_, _) =>
        {
            var dialog = new DiagnosticPreviewDialog(diagnosticService) { Owner = mainWindow };
            dialog.ShowDialog();
        };

        recoveryService.SetupWizardRequested += (_, _) => Dispatcher.InvokeAsync(() =>
            shellViewModel.OpenSetupWizardCommand.Execute(null));

        recoveryService.ErrorDetailDialogRequested += (_, error) => Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ErrorDetailDialog(error, diagnosticService) { Owner = mainWindow };
            dialog.ShowDialog();
        });

        recoveryService.ExportDiagnosticsDialogRequested += (_, focusError) => Dispatcher.InvokeAsync(() =>
        {
            var dialog = new DiagnosticPreviewDialog(diagnosticService, focusError) { Owner = mainWindow };
            dialog.ShowDialog();
        });

        recoveryService.FileReselectRequested += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            if (shellViewModel.DocumentsPanel.BrowseAndUploadCommand.CanExecute(null))
            {
                shellViewModel.DocumentsPanel.BrowseAndUploadCommand.Execute(null);
            }
        });

        recoveryService.ChatRestoreInputRequested += (_, _) => Dispatcher.InvokeAsync(() =>
            shellViewModel.ChatPanel.RestoreFailedInputCommand.Execute(null));

        mainWindow.Show();
        _ = shellViewModel.InitializeAsync(_shutdownCts.Token);

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _shutdownCts.Cancel();

        try
        {
            var settingsStore = _host.Services.GetRequiredService<ISettingsStore>();
            var serviceManager = _host.Services.GetRequiredService<IServiceManager>();
            if (settingsStore.Current.StopManagedServicesOnExit)
            {
                await serviceManager.StopManagedServicesAsync();
            }

            serviceManager.Dispose();
        }
        catch (Exception ex)
        {
            var logger = _host.Services.GetService<IStructuredLogger>();
            logger?.LogWarning("App", "System", "OnExit", $"종료 시 서비스 정리 중 경고: {ex.Message}");
        }

        await _host.StopAsync();
        _host.Dispose();
        _shutdownCts.Dispose();
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (_isHandlingGlobalCrash)
        {
            return;
        }

        _isHandlingGlobalCrash = true;
        try
        {
            var isFatal = e.Exception is OutOfMemoryException or StackOverflowException or AccessViolationException;
            var errorHandling = _host.Services.GetService<IErrorHandlingService>();
            var diagnosticService = _host.Services.GetService<IDiagnosticService>();
            var ctx = new ErrorContext("GlobalHandler", "Application", "UIThreadExecution");

            var appError = errorHandling?.Handle(e.Exception, ctx, notifyToast: false)
                ?? new ApplicationError(
                    "APP-999", ErrorSeverity.Critical, ErrorCategory.Application,
                    "예상하지 못한 UI 오류가 발생했습니다",
                    "화면 처리 중 분류되지 않은 예외가 발생했습니다.",
                    "내부 UI 처리 예외가 발생했습니다.", true,
                    "진행 중이던 작업이 중단되었습니다.",
                    "진단 보고서를 저장한 뒤 계속하거나 앱을 안전하게 종료해 주세요.",
                    [], e.Exception.ToString(), DateTimeOffset.Now, Guid.NewGuid().ToString("N")[..12], ctx);

            if (isFatal)
            {
                SaveEmergencyCrashReport(diagnosticService, appError);
                e.Handled = false;
                return;
            }

            // Show structured ErrorDetailDialog with option to continue or safely exit.
            var dialog = new ErrorDetailDialog(appError, diagnosticService, allowSafeExit: true);
            if (MainWindow is not null && MainWindow.IsVisible)
            {
                dialog.Owner = MainWindow;
            }
            dialog.ShowDialog();

            if (dialog.ShouldSafeExit)
            {
                e.Handled = true;
                Shutdown(1);
            }
            else
            {
                e.Handled = true;
            }
        }
        finally
        {
            _isHandlingGlobalCrash = false;
        }
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        var errorHandling = _host.Services.GetService<IErrorHandlingService>();
        var diagnosticService = _host.Services.GetService<IDiagnosticService>();
        var err = errorHandling?.Handle(
            ex,
            new ErrorContext("GlobalHandler", "Application", "AppDomainUnhandled"),
            notifyToast: false);

        SaveEmergencyCrashReport(diagnosticService, err, ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var errorHandling = _host.Services.GetService<IErrorHandlingService>();
        errorHandling?.Handle(
            e.Exception.GetBaseException(),
            new ErrorContext("GlobalHandler", "BackgroundTask", "UnobservedTask"),
            notifyToast: false);
        e.SetObserved();
    }

    private void SaveEmergencyCrashReport(IDiagnosticService? diagnosticService, ApplicationError? error, Exception? fallbackEx = null)
    {
        try
        {
            var logDir = _host.Services.GetService<IStructuredLogger>()?.LogDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dograc", "logs");
            Directory.CreateDirectory(logDir);
            var crashPath = Path.Combine(logDir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var body = diagnosticService?.BuildSanitizedReport(error)
                ?? $"[{DateTimeOffset.Now:O}] Fatal crash: {fallbackEx?.GetType().FullName}: {fallbackEx?.Message}";
            File.WriteAllText(crashPath, body);
        }
        catch (Exception writeEx)
        {
            System.Diagnostics.Debug.WriteLine($"Failed writing crash report: {writeEx.Message}");
        }
    }
}
