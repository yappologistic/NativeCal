using System;
using System.Collections.Generic;
using System.Linq;
using NativeCal.Models;

namespace NativeCal.Helpers;

public sealed record HolidayCalendarDefinition(string Name, string CountryCode, string ColorHex, string CountryDisplayName);

public static class CalendarCatalogHelper
{
    public static readonly HolidayCalendarDefinition UnitedStatesHolidays = new(
        "US Holidays",
        "US",
        "#3B82F6",
        "United States");

    public static readonly HolidayCalendarDefinition CanadaHolidays = new(
        "Canada Holidays",
        "CA",
        "#E11D48",
        "Canada");

    public static IReadOnlyList<HolidayCalendarDefinition> HolidayCalendars { get; } =
        new[] { UnitedStatesHolidays, CanadaHolidays };

    public static bool TryGetHolidayCalendar(CalendarInfo calendar, out HolidayCalendarDefinition definition)
    {
        definition = HolidayCalendars.FirstOrDefault(d => string.Equals(d.Name, calendar.Name, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static bool TryGetHolidayCalendar(string calendarName, out HolidayCalendarDefinition definition)
    {
        definition = HolidayCalendars.FirstOrDefault(d => string.Equals(d.Name, calendarName, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static bool IsProtectedCalendar(CalendarInfo calendar)
    {
        return TryGetHolidayCalendar(calendar, out _);
    }

    public static IEnumerable<CalendarInfo> GetVisibleHolidayCalendars(IEnumerable<CalendarInfo> calendars)
    {
        return calendars.Where(c => c.IsVisible && TryGetHolidayCalendar(c, out _));
    }
}
