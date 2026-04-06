using System;
using SQLite;

namespace NativeCal.Models
{
    /// <summary>
    /// Represents a single calendar event stored in the SQLite database.
    /// Each event belongs to exactly one <see cref="CalendarInfo"/> (via <see cref="CalendarId"/>).
    /// Holiday events use negative synthetic IDs and have <see cref="IsReadOnly"/> set to true.
    /// </summary>
    [Table("CalendarEvents")]
    public class CalendarEvent
    {
        /// <summary>Auto-incremented primary key. Negative for synthetic holiday events.</summary>
        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }

        /// <summary>Event title (required, max 256 chars).</summary>
        [NotNull, MaxLength(256), Column("Title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional long-form description.</summary>
        [MaxLength(2048), Column("Description")]
        public string? Description { get; set; }

        /// <summary>Optional location or venue.</summary>
        [MaxLength(512), Column("Location")]
        public string? Location { get; set; }

        /// <summary>When the event starts. For all-day events, the date component is used.</summary>
        [NotNull, Column("StartTime")]
        public DateTime StartTime { get; set; }

        /// <summary>When the event ends. Must be &gt;= <see cref="StartTime"/>.</summary>
        [NotNull, Column("EndTime")]
        public DateTime EndTime { get; set; }

        /// <summary>True if this is an all-day event (no specific start/end time).</summary>
        [NotNull, Column("IsAllDay")]
        public bool IsAllDay { get; set; }

        /// <summary>Foreign key to <see cref="CalendarInfo.Id"/>.</summary>
        [NotNull, Column("CalendarId")]
        public int CalendarId { get; set; }

        /// <summary>Optional per-event color override (e.g. "#E74C3C"). Null = use calendar color.</summary>
        [MaxLength(32), Column("ColorHex")]
        public string? ColorHex { get; set; }

        /// <summary>
        /// Recurrence rule stored as the <see cref="RecurrenceType"/> enum name
        /// (e.g. "Daily", "Weekly"). Null = no recurrence.
        /// </summary>
        [MaxLength(512), Column("RecurrenceRule")]
        public string? RecurrenceRule { get; set; }

        /// <summary>How many minutes before the event to show a reminder. 0 = no reminder.</summary>
        [NotNull, Column("ReminderMinutes")]
        public int ReminderMinutes { get; set; } = 15;

        /// <summary>UTC timestamp when the event was first created.</summary>
        [NotNull, Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp of the last modification.</summary>
        [NotNull, Column("ModifiedAt")]
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>True for holiday events — prevents editing and deleting in the UI.</summary>
        [Ignore]
        public bool IsReadOnly { get; set; }

        /// <summary>True if this event represents an official public holiday.</summary>
        [Ignore]
        public bool IsOfficialHoliday { get; set; }

        /// <summary>
        /// Returns a deep copy of this CalendarEvent.
        /// </summary>
        public CalendarEvent Clone()
        {
            return new CalendarEvent
            {
                Id = this.Id,
                Title = this.Title,
                Description = this.Description,
                Location = this.Location,
                StartTime = this.StartTime,
                EndTime = this.EndTime,
                IsAllDay = this.IsAllDay,
                CalendarId = this.CalendarId,
                ColorHex = this.ColorHex,
                RecurrenceRule = this.RecurrenceRule,
                ReminderMinutes = this.ReminderMinutes,
                CreatedAt = this.CreatedAt,
                ModifiedAt = this.ModifiedAt,
                IsReadOnly = this.IsReadOnly,
                IsOfficialHoliday = this.IsOfficialHoliday
            };
        }
    }
}
