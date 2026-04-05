using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace NativeCal.Helpers
{
    public class DateTimeToStringConverter : IValueConverter
    {
        private const string DefaultFormat = "MMM dd, yyyy HH:mm";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dateTime)
            {
                string format = parameter as string ?? DefaultFormat;
                return dateTime.ToString(format, CultureInfo.CurrentCulture);
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                string format = parameter as string ?? DefaultFormat;
                return dateTimeOffset.ToString(format, CultureInfo.CurrentCulture);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                string format = parameter as string ?? DefaultFormat;

                if (DateTime.TryParseExact(str, format, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime result))
                {
                    return result;
                }

                if (DateTime.TryParse(str, CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
                {
                    return result;
                }
            }

            return DateTime.Now;
        }
    }
}
