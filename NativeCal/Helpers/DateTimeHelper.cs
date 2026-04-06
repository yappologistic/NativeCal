using System;
using System.Collections.Generic;
using System.Globalization;

namespace NativeCal.Helpers
{
    public static class DateTimeHelper
    {
        public static DateTime GetWeekStart(DateTime date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
        {
            int diff = ((int)date.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
            return date.Date.AddDays(-diff);
        }

        public static DateTime GetWeekEnd(DateTime date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
        {
            return GetWeekStart(date, firstDayOfWeek).AddDays(6);
        }

        public static DateTime GetMonthStart(DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        public static DateTime GetMonthEnd(DateTime date)
        {
            return new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
        }

        public static DateTime GetCalendarGridStart(DateTime monthDate, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
        {
            DateTime monthStart = GetMonthStart(monthDate);
            return GetWeekStart(monthStart, firstDayOfWeek);
        }

        public static DateTime GetCalendarGridEnd(DateTime monthDate, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
        {
            DateTime gridStart = GetCalendarGridStart(monthDate, firstDayOfWeek);
            // 6 rows x 7 days = 42 cells, so the last cell is 41 days after the first.
            return gridStart.AddDays(41);
        }

        /// <summary>
        /// Formats a single clock time using the active culture's short-time pattern.
        /// This keeps the UI aligned with 12/24-hour user preferences.
        /// </summary>
        public static string FormatTime(DateTime value, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            return value.ToString("t", culture);
        }

        public static string FormatTimeRange(DateTime start, DateTime end, bool isAllDay)
        {
            if (isAllDay)
            {
                return "All Day";
            }

            return $"{FormatTime(start)} - {FormatTime(end)}";
        }

        public static string GetRelativeDate(DateTime date)
        {
            DateTime today = DateTime.Today;
            DateTime targetDate = date.Date;
            int daysDiff = (targetDate - today).Days;
            string dayName = targetDate.ToString("dddd", CultureInfo.CurrentCulture);

            if (daysDiff == 0)
            {
                return "Today";
            }

            if (daysDiff == 1)
            {
                return "Tomorrow";
            }

            if (daysDiff == -1)
            {
                return "Yesterday";
            }

            // Within the next 7 days (but not today/tomorrow).
            if (daysDiff > 1 && daysDiff <= 7)
            {
                return dayName;
            }

            // Within the past 7 days (but not yesterday).
            if (daysDiff < -1 && daysDiff >= -7)
            {
                return $"Last {dayName}";
            }

            return targetDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture);
        }

        public static bool IsSameDay(DateTime a, DateTime b)
        {
            return a.Year == b.Year && a.Month == b.Month && a.Day == b.Day;
        }

        /// <summary>
        /// Returns localized abbreviated weekday headers starting from the configured
        /// first day of week.
        /// </summary>
        public static IReadOnlyList<string> GetDayOfWeekHeaders(DayOfWeek firstDayOfWeek, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var headers = new string[7];

            for (int i = 0; i < headers.Length; i++)
            {
                DayOfWeek day = (DayOfWeek)(((int)firstDayOfWeek + i) % 7);
                headers[i] = culture.DateTimeFormat.GetAbbreviatedDayName(day);
            }

            return headers;
        }

        public static List<string> GetHourLabels()
        {
            var labels = new List<string>(24);

            for (int hour = 0; hour < 24; hour++)
            {
                DateTime time = DateTime.Today.AddHours(hour);
                labels.Add(FormatTime(time));
            }

            return labels;
        }

        /// <summary>
        /// Picks a sensible default start time for new events.
        /// Date-only inputs use the current time for today and a 9:00 AM business-hour
        /// default for other days. Explicit times are preserved exactly.
        /// </summary>
        public static DateTime GetDefaultEventStart(DateTime? selectedDate, DateTime? now = null)
        {
            DateTime referenceNow = now ?? DateTime.Now;

            if (!selectedDate.HasValue)
            {
                return RoundUpToMinuteIncrement(referenceNow, 30);
            }

            DateTime requested = selectedDate.Value;
            if (requested.TimeOfDay != TimeSpan.Zero)
            {
                return requested;
            }

            return requested.Date == referenceNow.Date
                ? RoundUpToMinuteIncrement(referenceNow, 30)
                : requested.Date.AddHours(9);
        }

        /// <summary>
        /// Rounds a timestamp up to the next minute increment while preserving the date.
        /// Exact increment boundaries are kept as-is.
        /// </summary>
        public static DateTime RoundUpToMinuteIncrement(DateTime value, int minuteIncrement)
        {
            if (minuteIncrement <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minuteIncrement), "Minute increment must be positive.");
            }

            // Trim sub-minute precision before applying the increment so callers get
            // stable, display-friendly values such as 09:30 instead of 09:30:42.
            DateTime truncated = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
            int totalMinutes = truncated.Hour * 60 + truncated.Minute;
            int remainder = totalMinutes % minuteIncrement;
            bool hasSubMinutePrecision = value.Second != 0 || value.Millisecond != 0 || (value.Ticks % TimeSpan.TicksPerSecond) != 0;

            if (remainder == 0 && !hasSubMinutePrecision)
            {
                return truncated;
            }

            int minutesToAdd = remainder == 0 ? minuteIncrement : minuteIncrement - remainder;
            return truncated.AddMinutes(minutesToAdd);
        }
    }
}
