using System;
using SQLite;

namespace NativeCal.Models
{
    [Table("Calendars")]
    public class CalendarInfo
    {
        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }

        [NotNull, MaxLength(256), Column("Name")]
        public string Name { get; set; } = string.Empty;

        [NotNull, MaxLength(32), Column("ColorHex")]
        public string ColorHex { get; set; } = "#4A90D9";

        [NotNull, Column("IsVisible")]
        public bool IsVisible { get; set; } = true;

        [NotNull, Column("IsDefault")]
        public bool IsDefault { get; set; } = false;

        [NotNull, Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
