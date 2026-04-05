using System;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Data;

namespace NativeCal.Helpers
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int minutes;

            if (value is int intValue)
            {
                minutes = intValue;
            }
            else if (value is long longValue)
            {
                minutes = (int)longValue;
            }
            else if (value is double doubleValue)
            {
                minutes = (int)doubleValue;
            }
            else
            {
                return string.Empty;
            }

            if (minutes <= 0)
            {
                return "0 minutes";
            }

            int hours = minutes / 60;
            int remainingMinutes = minutes % 60;

            if (hours == 0)
            {
                return remainingMinutes == 1 ? "1 minute" : $"{remainingMinutes} minutes";
            }

            if (remainingMinutes == 0)
            {
                return hours == 1 ? "1 hour" : $"{hours} hours";
            }

            string hourPart = hours == 1 ? "1 hour" : $"{hours} hours";
            string minutePart = remainingMinutes == 1 ? "1 minute" : $"{remainingMinutes} minutes";

            return $"{hourPart} {minutePart}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                int totalMinutes = 0;

                // Match hours
                var hourMatch = Regex.Match(str, @"(\d+)\s*hour");
                if (hourMatch.Success && int.TryParse(hourMatch.Groups[1].Value, out int hours))
                {
                    totalMinutes += hours * 60;
                }

                // Match minutes
                var minuteMatch = Regex.Match(str, @"(\d+)\s*minute");
                if (minuteMatch.Success && int.TryParse(minuteMatch.Groups[1].Value, out int minutes))
                {
                    totalMinutes += minutes;
                }

                // If no match, try parsing as a plain number
                if (totalMinutes == 0 && int.TryParse(str.Trim(), out int plainMinutes))
                {
                    totalMinutes = plainMinutes;
                }

                return totalMinutes;
            }

            return 0;
        }
    }
}
