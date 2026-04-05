using System.Collections.Generic;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.Tests.Helpers;

public class CalendarDisplayHelperTests
{
    [Fact]
    public void ResolveEventColorHex_PrefersExplicitEventColor()
    {
        var calendars = new Dictionary<int, CalendarInfo>
        {
            [2] = new CalendarInfo { Id = 2, Name = "Work", ColorHex = "#E74C3C" }
        };

        var color = CalendarDisplayHelper.ResolveEventColorHex(2, "#123456", calendars);

        Assert.Equal("#123456", color);
    }

    [Fact]
    public void ResolveEventColorHex_FallsBackToCalendarColorWhenEventHasNoOverride()
    {
        var calendars = new Dictionary<int, CalendarInfo>
        {
            [2] = new CalendarInfo { Id = 2, Name = "Work", ColorHex = "#E74C3C" }
        };

        var color = CalendarDisplayHelper.ResolveEventColorHex(2, null, calendars);

        Assert.Equal("#E74C3C", color);
    }

    [Fact]
    public void ResolveCalendarName_ReturnsCalendarNameWhenFound()
    {
        var calendars = new Dictionary<int, CalendarInfo>
        {
            [2] = new CalendarInfo { Id = 2, Name = "Work", ColorHex = "#E74C3C" }
        };

        var name = CalendarDisplayHelper.ResolveCalendarName(2, calendars);

        Assert.Equal("Work", name);
    }
}
