using System;
using Microsoft.UI.Xaml.Media;

namespace NativeCal.Helpers
{
    public static class ColorHelper
    {
        public static readonly string[] CalendarColors =
        {
            "#4A90D9",
            "#E74C3C",
            "#27AE60",
            "#F39C12",
            "#9B59B6",
            "#1ABC9C",
            "#E67E22",
            "#3498DB",
            "#E91E63",
            "#00BCD4"
        };

        public static Windows.UI.Color FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                throw new ArgumentException("Hex color string cannot be null or empty.", nameof(hex));
            }

            hex = hex.TrimStart('#');

            byte a = 255;
            byte r, g, b;

            switch (hex.Length)
            {
                case 6: // RRGGBB
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                    break;
                case 8: // AARRGGBB
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                    break;
                default:
                    throw new ArgumentException($"Invalid hex color format: #{hex}. Expected #RRGGBB or #AARRGGBB.", nameof(hex));
            }

            return Windows.UI.Color.FromArgb(a, r, g, b);
        }

        public static string ToHex(Windows.UI.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static SolidColorBrush ToBrush(string hex)
        {
            return new SolidColorBrush(FromHex(hex));
        }

        public static SolidColorBrush ToBrush(Windows.UI.Color color)
        {
            return new SolidColorBrush(color);
        }
    }
}
