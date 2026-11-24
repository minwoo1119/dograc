using System.Windows;
using Dograc.Core.Theme;
using Microsoft.Win32;

namespace Dograc.App.Services;

/// <summary>
/// Dynamically swaps Light and Dark ResourceDictionary color tokens at runtime.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private static readonly Uri LightUri = new("Themes/Tokens/Colors.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkUri = new("Themes/Tokens/Colors.Dark.xaml", UriKind.Relative);

    public AppThemeMode CurrentMode { get; private set; } = AppThemeMode.System;

    public AppThemeMode EffectiveTheme { get; private set; } = AppThemeMode.Light;

    public event EventHandler<AppThemeMode>? ThemeChanged;

    public void ApplyTheme(AppThemeMode mode)
    {
        CurrentMode = mode;
        EffectiveTheme = mode == AppThemeMode.System ? DetectWindowsTheme() : mode;

        var app = Application.Current;
        if (app is null)
        {
            ThemeChanged?.Invoke(this, EffectiveTheme);
            return;
        }

        var targetUri = EffectiveTheme == AppThemeMode.Dark ? DarkUri : LightUri;
        var merged = app.Resources.MergedDictionaries;

        var existing = merged.FirstOrDefault(d =>
            d.Source is not null &&
            (d.Source.OriginalString.Contains("Colors.Light.xaml", StringComparison.OrdinalIgnoreCase) ||
             d.Source.OriginalString.Contains("Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase)));

        var nextDict = new ResourceDictionary { Source = targetUri };

        if (existing is not null)
        {
            var index = merged.IndexOf(existing);
            merged[index] = nextDict;
        }
        else
        {
            merged.Insert(0, nextDict);
        }

        ThemeChanged?.Invoke(this, EffectiveTheme);
    }

    private static AppThemeMode DetectWindowsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intVal && intVal == 0)
            {
                return AppThemeMode.Dark;
            }
        }
        catch
        {
            // Fallback to Light if registry access is unavailable.
        }

        return AppThemeMode.Light;
    }
}
