using System.Collections.Generic;
using NativeCal.Models;

namespace NativeCal.Helpers;

/// <summary>
/// Resolves display properties (color, name) for events based on their
/// calendar assignment and optional per-event overrides.
/// </summary>
public static class CalendarDisplayHelper
{
    /// <summary>
    /// Resolves the hex color for an event. Priority:
    ///   1. Per-event color override (if non-empty).
    ///   2. Parent calendar's color.
    ///   3. Default palette color (#4A90D9).
    /// </summary>
    public static string ResolveEventColorHex(int calendarId, string? eventColorHex, IReadOnlyDictionary<int, CalendarInfo> calendars)
    {
        if (!string.IsNullOrWhiteSpace(eventColorHex))
            return eventColorHex;

        if (calendars.TryGetValue(calendarId, out var calendar) && !string.IsNullOrWhiteSpace(calendar.ColorHex))
            return calendar.ColorHex;

        return ColorHelper.CalendarColors[0];
    }

    /// <summary>
    /// Resolves the display name for the calendar an event belongs to.
    /// Falls back to "Calendar" if the calendar ID is not found.
    /// </summary>
    public static string ResolveCalendarName(int calendarId, IReadOnlyDictionary<int, CalendarInfo> calendars)
    {
        if (calendars.TryGetValue(calendarId, out var calendar) && !string.IsNullOrWhiteSpace(calendar.Name))
            return calendar.Name;

        return "Calendar";
    }
}
