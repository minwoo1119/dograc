using Dograc.Core.Models;
using Dograc.Core.Navigation;
using Dograc.Core.Services;
using Dograc.Infrastructure.Api;
using Dograc.Infrastructure.Navigation;
using Dograc.Infrastructure.Services;
using Dograc.Infrastructure.Settings;
using Dograc.Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;

namespace Dograc.Infrastructure.DependencyInjection;

/// <summary>
/// Registers infrastructure services for the dograc desktop client.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDogracInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient("HealthProbe");

        services.AddSingleton(_ => new ServiceRuntimeOptions(
            ProjectRoot: ProjectRootResolver.ResolveProjectRoot()));

        services.AddHttpClient<IDogracApiClient, DogracApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<ServiceRuntimeOptions>();
            client.BaseAddress = new Uri($"{options.ApiBaseUrl.TrimEnd('/')}/api/v1/");
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddHttpClient<IOllamaService, OllamaService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHttpClient<IUpdateService, GitHubUpdateService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(6);
        });

        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IProcessRunner, SystemProcessRunner>();
        services.AddSingleton<IHealthProbe, NetworkHealthProbe>();
        services.AddSingleton<IServiceManager, LocalServiceManager>();
        services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();
        services.AddSingleton<IWorkspaceContext, WorkspaceContext>();

        return services;
    }
}
