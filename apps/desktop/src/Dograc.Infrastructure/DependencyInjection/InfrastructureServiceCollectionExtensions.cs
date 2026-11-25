using Dograc.Core.Models;
using Dograc.Core.Navigation;
using Dograc.Core.Services;
using Dograc.Infrastructure.Navigation;
using Dograc.Infrastructure.Services;
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

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IProcessRunner, SystemProcessRunner>();
        services.AddSingleton<IHealthProbe, NetworkHealthProbe>();
        services.AddSingleton<IServiceManager, LocalServiceManager>();
        services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();

        return services;
    }
}
