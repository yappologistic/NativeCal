namespace NativeCal.Helpers;

/// <summary>
/// Test-only shim so SettingsViewModel can compile in the net10.0 test project
/// without bringing in WinUI color types.
/// </summary>
public static class ColorHelper
{
    public static readonly string[] CalendarColors =
    {
        "#4A90D9",
        "#E74C3C",
        "#27AE60",
        "#F39C12",
        "#9B59B6",
        "#1ABC9C",
        "#E67E22",
        "#3498DB",
        "#E91E63",
        "#00BCD4"
    };
}
