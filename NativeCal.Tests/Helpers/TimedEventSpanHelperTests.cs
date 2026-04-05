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
    public void SpansMultipleDays_ReturnsTrueForOvernightTimedEvent()
    {
        bool spans = TimedEventSpanHelper.SpansMultipleDays(
            new DateTime(2026, 4, 15, 9, 15, 0),
            new DateTime(2026, 4, 16, 11, 0, 0));

        Assert.True(spans);
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
}
