using System;
using System.Threading.Tasks;
using NativeCal.Models;

namespace NativeCal.Tests.Services;

/// <summary>
/// Edge-case tests for DatabaseService that focus on overlap logic,
/// boundary conditions, and multi-calendar scenarios.
/// </summary>
public class DatabaseServiceEdgeCaseTests : TestBase
{
    // ── Overlap logic edge cases ────────────────────────────────────────

    [Fact]
    public async Task GetEventsAsync_EventStartsBeforeRangeEndsInRange()
    {
        // Event starts before the range but ends within it
        await Db.SaveEventAsync(CreateEvent(
            start: new DateTime(2026, 3, 31, 22, 0, 0),
            end: new DateTime(2026, 4, 1, 2, 0, 0)));

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 2));

        Assert.Single(results);
    }

    [Fact]
    public async Task GetEventsForDateAsync_AllDayMultiDay_CoversAllDates()
    {
        // All-day event spanning April 3-5
        await Db.SaveEventAsync(CreateEvent(
            start: new DateTime(2026, 4, 3, 0, 0, 0),
            end: new DateTime(2026, 4, 5, 23, 59, 59),
            isAllDay: true));

        var apr3 = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 3));
        var apr4 = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 4));
        var apr5 = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));
        var apr2 = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 2));

        Assert.Single(apr3);
        Assert.Single(apr4);
        Assert.Single(apr5);
        Assert.Empty(apr2);
    }

    [Fact]
    public async Task GetEventsAsync_MultipleCalendars_EventsSeparated()
    {
        var calendars = await Db.GetCalendarsAsync();
        int cal1 = calendars[0].Id;
        int cal2 = calendars[1].Id;

        await Db.SaveEventAsync(CreateEvent(title: "Cal1", calendarId: cal1));
        await Db.SaveEventAsync(CreateEvent(title: "Cal2", calendarId: cal2));

        var cal1Events = await Db.GetEventsByCalendarAsync(cal1);
        var cal2Events = await Db.GetEventsByCalendarAsync(cal2);

        Assert.Single(cal1Events);
        Assert.Single(cal2Events);
        Assert.Equal("Cal1", cal1Events[0].Title);
        Assert.Equal("Cal2", cal2Events[0].Title);
    }

    [Fact]
    public async Task SearchEventsAsync_SpecialCharacters()
    {
        await Db.SaveEventAsync(CreateEvent(title: "O'Brien's Meeting"));
        await Db.SaveEventAsync(CreateEvent(title: "Review #project-x"));

        var results1 = await Db.SearchEventsAsync("O'Brien");
        Assert.Single(results1);

        var results2 = await Db.SearchEventsAsync("#project");
        Assert.Single(results2);
    }

    [Fact]
    public async Task SearchEventsAsync_PartialMatch()
    {
        await Db.SaveEventAsync(CreateEvent(title: "Quarterly Review"));

        var results = await Db.SearchEventsAsync("Quarter");
        Assert.Single(results);
    }

    [Fact]
    public async Task DeleteCalendarAsync_WithMultipleEvents_DeletesAll()
    {
        var calendars = await Db.GetCalendarsAsync();
        int calId = calendars[0].Id;

        for (int i = 0; i < 10; i++)
        {
            await Db.SaveEventAsync(CreateEvent(
                title: $"Event {i}",
                calendarId: calId,
                start: new DateTime(2026, 4, 5, 8 + i, 0, 0)));
        }

        await Db.DeleteCalendarAsync(calId);

        var events = await Db.GetEventsByCalendarAsync(calId);
        Assert.Empty(events);
    }

    [Fact]
    public async Task SaveEventAsync_SameStartAndEndTime()
    {
        // Zero-duration event
        var time = new DateTime(2026, 4, 5, 9, 0, 0);
        var evt = CreateEvent(start: time, end: time);

        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Equal(fetched.StartTime, fetched.EndTime);
    }

    [Fact]
    public async Task SaveEventAsync_EndBeforeStart_StillPersists()
    {
        // This tests whether the database allows inverted times
        var evt = CreateEvent(
            start: new DateTime(2026, 4, 5, 10, 0, 0),
            end: new DateTime(2026, 4, 5, 9, 0, 0));

        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        // The DB stores whatever we give it; validation is the app's responsibility
        Assert.True(fetched.EndTime < fetched.StartTime);
    }

    [Fact]
    public async Task GetEventsForDateAsync_DayBoundary_MidnightEvent()
    {
        // Event exactly at midnight
        await Db.SaveEventAsync(CreateEvent(
            start: new DateTime(2026, 4, 5, 0, 0, 0),
            end: new DateTime(2026, 4, 5, 0, 30, 0)));

        var results = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));
        Assert.Single(results);
    }

    [Fact]
    public async Task GetEventsForDateAsync_DayBoundary_2359Event()
    {
        await Db.SaveEventAsync(CreateEvent(
            start: new DateTime(2026, 4, 5, 23, 30, 0),
            end: new DateTime(2026, 4, 5, 23, 59, 0)));

        var results = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));
        Assert.Single(results);
    }

    [Fact]
    public async Task GetEventsForDateAsync_LongEventCoveringMultipleDays()
    {
        // 3-day event
        await Db.SaveEventAsync(CreateEvent(
            start: new DateTime(2026, 4, 4, 10, 0, 0),
            end: new DateTime(2026, 4, 7, 10, 0, 0)));

        // Should appear on April 4, 5, 6, 7
        Assert.Single(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 4)));
        Assert.Single(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5)));
        Assert.Single(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 6)));
        Assert.Single(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 7)));
        Assert.Empty(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 3)));
        Assert.Empty(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 8)));
    }

    [Fact]
    public async Task SaveEventAsync_ThenUpdateMultipleTimes()
    {
        var evt = CreateEvent(title: "V1");
        await Db.SaveEventAsync(evt);

        for (int i = 2; i <= 5; i++)
        {
            evt.Title = $"V{i}";
            await Db.SaveEventAsync(evt);
        }

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Equal("V5", fetched.Title);
    }

    [Fact]
    public async Task SaveCalendarAsync_MultipleNewCalendars()
    {
        for (int i = 0; i < 5; i++)
        {
            await Db.SaveCalendarAsync(new CalendarInfo
            {
                Name = $"Calendar {i}",
                ColorHex = "#4A90D9"
            });
        }

        var calendars = await Db.GetCalendarsAsync();
        // 3 defaults + 5 new = 8
        Assert.Equal(8, calendars.Count);
    }

    [Fact]
    public async Task GetEventsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var results = await Db.GetEventsAsync(DateTime.MinValue, DateTime.MaxValue);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetEventsForDateAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var results = await Db.GetEventsForDateAsync(DateTime.Today);
        Assert.Empty(results);
    }

    private static CalendarEvent CreateEvent(
        string title = "Test",
        DateTime? start = null,
        DateTime? end = null,
        bool isAllDay = false,
        int calendarId = 1)
    {
        var s = start ?? new DateTime(2026, 4, 5, 9, 0, 0);
        return new CalendarEvent
        {
            Title = title,
            StartTime = s,
            EndTime = end ?? s.AddHours(1),
            IsAllDay = isAllDay,
            CalendarId = calendarId
        };
    }
}
