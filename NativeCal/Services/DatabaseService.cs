using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _db = null!;
        private readonly string _dbPath;

        public DatabaseService() : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NativeCal",
                "nativecal.db"))
        {
        }

        public DatabaseService(string dbPath)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        }

        public async Task InitializeAsync()
        {
            if (_db is not null)
                return;

            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _db = new SQLiteAsyncConnection(_dbPath);

            await _db.CreateTableAsync<CalendarEvent>();
            await _db.CreateTableAsync<CalendarInfo>();
            await _db.CreateTableAsync<AppSettings>();

            var existingCalendars = await _db.Table<CalendarInfo>().CountAsync();
            if (existingCalendars == 0)
            {
                await _db.InsertAsync(new CalendarInfo
                {
                    Name = "Personal",
                    ColorHex = "#4A90D9",
                    IsVisible = true,
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow
                });

                await _db.InsertAsync(new CalendarInfo
                {
                    Name = "Work",
                    ColorHex = "#E74C3C",
                    IsVisible = true,
                    IsDefault = false,
                    CreatedAt = DateTime.UtcNow
                });

                await _db.InsertAsync(new CalendarInfo
                {
                    Name = "Family",
                    ColorHex = "#27AE60",
                    IsVisible = true,
                    IsDefault = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var calendars = await _db.Table<CalendarInfo>().ToListAsync();
            foreach (var holidayCalendar in CalendarCatalogHelper.HolidayCalendars)
            {
                if (calendars.Any(c => string.Equals(c.Name, holidayCalendar.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                await _db.InsertAsync(new CalendarInfo
                {
                    Name = holidayCalendar.Name,
                    ColorHex = holidayCalendar.ColorHex,
                    IsVisible = true,
                    IsDefault = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // ── Events ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns all events whose time range overlaps with [startDate, endDate).
        /// </summary>
        public async Task<List<CalendarEvent>> GetEventsAsync(DateTime startDate, DateTime endDate)
        {
            if (endDate <= startDate)
                return new List<CalendarEvent>();

            HashSet<int> visibleCalendarIds = await GetVisibleCalendarIdsAsync();
            if (visibleCalendarIds.Count == 0)
                return new List<CalendarEvent>();

            // Timed events: standard overlap check against the date range.
            var timedEvents = await _db.Table<CalendarEvent>()
                .Where(e =>
                    !e.IsAllDay &&
                    ((e.StartTime >= startDate && e.StartTime < endDate) ||
                     (e.EndTime > startDate && e.EndTime <= endDate) ||
                     (e.StartTime <= startDate && e.EndTime >= endDate)))
                .ToListAsync();

            // All-day events: SQLite can't evaluate e.StartTime.Date so we widen
            // the query window by 1 day on each side and then filter in memory.
            // This avoids loading the ENTIRE all-day events table.
            // Guard against DateTime overflow at the boundaries.
            DateTime allDayQueryStart = startDate.Date > DateTime.MinValue.AddDays(1)
                ? startDate.Date.AddDays(-1)
                : DateTime.MinValue;
            DateTime allDayQueryEnd = endDate.Date < DateTime.MaxValue.AddDays(-1)
                ? endDate.Date.AddDays(1)
                : DateTime.MaxValue;

            var allDayEvents = await _db.Table<CalendarEvent>()
                .Where(e => e.IsAllDay &&
                            e.StartTime < allDayQueryEnd &&
                            e.EndTime >= allDayQueryStart)
                .ToListAsync();

            var overlappingAllDayEvents = allDayEvents
                .Where(e => e.StartTime.Date < endDate.Date && e.EndTime.Date >= startDate.Date);

            return timedEvents
                .Concat(overlappingAllDayEvents)
                .Where(e => visibleCalendarIds.Contains(e.CalendarId))
                .OrderBy(e => e.StartTime)
                .ThenBy(e => e.Id)
                .ToList();
        }

        /// <summary>
        /// Returns a single event by its primary key.
        /// </summary>
        public async Task<CalendarEvent?> GetEventAsync(int id)
        {
            return await _db.Table<CalendarEvent>()
                .Where(e => e.Id == id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Inserts a new event or updates an existing one.
        /// Sets CreatedAt on insert; always sets ModifiedAt.
        /// </summary>
        public async Task<int> SaveEventAsync(CalendarEvent evt)
        {
            await ValidateEventAsync(evt);
            evt.ModifiedAt = DateTime.UtcNow;

            if (evt.Id > 0)
            {
                // Surface stale edit attempts instead of silently pretending the
                // update worked. This protects callers from false-success writes.
                int rowsUpdated = await _db.UpdateAsync(evt);
                if (rowsUpdated == 0)
                {
                    throw new InvalidOperationException($"Event {evt.Id} no longer exists.");
                }

                return evt.Id;
            }

            evt.CreatedAt = DateTime.UtcNow;
            await _db.InsertAsync(evt);
            return evt.Id; // populated by AutoIncrement after insert
        }

        private async Task ValidateEventAsync(CalendarEvent evt)
        {
            ArgumentNullException.ThrowIfNull(evt);

            evt.Title = evt.Title?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(evt.Title))
            {
                throw new ArgumentException("Event title is required.", nameof(evt));
            }

            if (evt.EndTime < evt.StartTime)
            {
                throw new ArgumentException("Event end time cannot be earlier than its start time.", nameof(evt));
            }

            if (!ReminderOptionCatalog.IsSupported(evt.ReminderMinutes))
            {
                throw new ArgumentException("Reminder minutes value is not supported.", nameof(evt));
            }

            // Validate against the real calendar row so service callers cannot
            // create orphaned events or write into read-only holiday calendars.
            var calendar = await _db.Table<CalendarInfo>()
                .Where(c => c.Id == evt.CalendarId)
                .FirstOrDefaultAsync();

            if (calendar is null)
            {
                throw new InvalidOperationException($"Calendar {evt.CalendarId} does not exist.");
            }

            if (CalendarCatalogHelper.IsProtectedCalendar(calendar))
            {
                throw new InvalidOperationException("Events cannot be saved into read-only holiday calendars.");
            }
        }

        /// <summary>
        /// Deletes an event by its primary key.
        /// </summary>
        public async Task<int> DeleteEventAsync(int id)
        {
            return await _db.DeleteAsync<CalendarEvent>(id);
        }

        /// <summary>
        /// Returns all events that overlap with the given date
        /// (all-day events for that date, or events whose start/end spans that date).
        /// </summary>
        public async Task<List<CalendarEvent>> GetEventsForDateAsync(DateTime date)
        {
            var dayStart = date.Date;
            return await GetEventsAsync(dayStart, dayStart.AddDays(1));
        }

        /// <summary>
        /// Returns all events belonging to a specific calendar.
        /// </summary>
        public async Task<List<CalendarEvent>> GetEventsByCalendarAsync(int calendarId)
        {
            return await _db.Table<CalendarEvent>()
                .Where(e => e.CalendarId == calendarId)
                .ToListAsync();
        }

        /// <summary>
        /// Searches events whose Title or Description contains the query string (case-insensitive).
        /// sqlite-net LIKE is case-insensitive for ASCII by default.
        /// </summary>
        public async Task<List<CalendarEvent>> SearchEventsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<CalendarEvent>();

            // Escape LIKE wildcards so user input is treated literally
            string escaped = query
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = $"%{escaped}%";

            return await _db.QueryAsync<CalendarEvent>(
                "SELECT * FROM CalendarEvents WHERE Title LIKE ? ESCAPE '\\' OR Description LIKE ? ESCAPE '\\'",
                pattern, pattern);
        }

        // ── Calendars ────────────────────────────────────────────────────

        /// <summary>
        /// Returns all calendars.
        /// </summary>
        public async Task<List<CalendarInfo>> GetCalendarsAsync()
        {
            return await _db.Table<CalendarInfo>().ToListAsync();
        }

        private async Task<HashSet<int>> GetVisibleCalendarIdsAsync()
        {
            var calendars = await _db.Table<CalendarInfo>()
                .Where(c => c.IsVisible)
                .ToListAsync();

            return calendars.Select(c => c.Id).ToHashSet();
        }

        /// <summary>
        /// Inserts a new calendar or updates an existing one.
        /// Reserved built-in holiday names cannot be claimed by user calendars,
        /// and stale updates fail loudly instead of silently doing nothing.
        /// </summary>
        public async Task<int> SaveCalendarAsync(CalendarInfo cal)
        {
            cal.Name = cal.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cal.Name))
            {
                throw new ArgumentException("Calendar name is required.", nameof(cal));
            }

            if (cal.Id > 0)
            {
                var existingCalendar = await _db.Table<CalendarInfo>()
                    .Where(c => c.Id == cal.Id)
                    .FirstOrDefaultAsync();
                if (existingCalendar is null)
                {
                    throw new InvalidOperationException($"Calendar {cal.Id} no longer exists.");
                }

                // Block calendars from newly claiming a reserved holiday name while
                // still allowing legacy rows that already carry that name to be saved
                // unchanged (for example, when toggling visibility or repairing data).
                if (CalendarCatalogHelper.IsReservedCalendarName(cal.Name) &&
                    !string.Equals(existingCalendar.Name?.Trim(), cal.Name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Reserved holiday calendar names cannot be used for user calendars.");
                }

                await _db.UpdateAsync(cal);
                return cal.Id;
            }

            if (CalendarCatalogHelper.IsReservedCalendarName(cal.Name))
            {
                throw new InvalidOperationException("Reserved holiday calendar names cannot be used for user calendars.");
            }

            cal.CreatedAt = DateTime.UtcNow;
            await _db.InsertAsync(cal);
            return cal.Id;
        }

        /// <summary>
        /// Deletes a calendar and all events that belong to it.
        /// Protected calendars (holidays) cannot be deleted.
        /// At least one non-protected calendar must remain after deletion.
        /// If the deleted calendar was the default, another is promoted.
        /// </summary>
        public async Task DeleteCalendarAsync(int id)
        {
            // Keep default promotion and row deletion in a single SQLite transaction
            // so a mid-operation failure cannot leave partial calendar state behind.
            await _db.RunInTransactionAsync(conn =>
            {
                var calendars = conn.Table<CalendarInfo>()
                    .ToList()
                    .OrderBy(c => c.Id)
                    .ToList();

                var calendarToDelete = calendars.FirstOrDefault(c => c.Id == id);
                if (calendarToDelete is null)
                    return;

                // Holiday calendars are read-only and cannot be removed.
                if (CalendarCatalogHelper.IsProtectedCalendar(calendarToDelete))
                    return;

                // Ensure at least one non-protected (user-writable) calendar
                // remains after this deletion. Without a writable calendar the
                // user cannot create new events.
                var nonProtected = calendars
                    .Where(c => !CalendarCatalogHelper.IsProtectedCalendar(c))
                    .ToList();
                if (nonProtected.Count <= 1)
                    return;

                var remainingCalendars = calendars
                    .Where(c => c.Id != id)
                    .OrderBy(c => c.Id)
                    .ToList();

                // If the deleted calendar was the default and no other default
                // exists, promote the first remaining non-protected calendar.
                if (calendarToDelete.IsDefault && !remainingCalendars.Any(c => c.IsDefault))
                {
                    var replacementDefault = remainingCalendars
                        .FirstOrDefault(c => !CalendarCatalogHelper.IsProtectedCalendar(c));
                    if (replacementDefault is not null)
                    {
                        replacementDefault.IsDefault = true;
                        conn.Update(replacementDefault);
                    }
                }

                conn.Execute("DELETE FROM CalendarEvents WHERE CalendarId = ?", id);
                conn.Delete<CalendarInfo>(id);
            });
        }

        // ── Settings ─────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves a setting value by key, returning defaultValue if not found.
        /// </summary>
        public async Task<string> GetSettingAsync(string key, string defaultValue)
        {
            var setting = await _db.Table<AppSettings>()
                .Where(s => s.Key == key)
                .FirstOrDefaultAsync();

            return setting?.Value ?? defaultValue;
        }

        /// <summary>
        /// Returns the persisted default reminder in minutes, falling back to 15
        /// whenever the stored value is missing or invalid.
        /// </summary>
        public async Task<int> GetDefaultReminderMinutesAsync()
        {
            string storedValue = await GetSettingAsync("DefaultReminderMinutes", "15");
            return int.TryParse(storedValue, out int minutes)
                ? ReminderOptionCatalog.NormalizeMinutes(minutes)
                : 15;
        }

        /// <summary>
        /// Inserts or updates a setting identified by key.
        /// </summary>
        public async Task SetSettingAsync(string key, string value)
        {
            var existing = await _db.Table<AppSettings>()
                .Where(s => s.Key == key)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                existing.Value = value;
                await _db.UpdateAsync(existing);
            }
            else
            {
                await _db.InsertAsync(new AppSettings
                {
                    Key = key,
                    Value = value
                });
            }
        }
    }
}
