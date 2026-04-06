using System;
using NativeCal.Models;

namespace NativeCal.Helpers;

/// <summary>
/// Pure helper functions for moving, resizing, and rounding calendar events.
/// Used by drag-and-drop and resize interactions in Day, Week, and Month views.
/// All methods return a new <see cref="CalendarEvent"/> clone — they never mutate the input.
/// </summary>
public static class CalendarEventMutationHelper
{
    /// <summary>Default snap increment in minutes (events snap to 15-minute boundaries).</summary>
    public const int DefaultIncrementMinutes = 15;

    /// <summary>Minimum allowed event duration in minutes.</summary>
    public const int MinimumDurationMinutes = 15;

    /// <summary>
    /// Rounds a <see cref="DateTime"/> to the nearest <paramref name="incrementMinutes"/> boundary.
    /// Clamps the result so it stays within the same day (00:00 – 23:45 for 15-min increments).
    /// </summary>
    public static DateTime RoundToIncrement(DateTime value, int incrementMinutes = DefaultIncrementMinutes)
    {
        int totalMinutes = value.Hour * 60 + value.Minute;
        int roundedMinutes = (int)Math.Round(totalMinutes / (double)incrementMinutes) * incrementMinutes;
        roundedMinutes = Math.Clamp(roundedMinutes, 0, 24 * 60 - incrementMinutes);
        return value.Date.AddMinutes(roundedMinutes);
    }

    /// <summary>
    /// Moves a timed event to a new start time (snapped to the increment grid),
    /// preserving the original duration. Enforces <see cref="MinimumDurationMinutes"/>.
    /// </summary>
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

    /// <summary>
    /// Resizes an event by changing its end time (snapped to the increment grid).
    /// The start time is unchanged. Enforces <see cref="MinimumDurationMinutes"/>.
    /// </summary>
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

    /// <summary>
    /// Moves an event to a different date by shifting both start and end
    /// by the same day-delta. Time-of-day and duration are preserved.
    /// Used for drag-and-drop between day cells in the month view.
    /// </summary>
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
