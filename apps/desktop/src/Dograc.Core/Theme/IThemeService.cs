namespace Dograc.Core.Theme;

/// <summary>
/// Supported application appearance modes.
/// </summary>
public enum AppThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>
/// Manages dynamic application theme switching across ResourceDictionary tokens.
/// </summary>
public interface IThemeService
{
    AppThemeMode CurrentMode { get; }

    AppThemeMode EffectiveTheme { get; }

    event EventHandler<AppThemeMode>? ThemeChanged;

    void ApplyTheme(AppThemeMode mode);
}
