using System.Collections.Generic;
using NativeCal.Models;

namespace NativeCal.Helpers;

public static class CalendarDisplayHelper
{
    public static string ResolveEventColorHex(int calendarId, string? eventColorHex, IReadOnlyDictionary<int, CalendarInfo> calendars)
    {
        if (!string.IsNullOrWhiteSpace(eventColorHex))
            return eventColorHex;

        if (calendars.TryGetValue(calendarId, out var calendar) && !string.IsNullOrWhiteSpace(calendar.ColorHex))
            return calendar.ColorHex;

        return ColorHelper.CalendarColors[0];
    }

    public static string ResolveCalendarName(int calendarId, IReadOnlyDictionary<int, CalendarInfo> calendars)
    {
        if (calendars.TryGetValue(calendarId, out var calendar) && !string.IsNullOrWhiteSpace(calendar.Name))
            return calendar.Name;

        return "Calendar";
    }
}
