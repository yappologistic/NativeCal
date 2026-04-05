using System;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.Tests.Helpers;

public class CalendarEventMutationHelperTests
{
    [Fact]
    public void MoveEventToDate_PreservesDurationAndTimeOfDay()
    {
        var evt = new CalendarEvent
        {
            Title = "Meeting",
            StartTime = new DateTime(2026, 4, 5, 9, 30, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 30, 0),
            CalendarId = 1
        };

        var moved = CalendarEventMutationHelper.MoveEventToDate(evt, new DateTime(2026, 4, 8));

        Assert.Equal(new DateTime(2026, 4, 8, 9, 30, 0), moved.StartTime);
        Assert.Equal(new DateTime(2026, 4, 8, 10, 30, 0), moved.EndTime);
    }

    [Fact]
    public void ResizeTimedEvent_EnforcesMinimumDuration()
    {
        var evt = new CalendarEvent
        {
            Title = "Meeting",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        };

        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, new DateTime(2026, 4, 5, 9, 5, 0));

        Assert.Equal(new DateTime(2026, 4, 5, 9, 15, 0), resized.EndTime);
    }
}
