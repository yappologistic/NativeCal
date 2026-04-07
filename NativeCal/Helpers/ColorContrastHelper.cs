using System;
using System.Globalization;

namespace NativeCal.Helpers;

/// <summary>
/// Computes readable foreground colors for solid event backgrounds.
/// The helper is UI-agnostic so its contrast decisions can be covered by
/// deterministic unit tests instead of screenshot-only verification.
/// </summary>
public static class ColorContrastHelper
{
    public static string ResolveTextColorHex(string backgroundHex)
    {
        double backgroundLuminance = GetRelativeLuminance(backgroundHex);
        double blackContrast = GetContrastRatioForLuminance(0.0, backgroundLuminance);
        double whiteContrast = GetContrastRatioForLuminance(1.0, backgroundLuminance);

        return blackContrast >= whiteContrast ? "#000000" : "#FFFFFF";
    }

    public static double GetContrastRatio(string foregroundHex, string backgroundHex)
    {
        double foregroundLuminance = GetRelativeLuminance(foregroundHex);
        double backgroundLuminance = GetRelativeLuminance(backgroundHex);
        return GetContrastRatioForLuminance(foregroundLuminance, backgroundLuminance);
    }

    private static double GetRelativeLuminance(string hex)
    {
        var (red, green, blue) = ParseRgb(hex);
        double redLinear = ToLinearChannel(red / 255.0);
        double greenLinear = ToLinearChannel(green / 255.0);
        double blueLinear = ToLinearChannel(blue / 255.0);

        return (0.2126 * redLinear) + (0.7152 * greenLinear) + (0.0722 * blueLinear);
    }

    private static double GetContrastRatioForLuminance(double firstLuminance, double secondLuminance)
    {
        double lighter = Math.Max(firstLuminance, secondLuminance);
        double darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double ToLinearChannel(double channel)
    {
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static (byte Red, byte Green, byte Blue) ParseRgb(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new ArgumentException("Hex color string cannot be null or empty.", nameof(hex));
        }

        string normalized = hex.Trim().TrimStart('#');
        if (normalized.Length == 8)
        {
            // Ignore alpha for contrast decisions because the stored calendar color
            // represents the intended visible hue for the chip background.
            normalized = normalized.Substring(2, 6);
        }

        if (normalized.Length != 6)
        {
            throw new ArgumentException($"Invalid hex color format: #{normalized}. Expected #RRGGBB or #AARRGGBB.", nameof(hex));
        }

        return (
            byte.Parse(normalized.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
