using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.Services;

public sealed class HolidayService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<HolidayRecord>>> _cache = new();
    private readonly Func<int, string, Task<IReadOnlyList<HolidayRecord>>> _fetcher;

    public HolidayService(Func<int, string, Task<IReadOnlyList<HolidayRecord>>>? fetcher = null)
    {
        _fetcher = fetcher ?? FetchFromApiAsync;
    }

    public async Task<List<CalendarEvent>> GetHolidayEventsAsync(DateTime startDate, DateTime endDate, IReadOnlyCollection<CalendarInfo> calendars)
    {
        if (endDate <= startDate)
            return new List<CalendarEvent>();

        var visibleHolidayCalendars = CalendarCatalogHelper.GetVisibleHolidayCalendars(calendars).ToList();
        if (visibleHolidayCalendars.Count == 0)
            return new List<CalendarEvent>();

        var events = new List<CalendarEvent>();
        var years = Enumerable.Range(startDate.Year, endDate.Year - startDate.Year + 1);

        foreach (var calendar in visibleHolidayCalendars)
        {
            if (!CalendarCatalogHelper.TryGetHolidayCalendar(calendar, out var definition))
                continue;

            foreach (var year in years)
            {
                var holidays = await GetHolidayRecordsAsync(year, definition.CountryCode);
                foreach (var holiday in holidays)
                {
                    DateTime holidayDate = holiday.Date.Date;
                    if (holidayDate < startDate.Date || holidayDate >= endDate.Date)
                        continue;

                    events.Add(new CalendarEvent
                    {
                        Id = CreateSyntheticId(definition.CountryCode, holidayDate, holiday.EnglishName),
                        Title = string.IsNullOrWhiteSpace(holiday.LocalName) ? holiday.EnglishName : holiday.LocalName,
                        Description = BuildDescription(definition.CountryDisplayName, holiday),
                        Location = definition.CountryDisplayName,
                        StartTime = holidayDate,
                        // Match the canonical user-created all-day contract: inclusive
                        // end of the holiday date, with time normalized to the last tick
                        // of the day for storage/display consistency.
                        EndTime = holidayDate.AddDays(1).AddTicks(-1),
                        IsAllDay = true,
                        CalendarId = calendar.Id,
                        ReminderMinutes = 0,
                        CreatedAt = holidayDate,
                        ModifiedAt = holidayDate,
                        IsReadOnly = true,
                        IsOfficialHoliday = true
                    });
                }
            }
        }

        return events
            .OrderBy(e => e.StartTime)
            .ThenBy(e => e.Title)
            .ToList();
    }

    private Task<IReadOnlyList<HolidayRecord>> GetHolidayRecordsAsync(int year, string countryCode)
    {
        string key = string.Create(CultureInfo.InvariantCulture, $"{countryCode}:{year}");
        return _cache.GetOrAdd(key, _ => _fetcher(year, countryCode));
    }

    private static async Task<IReadOnlyList<HolidayRecord>> FetchFromApiAsync(int year, string countryCode)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"https://date.nager.at/api/v3/publicholidays/{year}/{countryCode}");
            if (!response.IsSuccessStatusCode)
                return Array.Empty<HolidayRecord>();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var records = await JsonSerializer.DeserializeAsync<List<HolidayRecord>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return records is not null
                ? records
                : (IReadOnlyList<HolidayRecord>)Array.Empty<HolidayRecord>();
        }
        catch
        {
            return Array.Empty<HolidayRecord>();
        }
    }

    private static int CreateSyntheticId(string countryCode, DateTime date, string name)
    {
        int hash = HashCode.Combine(countryCode, date.Year, date.Month, date.Day, name);
        return hash == int.MinValue ? int.MinValue + 1 : -Math.Abs(hash == 0 ? 1 : hash);
    }

    private static string BuildDescription(string countryDisplayName, HolidayRecord holiday)
    {
        string holidayTypes = holiday.Types is { Length: > 0 }
            ? string.Join(", ", holiday.Types)
            : "Holiday";

        return $"{holiday.EnglishName} ({countryDisplayName})\nType: {holidayTypes}";
    }

    public sealed class HolidayRecord
    {
        public DateTime Date { get; set; }
        public string LocalName { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;
        public string Name
        {
            get => EnglishName;
            set => EnglishName = value;
        }
        public string[] Types { get; set; } = Array.Empty<string>();
    }
}
