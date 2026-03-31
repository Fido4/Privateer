using System;
using System.Windows;
using Microsoft.Win32;
using Privateer.Desktop.Interop;
using Privateer.Desktop.Models;

namespace Privateer.Desktop.Services;

public sealed class ThemeManager
{
    private ResourceDictionary? _activeDictionary;

    public AppTheme RequestedTheme { get; private set; } = AppTheme.System;

    public AppTheme ResolvedTheme { get; private set; } = AppTheme.Light;

    public event Action<AppTheme>? ThemeChanged;

    public void ApplyTheme(Application application, AppTheme requestedTheme)
    {
        RequestedTheme = requestedTheme;
        ResolvedTheme = ResolveTheme(requestedTheme);

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(GetThemeResourcePath(ResolvedTheme), UriKind.Relative)
        };

        if (_activeDictionary is not null)
        {
            application.Resources.MergedDictionaries.Remove(_activeDictionary);
        }

        application.Resources.MergedDictionaries.Add(dictionary);
        _activeDictionary = dictionary;

        foreach (Window window in application.Windows)
        {
            ApplyWindowTheme(window);
        }

        ThemeChanged?.Invoke(ResolvedTheme);
    }

    public void ApplyWindowTheme(Window window)
    {
        DwmHelper.ApplyModernWindowStyle(window, UsesDarkWindowChrome(ResolvedTheme));
    }

    private static string GetThemeResourcePath(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Dark => "Resources/Theme.Dark.xaml",
            AppTheme.TanSepia => "Resources/Theme.TanSepia.xaml",
            AppTheme.BrownSepia => "Resources/Theme.BrownSepia.xaml",
            AppTheme.GreenSepia => "Resources/Theme.GreenSepia.xaml",
            _ => "Resources/Theme.Light.xaml"
        };
    }

    private static bool UsesDarkWindowChrome(AppTheme theme)
    {
        return theme is AppTheme.Dark or AppTheme.BrownSepia or AppTheme.GreenSepia;
    }

    private static AppTheme ResolveTheme(AppTheme requestedTheme)
    {
        if (requestedTheme != AppTheme.System)
        {
            return requestedTheme;
        }

        var registryValue = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1);

        return registryValue is int value && value == 0
            ? AppTheme.Dark
            : AppTheme.Light;
    }
}
