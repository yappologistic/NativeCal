namespace NativeCal.Models
{
    /// <summary>
    /// Defines how an event repeats. Stored as the enum name string
    /// in <see cref="CalendarEvent.RecurrenceRule"/> (e.g. "Weekly").
    /// Currently used for display only — no automatic occurrence expansion.
    /// </summary>
    public enum RecurrenceType
    {
        /// <summary>No recurrence — single occurrence.</summary>
        None,
        /// <summary>Repeats every day.</summary>
        Daily,
        /// <summary>Repeats every week on the same day.</summary>
        Weekly,
        /// <summary>Repeats every two weeks on the same day.</summary>
        Biweekly,
        /// <summary>Repeats on the same day of each month.</summary>
        Monthly,
        /// <summary>Repeats on the same date each year.</summary>
        Yearly
    }
}
