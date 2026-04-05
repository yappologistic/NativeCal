using System.Linq;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.Tests.Helpers;

public class CalendarCatalogHelperTests
{
    [Fact]
    public void TryGetHolidayCalendar_MatchesKnownCalendarNameCaseInsensitively()
    {
        var calendar = new CalendarInfo { Name = "canada holidays", IsVisible = true };

        var found = CalendarCatalogHelper.TryGetHolidayCalendar(calendar, out var definition);

        Assert.True(found);
        Assert.Equal("Canada Holidays", definition.Name);
        Assert.Equal("CA", definition.CountryCode);
        Assert.Equal("Canada", definition.CountryDisplayName);
    }

    [Fact]
    public void TryGetHolidayCalendar_ReturnsFalseForUnknownCalendarName()
    {
        Assert.False(CalendarCatalogHelper.TryGetHolidayCalendar("Birthdays", out _));
    }

    [Fact]
    public void IsProtectedCalendar_ReturnsTrueOnlyForKnownHolidayCalendars()
    {
        Assert.True(CalendarCatalogHelper.IsProtectedCalendar(new CalendarInfo { Name = "US Holidays" }));
        Assert.False(CalendarCatalogHelper.IsProtectedCalendar(new CalendarInfo { Name = "Personal" }));
    }

    [Fact]
    public void GetVisibleHolidayCalendars_FiltersHiddenAndNonHolidayCalendars()
    {
        var calendars = new[]
        {
            new CalendarInfo { Id = 1, Name = "Personal", IsVisible = true },
            new CalendarInfo { Id = 2, Name = "US Holidays", IsVisible = true },
            new CalendarInfo { Id = 3, Name = "Canada Holidays", IsVisible = false }
        };

        var visibleHolidayCalendars = CalendarCatalogHelper.GetVisibleHolidayCalendars(calendars).ToList();

        var holidayCalendar = Assert.Single(visibleHolidayCalendars);
        Assert.Equal(2, holidayCalendar.Id);
        Assert.Equal("US Holidays", holidayCalendar.Name);
    }
}
