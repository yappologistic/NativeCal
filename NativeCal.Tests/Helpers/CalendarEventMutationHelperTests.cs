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

    // ════════════════════════════════════════════════════════════════════
    // Bug: DayView resize of multi-day events collapses them to one day.
    //
    // The DayView canvas can only produce a DateTime on the currently
    // viewed day. When a multi-day event (e.g. Apr 5 22:00 → Apr 6 03:00)
    // is resized in the Apr 5 DayView, the proposed end time is always
    // on Apr 5, which collapses the event from multi-day to single-day.
    //
    // The fix must detect when the proposed end falls on the start day of
    // a multi-day event and the resize handle is at the bottom of the
    // canvas (user dragging down = extending), and keep the original end
    // date in that case.
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simulates DayView resize: multi-day event, user drags handle DOWN
    /// to 23:45 on the viewed day (Apr 5). The canvas produces Apr 5 23:45.
    /// Since the event's end is on Apr 6, the resolve maps the proposed
    /// time-of-day (23:45) onto the original end date (Apr 6) so the event
    /// stays multi-day instead of collapsing.
    /// </summary>
    [Fact]
    public void ResizeTimedEvent_DayViewMultiDay_DragDown_MustNotCollapse()
    {
        var evt = new CalendarEvent
        {
            Title = "Overnight",
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 3, 0, 0),
            CalendarId = 1
        };

        // DayView canvas on Apr 5 produces Apr 5 23:45.
        DateTime proposedFromDayView = new DateTime(2026, 4, 5, 23, 45, 0);

        // Resolve maps 23:45 onto Apr 6 (the original end date).
        DateTime resolvedEnd = TimedEventSpanHelper.ResolveResizeEndForDayView(
            evt.StartTime, evt.EndTime, proposedFromDayView);
        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, resolvedEnd);

        // Event must still end on Apr 6, not collapse to Apr 5
        Assert.Equal(new DateTime(2026, 4, 6), resized.EndTime.Date);
        Assert.Equal(new DateTime(2026, 4, 6, 23, 45, 0), resized.EndTime);
    }

    /// <summary>
    /// DayView resize of multi-day event: user drags handle UP to 22:15 on
    /// the start day. The resolve still maps this onto the end date because
    /// the DayView canvas cannot produce times on other days. The event
    /// stays multi-day but with a shorter end-time on its end date.
    /// </summary>
    [Fact]
    public void ResizeTimedEvent_DayViewMultiDay_DragUp_StaysMultiDay()
    {
        var evt = new CalendarEvent
        {
            Title = "Overnight",
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 3, 0, 0),
            CalendarId = 1
        };

        // User drags to 22:15 on the viewed day (Apr 5).
        // Resolve maps this onto Apr 6 → end = Apr 6 22:15.
        DateTime proposedFromDayView = new DateTime(2026, 4, 5, 22, 15, 0);

        DateTime resolvedEnd = TimedEventSpanHelper.ResolveResizeEndForDayView(
            evt.StartTime, evt.EndTime, proposedFromDayView);
        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, resolvedEnd);

        // Still on Apr 6 — multi-day preserved
        Assert.Equal(new DateTime(2026, 4, 6, 22, 15, 0), resized.EndTime);
    }

    /// <summary>
    /// Simulates DayView resize: multi-day event viewed from the END date.
    /// Event Apr 5 22:00 → Apr 6 03:00, user views Apr 6 and drags handle
    /// down to 06:00. This should extend the event to end at Apr 6 06:00.
    /// </summary>
    [Fact]
    public void ResizeTimedEvent_DayViewMultiDay_ViewedFromEndDate_Extend()
    {
        var evt = new CalendarEvent
        {
            Title = "Overnight",
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 3, 0, 0),
            CalendarId = 1
        };

        // User views Apr 6 and drags the resize handle down to 06:00.
        DateTime proposedFromDayView = new DateTime(2026, 4, 6, 6, 0, 0);

        DateTime resolvedEnd = TimedEventSpanHelper.ResolveResizeEndForDayView(
            evt.StartTime, evt.EndTime, proposedFromDayView);
        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, resolvedEnd);

        // End should be on Apr 6 at 06:00 — straightforward extend on the end date
        Assert.Equal(new DateTime(2026, 4, 6, 6, 0, 0), resized.EndTime);
    }

    /// <summary>
    /// Single-day events should be unaffected by the new resolve logic.
    /// </summary>
    [Fact]
    public void ResizeTimedEvent_DayViewSingleDay_Unaffected()
    {
        var evt = new CalendarEvent
        {
            Title = "Meeting",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        };

        DateTime proposedFromDayView = new DateTime(2026, 4, 5, 11, 30, 0);

        DateTime resolvedEnd = TimedEventSpanHelper.ResolveResizeEndForDayView(
            evt.StartTime, evt.EndTime, proposedFromDayView);
        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, resolvedEnd);

        Assert.Equal(new DateTime(2026, 4, 5, 11, 30, 0), resized.EndTime);
    }
}
