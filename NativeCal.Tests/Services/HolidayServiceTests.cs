using System;
using System.Collections.Generic;
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
}
