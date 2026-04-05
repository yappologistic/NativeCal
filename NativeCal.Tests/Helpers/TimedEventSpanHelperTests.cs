using System;
using NativeCal.Helpers;

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
}
