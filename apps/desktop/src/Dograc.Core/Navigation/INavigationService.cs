namespace Dograc.Core.Navigation;

/// <summary>
/// Coordinates navigation between the primary pages in the Main Shell.
/// </summary>
public interface INavigationService
{
    NavigationPage CurrentPage { get; }

    event EventHandler<NavigationPage>? PageChanged;

    void NavigateTo(NavigationPage page);
}
