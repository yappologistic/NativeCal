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
    public void MoveEventToDate_PreservesOvernightSpan()
    {
        var evt = new CalendarEvent
        {
            Title = "Overnight",
            StartTime = new DateTime(2026, 4, 5, 23, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 1, 0, 0),
            CalendarId = 1
        };

        var moved = CalendarEventMutationHelper.MoveEventToDate(evt, new DateTime(2026, 4, 8));

        Assert.Equal(new DateTime(2026, 4, 8, 23, 0, 0), moved.StartTime);
        Assert.Equal(new DateTime(2026, 4, 9, 1, 0, 0), moved.EndTime);
    }

    [Fact]
    public void MoveTimedEvent_RoundsToNearestIncrementAndPreservesDuration()
    {
        var evt = new CalendarEvent
        {
            Title = "Workshop",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 30, 0),
            CalendarId = 1
        };

        var moved = CalendarEventMutationHelper.MoveTimedEvent(evt, new DateTime(2026, 4, 5, 11, 8, 0));

        Assert.Equal(new DateTime(2026, 4, 5, 11, 15, 0), moved.StartTime);
        Assert.Equal(new DateTime(2026, 4, 5, 12, 45, 0), moved.EndTime);
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

    [Fact]
    public void ResizeTimedEvent_CanExtendAcrossMidnight()
    {
        var evt = new CalendarEvent
        {
            Title = "Late work",
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 23, 0, 0),
            CalendarId = 1
        };

        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, new DateTime(2026, 4, 6, 0, 53, 0));

        Assert.Equal(new DateTime(2026, 4, 6, 1, 0, 0), resized.EndTime);
    }

    [Fact]
    public void ResizeTimedEvent_CanShrinkMultiDayEventToAnEarlierDay()
    {
        var evt = new CalendarEvent
        {
            Title = "Conference",
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 7, 2, 0, 0),
            CalendarId = 1
        };

        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, new DateTime(2026, 4, 6, 23, 40, 0));

        Assert.Equal(new DateTime(2026, 4, 6, 23, 45, 0), resized.EndTime);
    }

    [Fact]
    public void ResizeTimedEvent_CanExtendMultiDayEventToALaterDay()
    {
        var evt = new CalendarEvent
        {
            Title = "Conference",
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 2, 0, 0),
            CalendarId = 1
        };

        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, new DateTime(2026, 4, 8, 3, 40, 0));

        Assert.Equal(new DateTime(2026, 4, 8, 3, 45, 0), resized.EndTime);
    }
}
