using System;
using NativeCal.Models;

namespace NativeCal.Helpers;

public static class CalendarEventMutationHelper
{
    public const int DefaultIncrementMinutes = 15;
    public const int MinimumDurationMinutes = 15;

    public static DateTime RoundToIncrement(DateTime value, int incrementMinutes = DefaultIncrementMinutes)
    {
        int totalMinutes = value.Hour * 60 + value.Minute;
        int roundedMinutes = (int)Math.Round(totalMinutes / (double)incrementMinutes) * incrementMinutes;
        roundedMinutes = Math.Clamp(roundedMinutes, 0, 24 * 60 - incrementMinutes);
        return value.Date.AddMinutes(roundedMinutes);
    }

    public static CalendarEvent MoveTimedEvent(CalendarEvent evt, DateTime newStart, int incrementMinutes = DefaultIncrementMinutes)
    {
        var updated = evt.Clone();
        var roundedStart = RoundToIncrement(newStart, incrementMinutes);
        TimeSpan duration = evt.EndTime - evt.StartTime;
        if (duration.TotalMinutes < MinimumDurationMinutes)
            duration = TimeSpan.FromMinutes(MinimumDurationMinutes);

        updated.StartTime = roundedStart;
        updated.EndTime = roundedStart.Add(duration);
        return updated;
    }

    public static CalendarEvent ResizeTimedEvent(CalendarEvent evt, DateTime newEnd, int incrementMinutes = DefaultIncrementMinutes)
    {
        var updated = evt.Clone();
        var roundedEnd = RoundToIncrement(newEnd, incrementMinutes);
        DateTime minimumEnd = evt.StartTime.AddMinutes(MinimumDurationMinutes);
        if (roundedEnd < minimumEnd)
            roundedEnd = minimumEnd;

        updated.EndTime = roundedEnd;
        return updated;
    }

    public static CalendarEvent MoveEventToDate(CalendarEvent evt, DateTime targetDate)
    {
        var updated = evt.Clone();
        DateTime targetDay = targetDate.Date;
        DateTime sourceDay = evt.StartTime.Date;
        int dayDelta = (targetDay - sourceDay).Days;

        updated.StartTime = evt.StartTime.AddDays(dayDelta);
        updated.EndTime = evt.EndTime.AddDays(dayDelta);
        return updated;
    }
}
