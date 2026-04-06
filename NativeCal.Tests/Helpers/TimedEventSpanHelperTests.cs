using System;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.Tests.Helpers;

public class TimedEventSpanHelperTests
{
    [Fact]
    public void GetInclusiveEndDate_TreatsMidnightEndAsPreviousDay()
    {
        DateTime start = new(2026, 4, 15, 9, 0, 0);
        DateTime end = new(2026, 4, 16, 0, 0, 0);

        DateTime inclusiveEnd = TimedEventSpanHelper.GetInclusiveEndDate(start, end);

        Assert.Equal(new DateTime(2026, 4, 15), inclusiveEnd);
    }

    [Fact]
    public void GetInclusiveEndDate_LeavesNonMidnightEndOnSameDay()
    {
        DateTime start = new(2026, 4, 15, 9, 0, 0);
        DateTime end = new(2026, 4, 16, 0, 1, 0);

        DateTime inclusiveEnd = TimedEventSpanHelper.GetInclusiveEndDate(start, end);

        Assert.Equal(new DateTime(2026, 4, 16), inclusiveEnd);
    }

    [Fact]
    public void SpansMultipleDays_ReturnsTrueForOvernightTimedEvent()
    {
        bool spans = TimedEventSpanHelper.SpansMultipleDays(
            new DateTime(2026, 4, 15, 9, 15, 0),
            new DateTime(2026, 4, 16, 11, 0, 0));

        Assert.True(spans);
    }

    [Fact]
    public void SpansMultipleDays_ReturnsFalseForSameDayEvent()
    {
        bool spans = TimedEventSpanHelper.SpansMultipleDays(
            new DateTime(2026, 4, 15, 9, 15, 0),
            new DateTime(2026, 4, 15, 11, 0, 0));

        Assert.False(spans);
    }

    [Fact]
    public void GetVisibleSegmentBounds_ClipsToVisibleRange()
    {
        DateTime visibleStart = new(2026, 4, 16, 0, 0, 0);
        DateTime visibleEnd = visibleStart.AddDays(1);

        DateTime segmentStart = TimedEventSpanHelper.GetVisibleSegmentStart(
            new DateTime(2026, 4, 15, 22, 0, 0),
            visibleStart);
        DateTime segmentEnd = TimedEventSpanHelper.GetVisibleSegmentEnd(
            new DateTime(2026, 4, 16, 3, 0, 0),
            visibleEnd);

        Assert.Equal(visibleStart, segmentStart);
        Assert.Equal(new DateTime(2026, 4, 16, 3, 0, 0), segmentEnd);
    }

    [Fact]
    public void GetVisibleSegmentBounds_ReturnsOriginalBoundsWhenAlreadyVisible()
    {
        DateTime visibleStart = new(2026, 4, 16, 0, 0, 0);
        DateTime visibleEnd = visibleStart.AddDays(1);
        DateTime eventStart = new(2026, 4, 16, 9, 0, 0);
        DateTime eventEnd = new(2026, 4, 16, 10, 30, 0);

        DateTime segmentStart = TimedEventSpanHelper.GetVisibleSegmentStart(eventStart, visibleStart);
        DateTime segmentEnd = TimedEventSpanHelper.GetVisibleSegmentEnd(eventEnd, visibleEnd);

        Assert.Equal(eventStart, segmentStart);
        Assert.Equal(eventEnd, segmentEnd);
    }

    [Fact]
    public void ResolveResizeTargetDateTime_PreservesOriginalEndDateForSameDayEvents()
    {
        DateTime resolved = TimedEventSpanHelper.ResolveResizeTargetDateTime(
            new DateTime(2026, 4, 15, 9, 0, 0),
            new DateTime(2026, 4, 15, 10, 0, 0),
            new DateTime(2026, 4, 18, 11, 30, 0));

        Assert.Equal(new DateTime(2026, 4, 15, 11, 30, 0), resolved);
    }

    [Fact]
    public void ResolveResizeTargetDateTime_UsesDraggedDateForMultiDayEvents()
    {
        DateTime resolved = TimedEventSpanHelper.ResolveResizeTargetDateTime(
            new DateTime(2026, 4, 15, 22, 0, 0),
            new DateTime(2026, 4, 16, 1, 0, 0),
            new DateTime(2026, 4, 17, 3, 30, 0));

        Assert.Equal(new DateTime(2026, 4, 17, 3, 30, 0), resolved);
    }

    [Fact]
    public void FormatSpanTimeRange_IncludesBothDaysAndTimesForMultiDayEvents()
    {
        string text = TimedEventSpanHelper.FormatSpanTimeRange(
            new DateTime(2026, 4, 15, 22, 0, 0),
            new DateTime(2026, 4, 17, 3, 30, 0),
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("Wed 10:00 PM → Fri 3:30 AM", text);
    }

    [Fact]
    public void FormatSpanTimeRange_FallsBackToStandardTimeRangeForSameDayEvents()
    {
        string text = TimedEventSpanHelper.FormatSpanTimeRange(
            new DateTime(2026, 4, 15, 9, 0, 0),
            new DateTime(2026, 4, 15, 10, 30, 0),
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("9:00 AM - 10:30 AM", text);
    }

    /// <summary>
    /// Regression: The DayView canvas can only produce DateTimes on the viewed
    /// day. For a multi-day event, the new ResolveResizeEndForDayView helper
    /// maps the proposed time onto the original end date.  This test verifies
    /// the full DayView resize path produces a valid multi-day result.
    /// </summary>
    [Fact]
    public void DayViewResize_MultiDayEvent_FullPathProducesValidResult()
    {
        // Multi-day event: Apr 5 22:00 → Apr 6 03:00
        var evt = new CalendarEvent
        {
            Title = "Overnight",
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 3, 0, 0),
            CalendarId = 1
        };

        // User views Apr 5 and drags the resize handle to 23:00 on the canvas.
        DateTime proposedFromCanvas = new DateTime(2026, 4, 5, 23, 0, 0);

        // The resolve helper maps 23:00 onto the original end date (Apr 6).
        DateTime resolved = TimedEventSpanHelper.ResolveResizeEndForDayView(
            evt.StartTime, evt.EndTime, proposedFromCanvas);
        CalendarEvent resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, resolved);

        // Event must NOT collapse to single-day
        Assert.True(resized.EndTime > resized.StartTime, "EndTime must be after StartTime");
        Assert.Equal(new DateTime(2026, 4, 6, 23, 0, 0), resized.EndTime);
    }

    /// <summary>
    /// Documents that ResolveResizeTargetTimeOnOriginalEndDate produces wrong
    /// results for multi-day events in DayView context. The DayViewPage no
    /// longer calls this method for resize; it passes the canvas DateTime
    /// directly to ResizeTimedEvent instead.
    /// </summary>
    [Fact]
    public void ResolveResizeTargetTimeOnOriginalEndDate_KnownLimitation_ForMultiDayEvents()
    {
        // OriginalEnd is on Apr 6. Canvas pointer gives Apr 5 23:00.
        // The helper pastes 23:00 onto Apr 6 → Apr 6 23:00.
        // For DayView this is WRONG (grows the event), but the helper
        // is still used correctly by other code paths where the proposed
        // date IS on the original end date. Kept for documentation.
        DateTime resolved = TimedEventSpanHelper.ResolveResizeTargetTimeOnOriginalEndDate(
            new DateTime(2026, 4, 6, 3, 0, 0),  // originalEnd
            new DateTime(2026, 4, 5, 23, 0, 0)); // proposed from canvas

        Assert.Equal(new DateTime(2026, 4, 6, 23, 0, 0), resolved);
    }

    // ── ResolveResizeEndForDayView tests ─────────────────────────────────

    [Fact]
    public void ResolveResizeEndForDayView_SingleDayEvent_PassesThrough()
    {
        DateTime result = TimedEventSpanHelper.ResolveResizeEndForDayView(
            new DateTime(2026, 4, 5, 9, 0, 0),   // start
            new DateTime(2026, 4, 5, 10, 0, 0),  // end (same day)
            new DateTime(2026, 4, 5, 11, 30, 0)); // proposed

        Assert.Equal(new DateTime(2026, 4, 5, 11, 30, 0), result);
    }

    [Fact]
    public void ResolveResizeEndForDayView_MultiDay_ViewedFromEndDate_PassesThrough()
    {
        // User views Apr 6 (the end date) and drags to 06:00
        DateTime result = TimedEventSpanHelper.ResolveResizeEndForDayView(
            new DateTime(2026, 4, 5, 22, 0, 0),  // start
            new DateTime(2026, 4, 6, 3, 0, 0),   // end
            new DateTime(2026, 4, 6, 6, 0, 0));   // proposed on end date

        Assert.Equal(new DateTime(2026, 4, 6, 6, 0, 0), result);
    }

    [Fact]
    public void ResolveResizeEndForDayView_MultiDay_ViewedFromStartDate_MapsOntoEndDate()
    {
        // User views Apr 5 (the start date) and drags to 23:30.
        // Canvas gives Apr 5 23:30 but event ends on Apr 6.
        // Must map 23:30 onto Apr 6 to avoid collapsing.
        DateTime result = TimedEventSpanHelper.ResolveResizeEndForDayView(
            new DateTime(2026, 4, 5, 22, 0, 0),  // start
            new DateTime(2026, 4, 6, 3, 0, 0),   // end
            new DateTime(2026, 4, 5, 23, 30, 0)); // proposed on start date

        Assert.Equal(new DateTime(2026, 4, 6, 23, 30, 0), result);
    }

    [Fact]
    public void ResolveResizeEndForDayView_ThreeDayEvent_ViewedFromMiddleDate_MapsOntoEndDate()
    {
        // Event: Apr 5 22:00 → Apr 7 10:00. User views Apr 6 (middle day).
        // Canvas gives Apr 6 15:00. Must map onto Apr 7 (the end date).
        DateTime result = TimedEventSpanHelper.ResolveResizeEndForDayView(
            new DateTime(2026, 4, 5, 22, 0, 0),  // start
            new DateTime(2026, 4, 7, 10, 0, 0),  // end
            new DateTime(2026, 4, 6, 15, 0, 0)); // proposed on middle date

        Assert.Equal(new DateTime(2026, 4, 7, 15, 0, 0), result);
    }
}
