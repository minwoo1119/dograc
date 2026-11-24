using Dograc.Core.Navigation;
using Dograc.Infrastructure.DependencyInjection;
using Dograc.Infrastructure.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dograc.Core.Tests;

public sealed class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_ChangesCurrentPage_AndRaisesEvent()
    {
        var service = new NavigationService();
        NavigationPage? raisedPage = null;
        service.PageChanged += (_, page) => raisedPage = page;

        service.NavigateTo(NavigationPage.Documents);

        Assert.Equal(NavigationPage.Documents, service.CurrentPage);
        Assert.Equal(NavigationPage.Documents, raisedPage);
    }

    [Fact]
    public void NavigateTo_SamePage_DoesNotRaiseEvent()
    {
        var service = new NavigationService();
        var raisedCount = 0;
        service.PageChanged += (_, _) => raisedCount++;

        service.NavigateTo(NavigationPage.Chat);

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void AddDogracInfrastructure_ResolvesNavigationService()
    {
        var services = new ServiceCollection();
        services.AddDogracInfrastructure();

        using var provider = services.BuildServiceProvider();
        var nav = provider.GetRequiredService<INavigationService>();

        Assert.Equal(NavigationPage.Chat, nav.CurrentPage);
    }
}
