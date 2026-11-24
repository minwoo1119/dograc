using Dograc.Core.Navigation;

namespace Dograc.Infrastructure.Navigation;

/// <summary>
/// Default in-memory navigation coordinator for the desktop shell.
/// </summary>
public sealed class NavigationService : INavigationService
{
    public NavigationPage CurrentPage { get; private set; } = NavigationPage.Chat;

    public event EventHandler<NavigationPage>? PageChanged;

    public void NavigateTo(NavigationPage page)
    {
        if (CurrentPage == page)
        {
            return;
        }

        CurrentPage = page;
        PageChanged?.Invoke(this, page);
    }
}
