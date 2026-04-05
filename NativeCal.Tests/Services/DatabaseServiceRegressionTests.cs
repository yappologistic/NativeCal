using System;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;

namespace NativeCal.Tests.Services;

public class DatabaseServiceRegressionTests : TestBase
{
    [Fact]
    public async Task GetEventsForDateAsync_IncludesFinalDayOfLegacyAllDayEventStoredWithMidnightEnd()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Festival",
            StartTime = new DateTime(2026, 4, 3),
            EndTime = new DateTime(2026, 4, 5),
            IsAllDay = true,
            CalendarId = 1
        });

        var events = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));

        Assert.Contains(events, e => e.Title == "Festival");
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsEmptyForReversedRange()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Should not match",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });

        var events = await Db.GetEventsAsync(new DateTime(2026, 4, 6), new DateTime(2026, 4, 5));

        Assert.Empty(events);
    }

    [Fact]
    public async Task GetEventsAsync_ExcludesEventsFromHiddenCalendars()
    {
        var calendars = await Db.GetCalendarsAsync();
        var hiddenCalendar = Assert.Single(calendars.Where(c => !c.IsDefault).Take(1));
        hiddenCalendar.IsVisible = false;
        await Db.SaveCalendarAsync(hiddenCalendar);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Visible event",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = calendars[0].Id
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Hidden event",
            StartTime = new DateTime(2026, 4, 5, 11, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 12, 0, 0),
            CalendarId = hiddenCalendar.Id
        });

        var events = await Db.GetEventsAsync(new DateTime(2026, 4, 5), new DateTime(2026, 4, 6));

        var match = Assert.Single(events);
        Assert.Equal("Visible event", match.Title);
    }

    [Fact]
    public async Task GetEventsForDateAsync_ExcludesAllDayEventsFromHiddenCalendars()
    {
        var calendars = await Db.GetCalendarsAsync();
        var hiddenCalendar = Assert.Single(calendars.Where(c => !c.IsDefault).Take(1));
        hiddenCalendar.IsVisible = false;
        await Db.SaveCalendarAsync(hiddenCalendar);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Hidden all-day",
            StartTime = new DateTime(2026, 4, 5),
            EndTime = new DateTime(2026, 4, 5, 23, 59, 59),
            IsAllDay = true,
            CalendarId = hiddenCalendar.Id
        });

        var events = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));

        Assert.Empty(events);
    }

    [Fact]
    public async Task SearchEventsAsync_TreatsPercentUnderscoreAndBackslashAsLiteralCharacters()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = @"100%_done\\ready",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "100Xdone",
            StartTime = new DateTime(2026, 4, 5, 11, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 12, 0, 0),
            CalendarId = 1
        });

        var results = await Db.SearchEventsAsync(@"100%_done\\ready");

        var match = Assert.Single(results);
        Assert.Equal(@"100%_done\\ready", match.Title);
    }

    [Fact]
    public async Task DeleteCalendarAsync_DoesNotDeleteProtectedHolidayCalendars()
    {
        var holidayCalendar = Assert.Single(await Db.GetCalendarsAsync(), c => c.Name == "US Holidays");

        await Db.DeleteCalendarAsync(holidayCalendar.Id);

        var remaining = await Db.GetCalendarsAsync();
        Assert.Contains(remaining, c => c.Id == holidayCalendar.Id);
    }

    [Fact]
    public async Task GetEventsAsync_ExcludesEventStartingExactlyAtRangeEnd()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Starts at end",
            StartTime = new DateTime(2026, 4, 6, 0, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 1, 0, 0),
            CalendarId = 1
        });

        var events = await Db.GetEventsAsync(new DateTime(2026, 4, 5), new DateTime(2026, 4, 6));

        Assert.Empty(events);
    }

    [Fact]
    public async Task GetEventsAsync_ExcludesEventEndingExactlyAtRangeStart()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Ends at start",
            StartTime = new DateTime(2026, 4, 4, 23, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 0, 0, 0),
            CalendarId = 1
        });

        var events = await Db.GetEventsAsync(new DateTime(2026, 4, 5), new DateTime(2026, 4, 6));

        Assert.Empty(events);
    }

    [Fact]
    public async Task GetEventsAsync_OrdersByStartTimeThenId()
    {
        var later = new CalendarEvent
        {
            Title = "Later",
            StartTime = new DateTime(2026, 4, 5, 10, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 11, 0, 0),
            CalendarId = 1
        };
        var first = new CalendarEvent
        {
            Title = "First",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 9, 30, 0),
            CalendarId = 1
        };
        var second = new CalendarEvent
        {
            Title = "Second",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 9, 45, 0),
            CalendarId = 1
        };

        await Db.SaveEventAsync(later);
        await Db.SaveEventAsync(first);
        await Db.SaveEventAsync(second);

        var events = await Db.GetEventsAsync(new DateTime(2026, 4, 5), new DateTime(2026, 4, 6));

        Assert.Equal(new[] { first.Id, second.Id, later.Id }, events.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsEmptyWhenAllCalendarsAreHidden()
    {
        var calendars = await Db.GetCalendarsAsync();
        foreach (var calendar in calendars)
        {
            calendar.IsVisible = false;
            await Db.SaveCalendarAsync(calendar);
        }

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Hidden by calendar state",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = calendars[0].Id
        });

        var events = await Db.GetEventsAsync(new DateTime(2026, 4, 5), new DateTime(2026, 4, 6));

        Assert.Empty(events);
    }

    [Fact]
    public async Task DeleteCalendarAsync_PromotesAnotherCalendarWhenDeletingTheDefault()
    {
        var defaultCalendar = Assert.Single(await Db.GetCalendarsAsync(), c => c.IsDefault);

        await Db.DeleteCalendarAsync(defaultCalendar.Id);

        var remaining = await Db.GetCalendarsAsync();

        Assert.Equal(4, remaining.Count);
        Assert.Single(remaining, c => c.IsDefault);
        Assert.DoesNotContain(remaining, c => c.Id == defaultCalendar.Id);
    }
}
