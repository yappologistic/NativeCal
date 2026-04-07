using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeCal.Helpers
{
    public static class ReminderOptionCatalog
    {
        // Keep reminder labels and minute values in one place so the
        // settings UI, event dialog, and persistence layer never drift apart.
        public static readonly (string Label, int Minutes)[] Options =
        {
            ("None", 0),
            ("5 minutes", 5),
            ("10 minutes", 10),
            ("15 minutes", 15),
            ("30 minutes", 30),
            ("1 hour", 60),
            ("2 hours", 120),
            ("1 day", 1440)
        };

        private static readonly HashSet<int> SupportedMinutes = Options
            .Select(option => option.Minutes)
            .ToHashSet();

        public static bool IsSupported(int minutes)
        {
            return SupportedMinutes.Contains(minutes);
        }

        public static int NormalizeMinutes(int minutes, int fallbackMinutes = 15)
        {
            return IsSupported(minutes) ? minutes : fallbackMinutes;
        }

        public static int GetSelectedIndexOrDefault(int minutes, int fallbackMinutes = 15)
        {
            int selectedIndex = Array.FindIndex(Options, option => option.Minutes == minutes);
            if (selectedIndex >= 0)
                return selectedIndex;

            int fallbackIndex = Array.FindIndex(Options, option => option.Minutes == fallbackMinutes);
            return fallbackIndex >= 0 ? fallbackIndex : 0;
        }
    }
}
