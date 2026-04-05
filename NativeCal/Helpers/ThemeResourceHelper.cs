using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NativeCal.Helpers;

/// <summary>
/// Resolves WinUI 3 theme resources based on the actual element theme,
/// not the Application-level theme. This is critical when the user selects
/// a theme (Light/Dark) different from the system default: XAML {ThemeResource}
/// bindings update automatically, but C# Application.Current.Resources["key"]
/// always resolves against the system theme.
///
/// For the core text/card/divider brushes used in code-behind, we maintain a
/// built-in table of the standard WinUI 3 color values to guarantee correct
/// resolution regardless of how the XAML resource tree is structured.
/// </summary>
public static class ThemeResourceHelper
{
    // ── Static theme tracking ────────────────────────────────────────
    // Set by MainWindow when the theme changes. This is the single source
    // of truth for which theme the app should use, accessible even when
    // App.MainAppWindow is null (during window construction).
    private static ElementTheme _appRequestedTheme = ElementTheme.Default;

    /// <summary>
    /// Call this from MainWindow whenever the app theme changes.
    /// </summary>
    public static void SetAppTheme(ElementTheme theme)
    {
        _appRequestedTheme = theme;
    }

    // ── Built-in WinUI 3 system color table ─────────────────────────────
    // These match the standard WinUI 3 / Windows 11 system theme colors.
    // We define them here so code-behind brush lookups always resolve to
    // the correct theme, even when Application.Current.Resources resolves
    // against the system theme instead of the app's RequestedTheme.
    private static readonly Dictionary<string, Windows.UI.Color> LightColors = new()
    {
        ["TextFillColorPrimaryBrush"]             = Windows.UI.Color.FromArgb(0xE4, 0x00, 0x00, 0x00),
        ["TextFillColorSecondaryBrush"]           = Windows.UI.Color.FromArgb(0x9E, 0x00, 0x00, 0x00),
        ["TextFillColorDisabledBrush"]            = Windows.UI.Color.FromArgb(0x5C, 0x00, 0x00, 0x00),
        ["TextOnAccentFillColorPrimaryBrush"]     = Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
        ["CardBackgroundFillColorDefaultBrush"]   = Windows.UI.Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF),
        ["CardBackgroundFillColorSecondaryBrush"] = Windows.UI.Color.FromArgb(0x80, 0xF6, 0xF6, 0xF6),
        ["CardStrokeColorDefaultBrush"]           = Windows.UI.Color.FromArgb(0x0F, 0x00, 0x00, 0x00),
        ["DividerStrokeColorDefaultBrush"]        = Windows.UI.Color.FromArgb(0x14, 0x00, 0x00, 0x00),
    };

    private static readonly Dictionary<string, Windows.UI.Color> DarkColors = new()
    {
        ["TextFillColorPrimaryBrush"]             = Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
        ["TextFillColorSecondaryBrush"]           = Windows.UI.Color.FromArgb(0xC8, 0xFF, 0xFF, 0xFF),
        ["TextFillColorDisabledBrush"]            = Windows.UI.Color.FromArgb(0x5D, 0xFF, 0xFF, 0xFF),
        ["TextOnAccentFillColorPrimaryBrush"]     = Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
        ["CardBackgroundFillColorDefaultBrush"]   = Windows.UI.Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF),
        ["CardBackgroundFillColorSecondaryBrush"] = Windows.UI.Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF),
        ["CardStrokeColorDefaultBrush"]           = Windows.UI.Color.FromArgb(0x19, 0x00, 0x00, 0x00),
        ["DividerStrokeColorDefaultBrush"]        = Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF),
    };

    /// <summary>
    /// Resolves a theme-aware brush resource by key, using the specified element theme.
    /// First checks the built-in color table, then falls back to ThemeDictionaries,
    /// then to Application.Current.Resources.
    /// </summary>
    public static Brush GetBrush(string key, ElementTheme theme)
    {
        // 1) Built-in color table — guaranteed correct for the requested theme
        var colorTable = theme == ElementTheme.Light ? LightColors : DarkColors;
        if (colorTable.TryGetValue(key, out Windows.UI.Color color))
            return new SolidColorBrush(color);

        // 2) ThemeDictionaries search (for AccentFillColorDefaultBrush etc.)
        if (TryGetResource(key, theme, out object? value) && value is Brush brush)
            return brush;

        // 3) Fallback: Application-level lookup (resolves against system theme)
        if (Application.Current.Resources.TryGetValue(key, out object? fallback) && fallback is Brush fallbackBrush)
            return fallbackBrush;

        // Last resort: transparent
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    /// <summary>
    /// Resolves a theme-aware resource of any type by key, using the specified element theme.
    /// </summary>
    public static object? GetResource(string key, ElementTheme theme)
    {
        if (TryGetResource(key, theme, out object? value))
            return value;

        if (Application.Current.Resources.TryGetValue(key, out object? fallback))
            return fallback;

        return null;
    }

    /// <summary>
    /// Determines the effective theme for resolution. If the element theme is Default,
    /// we check the statically-tracked app RequestedTheme, then the MainWindow's root
    /// element, then fall back to the system theme.
    /// </summary>
    public static ElementTheme GetEffectiveTheme(FrameworkElement element)
    {
        // 1) If the app has explicitly set a theme (Light or Dark), use that.
        //    This is the most reliable source since it's set before any pages
        //    are created and doesn't depend on visual tree propagation.
        if (_appRequestedTheme == ElementTheme.Light) return ElementTheme.Light;
        if (_appRequestedTheme == ElementTheme.Dark) return ElementTheme.Dark;

        // 2) App theme is Default — use the element's ActualTheme if available
        var actual = element.ActualTheme;
        if (actual == ElementTheme.Light) return ElementTheme.Light;
        if (actual == ElementTheme.Dark) return ElementTheme.Dark;

        // 3) Check the MainWindow's root element
        if (App.MainAppWindow?.Content is FrameworkElement root)
        {
            if (root.ActualTheme == ElementTheme.Light)
                return ElementTheme.Light;
            if (root.ActualTheme == ElementTheme.Dark)
                return ElementTheme.Dark;
        }

        // Final fallback: system theme
        return Application.Current.RequestedTheme == ApplicationTheme.Light
            ? ElementTheme.Light
            : ElementTheme.Dark;
    }

    /// <summary>
    /// Convenience: get a brush from an element's actual theme.
    /// </summary>
    public static Brush GetBrush(string key, FrameworkElement element)
    {
        return GetBrush(key, GetEffectiveTheme(element));
    }

    private static bool TryGetResource(string key, ElementTheme theme, out object? value)
    {
        value = null;

        // WinUI 3 ThemeDictionaries use "Light" for light and "Default" for dark.
        string themeDictionaryKey = theme == ElementTheme.Light ? "Light" : "Default";

        // Search the Application-level merged dictionaries (XamlControlsResources etc.)
        if (TryGetFromDictionary(Application.Current.Resources, key, themeDictionaryKey, out value))
            return true;

        return false;
    }

    private static bool TryGetFromDictionary(ResourceDictionary dict, string key, string themeDictionaryKey, out object? value)
    {
        value = null;

        // Check ThemeDictionaries at this level
        if (dict.ThemeDictionaries.Count > 0)
        {
            if (dict.ThemeDictionaries.TryGetValue(themeDictionaryKey, out object? themeDict) &&
                themeDict is ResourceDictionary td &&
                td.TryGetValue(key, out value))
            {
                return true;
            }

            // For dark theme, also try "Dark" key as some dictionaries use it
            if (themeDictionaryKey == "Default" &&
                dict.ThemeDictionaries.TryGetValue("Dark", out object? darkDict) &&
                darkDict is ResourceDictionary dd &&
                dd.TryGetValue(key, out value))
            {
                return true;
            }
        }

        // Recursively search merged dictionaries
        foreach (var merged in dict.MergedDictionaries)
        {
            if (TryGetFromDictionary(merged, key, themeDictionaryKey, out value))
                return true;
        }

        return false;
    }
}
