using System;
using System.Text.RegularExpressions;

namespace NativeCal.Tests.Helpers;

/// <summary>
/// Tests for the TimeSpanToStringConverter's ConvertBack regex logic.
/// The converter depends on WinUI types, so we test the core parsing logic directly.
/// </summary>
public class TimeSpanToStringConverterLogicTests
{
    // Extracted from TimeSpanToStringConverter.ConvertBack
    private static int ParseTimeString(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return 0;

        int totalMinutes = 0;

        var hourMatch = Regex.Match(str, @"(\d+)\s*hour");
        if (hourMatch.Success && int.TryParse(hourMatch.Groups[1].Value, out int hours))
        {
            totalMinutes += hours * 60;
        }

        var minuteMatch = Regex.Match(str, @"(\d+)\s*minute");
        if (minuteMatch.Success && int.TryParse(minuteMatch.Groups[1].Value, out int minutes))
        {
            totalMinutes += minutes;
        }

        if (totalMinutes == 0 && int.TryParse(str.Trim(), out int plainMinutes))
        {
            totalMinutes = plainMinutes;
        }

        return totalMinutes;
    }

    // Extracted from TimeSpanToStringConverter.Convert
    private static string FormatMinutes(int minutes)
    {
        if (minutes <= 0)
            return "0 minutes";

        int hours = minutes / 60;
        int remainingMinutes = minutes % 60;

        if (hours == 0)
        {
            return remainingMinutes == 1 ? "1 minute" : $"{remainingMinutes} minutes";
        }

        if (remainingMinutes == 0)
        {
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        string hourPart = hours == 1 ? "1 hour" : $"{hours} hours";
        string minutePart = remainingMinutes == 1 ? "1 minute" : $"{remainingMinutes} minutes";

        return $"{hourPart} {minutePart}";
    }

    // ── Format (Convert) tests ──────────────────────────────────────────

    [Theory]
    [InlineData(0, "0 minutes")]
    [InlineData(-5, "0 minutes")]
    [InlineData(1, "1 minute")]
    [InlineData(5, "5 minutes")]
    [InlineData(15, "15 minutes")]
    [InlineData(30, "30 minutes")]
    [InlineData(59, "59 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(120, "2 hours")]
    [InlineData(90, "1 hour 30 minutes")]
    [InlineData(75, "1 hour 15 minutes")]
    [InlineData(135, "2 hours 15 minutes")]
    [InlineData(1440, "24 hours")]
    public void FormatMinutes_ReturnsExpectedString(int minutes, string expected)
    {
        Assert.Equal(expected, FormatMinutes(minutes));
    }

    [Fact]
    public void FormatMinutes_61Minutes()
    {
        Assert.Equal("1 hour 1 minute", FormatMinutes(61));
    }

    // ── Parse (ConvertBack) tests ───────────────────────────────────────

    [Theory]
    [InlineData("1 minute", 1)]
    [InlineData("5 minutes", 5)]
    [InlineData("15 minutes", 15)]
    [InlineData("1 hour", 60)]
    [InlineData("2 hours", 120)]
    [InlineData("1 hour 30 minutes", 90)]
    [InlineData("2 hours 15 minutes", 135)]
    [InlineData("24 hours", 1440)]
    public void ParseTimeString_ReturnsExpectedMinutes(string input, int expected)
    {
        Assert.Equal(expected, ParseTimeString(input));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData(null, 0)]
    public void ParseTimeString_EmptyOrNull_ReturnsZero(string? input, int expected)
    {
        Assert.Equal(expected, ParseTimeString(input!));
    }

    [Fact]
    public void ParseTimeString_PlainNumber_ReturnsAsMinutes()
    {
        Assert.Equal(30, ParseTimeString("30"));
    }

    [Fact]
    public void ParseTimeString_SpacyInput_StillParses()
    {
        Assert.Equal(60, ParseTimeString("1  hour"));
        Assert.Equal(30, ParseTimeString("30  minutes"));
    }

    // ── Round-trip tests ────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(1440)]
    public void RoundTrip_FormatThenParse(int minutes)
    {
        string formatted = FormatMinutes(minutes);
        int parsed = ParseTimeString(formatted);
        Assert.Equal(minutes, parsed);
    }
}
