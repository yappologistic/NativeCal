using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;
using NativeCal.Services;

namespace NativeCal.Tests.Services;

public class HolidayServiceTests
{
    [Fact]
    public async Task GetHolidayEventsAsync_ReturnsReadOnlyEventsForVisibleHolidayCalendars()
    {
        var service = new HolidayService((year, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            new[]
            {
                new HolidayService.HolidayRecord
                {
                    Date = new DateTime(2026, 7, 1),
                    LocalName = "Canada Day",
                    EnglishName = "Canada Day",
                    Types = new[] { "Public" }
                }
            }));

        var calendars = new[]
        {
            new CalendarInfo { Id = 10, Name = "Canada Holidays", ColorHex = "#E11D48", IsVisible = true }
        };

        var events = await service.GetHolidayEventsAsync(new DateTime(2026, 7, 1), new DateTime(2026, 7, 2), calendars);

        var holiday = Assert.Single(events);
        Assert.Equal("Canada Day", holiday.Title);
        Assert.True(holiday.IsAllDay);
        Assert.True(holiday.IsReadOnly);
        Assert.True(holiday.IsOfficialHoliday);
        Assert.Equal(10, holiday.CalendarId);
    }

    /// <summary>
    /// Verifies that holiday events use the same EndTime convention as
    /// user-created all-day events (midnight of the holiday date).
    /// This ensures consistent behavior across all query and display code.
    /// </summary>
    [Fact]
    public async Task GetHolidayEventsAsync_EndTimeUsesConsistentMidnightConvention()
    {
        var service = new HolidayService((year, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            new[]
            {
                new HolidayService.HolidayRecord
                {
                    Date = new DateTime(2026, 7, 4),
                    LocalName = "Independence Day",
                    EnglishName = "Independence Day",
                    Types = new[] { "Public" }
                }
            }));

        var calendars = new[]
        {
            new CalendarInfo { Id = 20, Name = "US Holidays", ColorHex = "#3B82F6", IsVisible = true }
        };

        var holiday = Assert.Single(
            await service.GetHolidayEventsAsync(new DateTime(2026, 7, 4), new DateTime(2026, 7, 5), calendars));

        // StartTime and EndTime should both be midnight of the holiday date,
        // matching the convention EventDialog uses for single-day all-day events.
        Assert.Equal(new DateTime(2026, 7, 4), holiday.StartTime);
        Assert.Equal(new DateTime(2026, 7, 4), holiday.EndTime);
        Assert.Equal(TimeSpan.Zero, holiday.StartTime.TimeOfDay);
        Assert.Equal(TimeSpan.Zero, holiday.EndTime.TimeOfDay);
    }

    [Fact]
    public async Task GetHolidayEventsAsync_UsesNegativeSyntheticIds()
    {
        var service = new HolidayService((year, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            new[]
            {
                new HolidayService.HolidayRecord
                {
                    Date = new DateTime(2026, 7, 1),
                    LocalName = "Canada Day",
                    EnglishName = "Canada Day",
                    Types = new[] { "Public" }
                }
            }));

        var calendars = new[]
        {
            new CalendarInfo { Id = 10, Name = "Canada Holidays", ColorHex = "#E11D48", IsVisible = true }
        };

        var events = await service.GetHolidayEventsAsync(new DateTime(2026, 7, 1), new DateTime(2026, 7, 2), calendars);

        Assert.All(events, e => Assert.True(e.Id < 0));
    }

    [Fact]
    public async Task GetHolidayEventsAsync_CachesHolidayFetchesPerCountryAndYear()
    {
        int fetchCount = 0;
        var service = new HolidayService((year, countryCode) =>
        {
            fetchCount++;
            return Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
                new[]
                {
                    new HolidayService.HolidayRecord
                    {
                        Date = new DateTime(2026, 7, 4),
                        LocalName = "Independence Day",
                        EnglishName = "Independence Day",
                        Types = new[] { "Public" }
                    }
                });
        });

        var calendars = new[]
        {
            new CalendarInfo { Id = 20, Name = "US Holidays", ColorHex = "#3B82F6", IsVisible = true }
        };

        await service.GetHolidayEventsAsync(new DateTime(2026, 7, 1), new DateTime(2026, 7, 5), calendars);
        await service.GetHolidayEventsAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), calendars);

        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task GetHolidayEventsAsync_SkipsHiddenHolidayCalendars()
    {
        var service = new HolidayService((year, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            new[]
            {
                new HolidayService.HolidayRecord
                {
                    Date = new DateTime(2026, 7, 4),
                    LocalName = "Independence Day",
                    EnglishName = "Independence Day",
                    Types = new[] { "Public" }
                }
            }));

        var calendars = new[]
        {
            new CalendarInfo { Id = 20, Name = "US Holidays", ColorHex = "#3B82F6", IsVisible = false }
        };

        var events = await service.GetHolidayEventsAsync(new DateTime(2026, 7, 4), new DateTime(2026, 7, 5), calendars);

        Assert.Empty(events);
    }

    [Fact]
    public async Task GetHolidayEventsAsync_FallsBackToEnglishNameAndGenericTypeDescription()
    {
        var service = new HolidayService((year, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            new[]
            {
                new HolidayService.HolidayRecord
                {
                    Date = new DateTime(2026, 7, 4),
                    LocalName = "   ",
                    EnglishName = "Independence Day",
                    Types = Array.Empty<string>()
                }
            }));

        var calendars = new[]
        {
            new CalendarInfo { Id = 20, Name = "US Holidays", ColorHex = "#3B82F6", IsVisible = true }
        };

        var holiday = Assert.Single(await service.GetHolidayEventsAsync(new DateTime(2026, 7, 4), new DateTime(2026, 7, 5), calendars));

        Assert.Equal("Independence Day", holiday.Title);
        Assert.Equal("United States", holiday.Location);
        Assert.Contains("Independence Day (United States)", holiday.Description);
        Assert.Contains("Type: Holiday", holiday.Description);
    }

    [Fact]
    public async Task GetHolidayEventsAsync_ReturnsEmptyForReversedRange()
    {
        var service = new HolidayService((year, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            new[]
            {
                new HolidayService.HolidayRecord
                {
                    Date = new DateTime(2026, 7, 4),
                    LocalName = "Independence Day",
                    EnglishName = "Independence Day",
                    Types = new[] { "Public" }
                }
            }));

        var calendars = new[]
        {
            new CalendarInfo { Id = 20, Name = "US Holidays", ColorHex = "#3B82F6", IsVisible = true }
        };

        var events = await service.GetHolidayEventsAsync(new DateTime(2026, 7, 5), new DateTime(2026, 7, 4), calendars);

        Assert.Empty(events);
    }

    [Fact]
    public async Task GetHolidayEventsAsync_FetchesEachYearAcrossCrossYearRangeAndOrdersResults()
    {
        int fetchCount = 0;
        var service = new HolidayService((year, countryCode) =>
        {
            fetchCount++;
            IReadOnlyList<HolidayService.HolidayRecord> holidays = year switch
            {
                2026 =>
                [
                    new HolidayService.HolidayRecord
                    {
                        Date = new DateTime(2026, 12, 31),
                        LocalName = "New Year's Eve",
                        EnglishName = "New Year's Eve",
                        Types = new[] { "Observance" }
                    }
                ],
                2027 =>
                [
                    new HolidayService.HolidayRecord
                    {
                        Date = new DateTime(2027, 1, 1),
                        LocalName = "New Year's Day",
                        EnglishName = "New Year's Day",
                        Types = new[] { "Public" }
                    }
                ],
                _ => Array.Empty<HolidayService.HolidayRecord>()
            };

            return Task.FromResult(holidays);
        });

        var calendars = new[]
        {
            new CalendarInfo { Id = 10, Name = "Canada Holidays", ColorHex = "#E11D48", IsVisible = true }
        };

        var events = await service.GetHolidayEventsAsync(new DateTime(2026, 12, 31), new DateTime(2027, 1, 2), calendars);

        Assert.Equal(2, fetchCount);
        Assert.Equal(
            new[] { new DateTime(2026, 12, 31), new DateTime(2027, 1, 1) },
            events.Select(e => e.StartTime.Date).ToArray());
        Assert.Equal(new[] { "New Year's Eve", "New Year's Day" }, events.Select(e => e.Title).ToArray());
    }
}
