namespace Dograc.Core.Navigation;

/// <summary>
/// Represents the top-level independent pages accessible from the Navigation Rail.
/// </summary>
public enum NavigationPage
{
    Chat,
    Documents,
    Workspaces,
    Models,
    System,
    Settings
}

/// <summary>
/// Metadata describing a single item in the left Navigation Rail.
/// </summary>
public sealed record NavigationItem(
    NavigationPage Page,
    string Label,
    string Glyph,
    string Tooltip);
