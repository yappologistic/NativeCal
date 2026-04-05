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

            var timedEvents = await _db.Table<CalendarEvent>()
                .Where(e =>
                    !e.IsAllDay &&
                    ((e.StartTime >= startDate && e.StartTime < endDate) ||
                     (e.EndTime > startDate && e.EndTime <= endDate) ||
                     (e.StartTime <= startDate && e.EndTime >= endDate)))
                .ToListAsync();

            var allDayEvents = await _db.Table<CalendarEvent>()
                .Where(e => e.IsAllDay)
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
            evt.ModifiedAt = DateTime.UtcNow;

            if (evt.Id > 0)
            {
                await _db.UpdateAsync(evt);
                return evt.Id;
            }
            else
            {
                evt.CreatedAt = DateTime.UtcNow;
                await _db.InsertAsync(evt);
                return evt.Id; // populated by AutoIncrement after insert
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
        /// </summary>
        public async Task<int> SaveCalendarAsync(CalendarInfo cal)
        {
            if (cal.Id > 0)
            {
                await _db.UpdateAsync(cal);
                return cal.Id;
            }
            else
            {
                cal.CreatedAt = DateTime.UtcNow;
                await _db.InsertAsync(cal);
                return cal.Id;
            }
        }

        /// <summary>
        /// Deletes a calendar and all events that belong to it.
        /// </summary>
        public async Task DeleteCalendarAsync(int id)
        {
            var calendars = (await _db.Table<CalendarInfo>().ToListAsync())
                .OrderBy(c => c.Id)
                .ToList();

            var calendarToDelete = calendars.FirstOrDefault(c => c.Id == id);
            if (calendarToDelete is null)
                return;

            if (CalendarCatalogHelper.IsProtectedCalendar(calendarToDelete))
                return;

            if (calendars.Count <= 1)
                return;

            var remainingCalendars = calendars
                .Where(c => c.Id != id)
                .OrderBy(c => c.Id)
                .ToList();

            if (calendarToDelete.IsDefault && !remainingCalendars.Any(c => c.IsDefault))
            {
                var replacementDefault = remainingCalendars[0];
                replacementDefault.IsDefault = true;
                await _db.UpdateAsync(replacementDefault);
            }

            await _db.ExecuteAsync("DELETE FROM CalendarEvents WHERE CalendarId = ?", id);
            await _db.DeleteAsync<CalendarInfo>(id);
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
