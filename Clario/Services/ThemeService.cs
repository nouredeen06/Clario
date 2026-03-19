using System.Diagnostics;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clario.Services;

public class ThemeService
{
    public static void SwitchToTheme(ThemeVariant theme)
    {
        var app = Application.Current;
        if (app is null) return;

        app.RequestedThemeVariant = theme;
    }

    public static void SwitchToTheme(string theme)
    {
        var app = Application.Current;
        if (app is null) return;
        var themeVariant = theme switch
        {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
        app.RequestedThemeVariant = themeVariant;
    }

    public static bool IsDarkTheme => Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;
    public static bool IsLightTheme => Application.Current?.RequestedThemeVariant == ThemeVariant.Light;
}