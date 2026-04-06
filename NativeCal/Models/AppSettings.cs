using SQLite;

namespace NativeCal.Models
{
    /// <summary>
    /// Key/value pair stored in the AppSettings table for persisting user preferences
    /// such as theme selection, default reminder, and first day of week.
    /// </summary>
    [Table("AppSettings")]
    public class AppSettings
    {
        /// <summary>Auto-incremented primary key.</summary>
        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }

        /// <summary>Setting key (unique, e.g. "Theme", "DefaultReminderMinutes").</summary>
        [NotNull, Unique, MaxLength(256), Column("Key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>Setting value stored as a string (parsed by the consumer).</summary>
        [MaxLength(2048), Column("Value")]
        public string Value { get; set; } = string.Empty;
    }
}
