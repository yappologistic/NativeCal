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

    /// <summary>
    /// Resolves a resize end time for the DayView, where the canvas can only
    /// produce DateTimes on the currently viewed day.
    ///
    /// For single-day events this is a no-op — the proposed time is returned.
    ///
    /// For multi-day events, the canvas always produces a time on the viewed
    /// day.  Two sub-cases:
    ///
    ///   1. Viewed day == original end date (or later) — the resize handle
    ///      is on the end segment of the event.  The proposed time is
    ///      already on the correct date → pass through.
    ///
    ///   2. Viewed day  &lt; original end date — the user is looking at the
    ///      start-date (or a middle day).  The event's visible segment
    ///      here runs from the start time (or 00:00) to midnight.  The
    ///      resize handle sits at the bottom edge.  Any drag changes the
    ///      end to a point on the viewed day.  Because the DayView canvas
    ///      cannot represent times on other days, the only meaningful
    ///      resize is "shrink to this day" — map the time onto the
    ///      original end date so the event stays multi-day but with a
    ///      new time-of-day on its end date.
    /// </summary>
    public static DateTime ResolveResizeEndForDayView(
        DateTime eventStart, DateTime originalEnd, DateTime proposedFromCanvas)
    {
        // Single-day event — canvas date is correct, pass through.
        if (!SpansMultipleDays(eventStart, originalEnd))
            return proposedFromCanvas;

        // If the proposed time is already on or after the original end date,
        // the user is viewing the end-date day — pass through.
        if (proposedFromCanvas.Date >= originalEnd.Date)
            return proposedFromCanvas;

        // The proposed time is on an earlier day than the original end.
        // Map the proposed time-of-day onto the original end date so the
        // event stays multi-day.  This lets the user adjust what time the
        // event ends on its final day without collapsing the span.
        return originalEnd.Date.Add(proposedFromCanvas.TimeOfDay);
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
