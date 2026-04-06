using System;
using SQLite;

namespace NativeCal.Models
{
    /// <summary>
    /// Represents a named calendar that groups events and defines a display color.
    /// The app seeds three default calendars (Personal, Work, Family) on first launch,
    /// plus two holiday calendars (US Holidays, Canada Holidays).
    /// </summary>
    [Table("Calendars")]
    public class CalendarInfo
    {
        /// <summary>Auto-incremented primary key.</summary>
        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }

        /// <summary>Display name shown in the sidebar and event dialogs.</summary>
        [NotNull, MaxLength(256), Column("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Hex color string (e.g. "#4A90D9") used for event chips.</summary>
        [NotNull, MaxLength(32), Column("ColorHex")]
        public string ColorHex { get; set; } = "#4A90D9";

        /// <summary>Whether events from this calendar are shown in views.</summary>
        [NotNull, Column("IsVisible")]
        public bool IsVisible { get; set; } = true;

        /// <summary>True if this is the default calendar for new events.</summary>
        [NotNull, Column("IsDefault")]
        public bool IsDefault { get; set; } = false;

        /// <summary>UTC timestamp when this calendar was created.</summary>
        [NotNull, Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
