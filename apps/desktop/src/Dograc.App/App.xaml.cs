using System.Windows;
using Dograc.App.Services;
using Dograc.App.ViewModels;
using Dograc.App.Views;
using Dograc.App.Views.Dialogs;
using Dograc.Core.Services;
using Dograc.Core.Theme;
using Dograc.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dograc.App;

public partial class App : Application
{
    private readonly IHost _host;
    private readonly CancellationTokenSource _shutdownCts = new();

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDogracInfrastructure();

                services.AddSingleton<IThemeService, ThemeService>();

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
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var settingsStore = _host.Services.GetRequiredService<ISettingsStore>();
        var themeService = _host.Services.GetRequiredService<IThemeService>();
        themeService.ApplyTheme(settingsStore.Current.ThemeMode);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var shellViewModel = _host.Services.GetRequiredService<MainShellViewModel>();

        shellViewModel.SetupWizardRequested += (_, summary) =>
        {
            var wizardVm = _host.Services.GetRequiredService<SetupWizardViewModel>();
            wizardVm.LoadSummary(summary);
            var wizard = new SetupWizardWindow(wizardVm) { Owner = mainWindow };
            wizard.ShowDialog();
        };

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
        catch
        {
            // Ensure application exits cleanly even if subprocess cleanup reports errors.
        }

        await _host.StopAsync();
        _host.Dispose();
        _shutdownCts.Dispose();
        base.OnExit(e);
    }
}
