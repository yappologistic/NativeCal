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
            // 6 rows x 7 days = 42 cells, so the last cell is 41 days after the first
            return gridStart.AddDays(41);
        }

        public static string FormatTimeRange(DateTime start, DateTime end, bool isAllDay)
        {
            if (isAllDay)
            {
                return "All Day";
            }

            string startTime = start.ToString("h:mm tt", CultureInfo.InvariantCulture);
            string endTime = end.ToString("h:mm tt", CultureInfo.InvariantCulture);

            return $"{startTime} - {endTime}";
        }

        public static string GetRelativeDate(DateTime date)
        {
            DateTime today = DateTime.Today;
            DateTime targetDate = date.Date;

            int daysDiff = (targetDate - today).Days;

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

            // Within the next 7 days (but not today/tomorrow)
            if (daysDiff > 1 && daysDiff <= 7)
            {
                return targetDate.DayOfWeek.ToString();
            }

            // Within the past 7 days (but not yesterday)
            if (daysDiff < -1 && daysDiff >= -7)
            {
                return $"Last {targetDate.DayOfWeek}";
            }

            return targetDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture);
        }

        public static bool IsSameDay(DateTime a, DateTime b)
        {
            return a.Year == b.Year && a.Month == b.Month && a.Day == b.Day;
        }

        public static List<string> GetHourLabels()
        {
            var labels = new List<string>(24);

            for (int hour = 0; hour < 24; hour++)
            {
                DateTime time = DateTime.Today.AddHours(hour);
                labels.Add(time.ToString("h tt", CultureInfo.InvariantCulture));
            }

            return labels;
        }
    }
}
