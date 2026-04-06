using System;
using System.Collections.Generic;
using System.Linq;
using NativeCal.Models;

namespace NativeCal.Helpers;

/// <summary>
/// Defines a built-in holiday calendar backed by the Nager.Date public API.
/// </summary>
/// <param name="Name">Display name stored in the Calendars table (e.g. "US Holidays").</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 code for the API (e.g. "US", "CA").</param>
/// <param name="ColorHex">Default hex color for this calendar's events.</param>
/// <param name="CountryDisplayName">Human-readable country name shown in holiday descriptions.</param>
public sealed record HolidayCalendarDefinition(string Name, string CountryCode, string ColorHex, string CountryDisplayName);

/// <summary>
/// Central registry of built-in holiday calendars and utility methods to identify
/// and filter them. Holiday calendars are "protected" — they cannot be renamed,
/// recolored, or deleted by the user.
/// </summary>
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

    /// <summary>
    /// Tries to match a <see cref="CalendarInfo"/> to a known holiday definition by name.
    /// Returns true if a match is found and populates <paramref name="definition"/>.
    /// </summary>
    public static bool TryGetHolidayCalendar(CalendarInfo calendar, out HolidayCalendarDefinition definition)
    {
        definition = HolidayCalendars.FirstOrDefault(d => string.Equals(d.Name, calendar.Name, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    /// <summary>Overload that matches by calendar name string.</summary>
    public static bool TryGetHolidayCalendar(string calendarName, out HolidayCalendarDefinition definition)
    {
        definition = HolidayCalendars.FirstOrDefault(d => string.Equals(d.Name, calendarName, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    /// <summary>
    /// Returns true if the calendar is a built-in holiday calendar that cannot be
    /// edited or deleted by the user.
    /// </summary>
    public static bool IsProtectedCalendar(CalendarInfo calendar)
    {
        return TryGetHolidayCalendar(calendar, out _);
    }

    /// <summary>
    /// Filters the given calendars to only those that are visible AND are
    /// recognized holiday calendars (for holiday API fetching).
    /// </summary>
    public static IEnumerable<CalendarInfo> GetVisibleHolidayCalendars(IEnumerable<CalendarInfo> calendars)
    {
        return calendars.Where(c => c.IsVisible && TryGetHolidayCalendar(c, out _));
    }
}
