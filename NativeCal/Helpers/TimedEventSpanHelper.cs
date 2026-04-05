using System;

namespace NativeCal.Helpers;

public static class TimedEventSpanHelper
{
    public static DateTime GetInclusiveEndDate(DateTime startTime, DateTime endTime)
    {
        DateTime inclusiveEndDate = endTime.Date;

        if (endTime.TimeOfDay == TimeSpan.Zero && endTime > startTime)
        {
            inclusiveEndDate = inclusiveEndDate.AddDays(-1);
        }

        return inclusiveEndDate;
    }

    public static bool SpansMultipleDays(DateTime startTime, DateTime endTime)
    {
        return GetInclusiveEndDate(startTime, endTime) > startTime.Date;
    }

    public static DateTime GetVisibleSegmentStart(DateTime eventStart, DateTime visibleStart)
    {
        return eventStart < visibleStart ? visibleStart : eventStart;
    }

    public static DateTime GetVisibleSegmentEnd(DateTime eventEnd, DateTime visibleEnd)
    {
        return eventEnd > visibleEnd ? visibleEnd : eventEnd;
    }
}
