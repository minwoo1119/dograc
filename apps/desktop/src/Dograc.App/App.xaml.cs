using System.Windows;
using Dograc.App.Services;
using Dograc.App.ViewModels;
using Dograc.App.Views;
using Dograc.Core.Theme;
using Dograc.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dograc.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDogracInfrastructure();

                services.AddSingleton<IThemeService, ThemeService>();

                services.AddSingleton<ChatViewModel>();
                services.AddSingleton<DocumentsViewModel>();
                services.AddSingleton<WorkspacesViewModel>();
                services.AddSingleton<ModelsViewModel>();
                services.AddSingleton<SystemViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainShellViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var themeService = _host.Services.GetRequiredService<IThemeService>();
        themeService.ApplyTheme(AppThemeMode.System);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
