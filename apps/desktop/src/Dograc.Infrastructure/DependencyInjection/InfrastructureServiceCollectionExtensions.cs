using Dograc.Core.Navigation;
using Dograc.Infrastructure.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Dograc.Infrastructure.DependencyInjection;

/// <summary>
/// Registers infrastructure services for the dograc desktop client.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDogracInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        return services;
    }
}
