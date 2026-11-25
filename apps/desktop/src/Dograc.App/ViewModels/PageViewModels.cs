using CommunityToolkit.Mvvm.ComponentModel;

namespace Dograc.App.ViewModels;

/// <summary>
/// Base class for top-level navigation page ViewModels.
/// </summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    public abstract string PageTitle { get; }

    public abstract string PageDescription { get; }
}
