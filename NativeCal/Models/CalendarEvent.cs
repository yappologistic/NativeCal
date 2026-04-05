using System;
using SQLite;

namespace NativeCal.Models
{
    [Table("CalendarEvents")]
    public class CalendarEvent
    {
        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }

        [NotNull, MaxLength(256), Column("Title")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2048), Column("Description")]
        public string? Description { get; set; }

        [MaxLength(512), Column("Location")]
        public string? Location { get; set; }

        [NotNull, Column("StartTime")]
        public DateTime StartTime { get; set; }

        [NotNull, Column("EndTime")]
        public DateTime EndTime { get; set; }

        [NotNull, Column("IsAllDay")]
        public bool IsAllDay { get; set; }

        [NotNull, Column("CalendarId")]
        public int CalendarId { get; set; }

        [MaxLength(32), Column("ColorHex")]
        public string? ColorHex { get; set; }

        [MaxLength(512), Column("RecurrenceRule")]
        public string? RecurrenceRule { get; set; }

        [NotNull, Column("ReminderMinutes")]
        public int ReminderMinutes { get; set; } = 15;

        [NotNull, Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotNull, Column("ModifiedAt")]
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        [Ignore]
        public bool IsReadOnly { get; set; }

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
