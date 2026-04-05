using System;
using System.Globalization;

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

    public static DateTime ResolveResizeTargetDateTime(DateTime startTime, DateTime originalEndTime, DateTime proposedDateTime)
    {
        if (SpansMultipleDays(startTime, originalEndTime))
        {
            return proposedDateTime;
        }

        return originalEndTime.Date.Add(proposedDateTime.TimeOfDay);
    }

    public static DateTime ResolveResizeTargetTimeOnOriginalEndDate(DateTime originalEndTime, DateTime proposedDateTime)
    {
        return originalEndTime.Date.Add(proposedDateTime.TimeOfDay);
    }

    public static string FormatSpanTimeRange(DateTime startTime, DateTime endTime, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        if (!SpansMultipleDays(startTime, endTime))
        {
            return DateTimeHelper.FormatTimeRange(startTime, endTime, isAllDay: false);
        }

        return $"{startTime.ToString("ddd h:mm tt", culture)} → {endTime.ToString("ddd h:mm tt", culture)}";
    }
}
