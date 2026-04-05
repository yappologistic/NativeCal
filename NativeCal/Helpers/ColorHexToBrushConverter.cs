using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace NativeCal.Helpers
{
    public class ColorHexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    return ColorHelper.ToBrush(hex);
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Default to first calendar color
            return ColorHelper.ToBrush(ColorHelper.CalendarColors[0]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is SolidColorBrush brush)
            {
                return ColorHelper.ToHex(brush.Color);
            }

            return ColorHelper.CalendarColors[0];
        }
    }
}
