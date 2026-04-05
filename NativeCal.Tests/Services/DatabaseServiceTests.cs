using System;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;
using NativeCal.Services;

namespace NativeCal.Tests.Services;

public class DatabaseServiceTests : TestBase
{
    // ── Initialization ──────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_CreatesDefaultCalendars()
    {
        var calendars = await Db.GetCalendarsAsync();

        Assert.Equal(3, calendars.Count);
        Assert.Contains(calendars, c => c.Name == "Personal" && c.IsDefault);
        Assert.Contains(calendars, c => c.Name == "Work");
        Assert.Contains(calendars, c => c.Name == "Family");
    }

    [Fact]
    public async Task InitializeAsync_DoesNotDuplicateCalendarsOnSecondCall()
    {
        // InitializeAsync was already called in TestBase, call again
        await Db.InitializeAsync();

        var calendars = await Db.GetCalendarsAsync();
        Assert.Equal(3, calendars.Count);
    }

    [Fact]
    public async Task Constructor_ThrowsOnNullPath()
    {
        Assert.Throws<ArgumentNullException>(() => new DatabaseService(null!));
    }

    // ── Save / Get Events ───────────────────────────────────────────────

    [Fact]
    public async Task SaveEventAsync_InsertsNewEvent_AndSetsId()
    {
        var evt = CreateTestEvent(title: "Test Event");

        int id = await Db.SaveEventAsync(evt);

        Assert.True(id > 0);
        Assert.Equal(id, evt.Id);
    }

    [Fact]
    public async Task SaveEventAsync_InsertSetsCreatedAt()
    {
        var evt = CreateTestEvent(title: "Test");
        var beforeSave = DateTime.UtcNow;

        await Db.SaveEventAsync(evt);

        // CreatedAt is set server-side; the original should have been updated
        Assert.True(evt.CreatedAt >= beforeSave.AddSeconds(-1));
    }

    [Fact]
    public async Task SaveEventAsync_UpdateDoesNotChangeCreatedAt()
    {
        var evt = CreateTestEvent(title: "Test");
        await Db.SaveEventAsync(evt);
        var originalCreatedAt = evt.CreatedAt;

        evt.Title = "Updated Title";
        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Equal(originalCreatedAt, fetched.CreatedAt);
        Assert.Equal("Updated Title", fetched.Title);
    }

    [Fact]
    public async Task SaveEventAsync_AlwaysUpdatesModifiedAt()
    {
        var evt = CreateTestEvent(title: "Test");
        await Db.SaveEventAsync(evt);

        var firstModified = evt.ModifiedAt;

        // Wait a tiny bit to ensure different timestamp
        await Task.Delay(10);

        evt.Title = "Updated";
        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.True(fetched.ModifiedAt >= firstModified);
    }

    [Fact]
    public async Task GetEventAsync_ReturnsEventById()
    {
        var evt = CreateTestEvent(title: "Find Me");
        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));

        Assert.Equal("Find Me", fetched.Title);
        Assert.Equal(evt.StartTime, fetched.StartTime);
        Assert.Equal(evt.EndTime, fetched.EndTime);
    }

    [Fact]
    public async Task GetEventAsync_ReturnsNullForNonexistentId()
    {
        var fetched = await Db.GetEventAsync(99999);
        Assert.Null(fetched);
    }

    // ── Delete Events ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEventAsync_RemovesEvent()
    {
        var evt = CreateTestEvent(title: "Delete Me");
        await Db.SaveEventAsync(evt);

        int deleted = await Db.DeleteEventAsync(evt.Id);
        Assert.Equal(1, deleted);

        var fetched = await Db.GetEventAsync(evt.Id);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task DeleteEventAsync_ReturnsZeroForNonexistentId()
    {
        int deleted = await Db.DeleteEventAsync(99999);
        Assert.Equal(0, deleted);
    }

    // ── GetEventsAsync (date range overlap) ─────────────────────────────

    [Fact]
    public async Task GetEventsAsync_ReturnsEventsStartingInRange()
    {
        var evt = CreateTestEvent(
            title: "In Range",
            start: new DateTime(2026, 4, 5, 9, 0, 0),
            end: new DateTime(2026, 4, 5, 10, 0, 0));
        await Db.SaveEventAsync(evt);

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 10));

        Assert.Single(results);
        Assert.Equal("In Range", results[0].Title);
    }

    [Fact]
    public async Task GetEventsAsync_ExcludesEventsOutsideRange()
    {
        var evt = CreateTestEvent(
            title: "Out of Range",
            start: new DateTime(2026, 5, 1, 9, 0, 0),
            end: new DateTime(2026, 5, 1, 10, 0, 0));
        await Db.SaveEventAsync(evt);

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsEventsEndingInRange()
    {
        var evt = CreateTestEvent(
            title: "Ends in Range",
            start: new DateTime(2026, 3, 30, 9, 0, 0),
            end: new DateTime(2026, 4, 2, 10, 0, 0));
        await Db.SaveEventAsync(evt);

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 5));

        Assert.Single(results);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsEventsSpanningEntireRange()
    {
        var evt = CreateTestEvent(
            title: "Spans Everything",
            start: new DateTime(2026, 3, 1, 0, 0, 0),
            end: new DateTime(2026, 5, 1, 0, 0, 0));
        await Db.SaveEventAsync(evt);

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        Assert.Single(results);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsMultipleEventsInSameRange()
    {
        for (int i = 0; i < 5; i++)
        {
            await Db.SaveEventAsync(CreateTestEvent(
                title: $"Event {i}",
                start: new DateTime(2026, 4, 5, 9 + i, 0, 0),
                end: new DateTime(2026, 4, 5, 9 + i, 30, 0)));
        }

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 10));

        Assert.Equal(5, results.Count);
    }

    // ── GetEventsForDateAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetEventsForDateAsync_ReturnsEventsForSpecificDate()
    {
        await Db.SaveEventAsync(CreateTestEvent(
            title: "Today Event",
            start: new DateTime(2026, 4, 5, 9, 0, 0),
            end: new DateTime(2026, 4, 5, 10, 0, 0)));

        await Db.SaveEventAsync(CreateTestEvent(
            title: "Tomorrow Event",
            start: new DateTime(2026, 4, 6, 9, 0, 0),
            end: new DateTime(2026, 4, 6, 10, 0, 0)));

        var results = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));

        Assert.Single(results);
        Assert.Equal("Today Event", results[0].Title);
    }

    [Fact]
    public async Task GetEventsForDateAsync_IncludesAllDayEvents()
    {
        await Db.SaveEventAsync(CreateTestEvent(
            title: "All Day",
            start: new DateTime(2026, 4, 5, 0, 0, 0),
            end: new DateTime(2026, 4, 5, 23, 59, 59),
            isAllDay: true));

        var results = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));

        Assert.Single(results);
        Assert.True(results[0].IsAllDay);
    }

    [Fact]
    public async Task GetEventsForDateAsync_IncludesEventsSpanningMidnight()
    {
        await Db.SaveEventAsync(CreateTestEvent(
            title: "Night Owl",
            start: new DateTime(2026, 4, 4, 22, 0, 0),
            end: new DateTime(2026, 4, 5, 2, 0, 0)));

        var results = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));

        Assert.Single(results);
    }

    // ── GetEventsByCalendarAsync ────────────────────────────────────────

    [Fact]
    public async Task GetEventsByCalendarAsync_ReturnsOnlyEventsForCalendar()
    {
        var calendars = await Db.GetCalendarsAsync();
        int cal1 = calendars[0].Id;
        int cal2 = calendars[1].Id;

        await Db.SaveEventAsync(CreateTestEvent(title: "Cal1 Event", calendarId: cal1));
        await Db.SaveEventAsync(CreateTestEvent(title: "Cal2 Event", calendarId: cal2));

        var results = await Db.GetEventsByCalendarAsync(cal1);

        Assert.All(results, r => Assert.Equal(cal1, r.CalendarId));
        Assert.Single(results);
    }

    [Fact]
    public async Task GetEventsByCalendarAsync_ReturnsEmptyForUnusedCalendar()
    {
        var results = await Db.GetEventsByCalendarAsync(99999);
        Assert.Empty(results);
    }

    // ── SearchEventsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SearchEventsAsync_FindsByTitle()
    {
        await Db.SaveEventAsync(CreateTestEvent(title: "Team Standup"));
        await Db.SaveEventAsync(CreateTestEvent(title: "Lunch Break"));

        var results = await Db.SearchEventsAsync("Standup");

        Assert.Single(results);
        Assert.Equal("Team Standup", results[0].Title);
    }

    [Fact]
    public async Task SearchEventsAsync_FindsByDescription()
    {
        await Db.SaveEventAsync(CreateTestEvent(
            title: "Meeting",
            description: "Quarterly budget review"));

        var results = await Db.SearchEventsAsync("budget");

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchEventsAsync_ReturnsEmptyForNullOrWhitespace()
    {
        await Db.SaveEventAsync(CreateTestEvent(title: "Something"));

        Assert.Empty(await Db.SearchEventsAsync(null!));
        Assert.Empty(await Db.SearchEventsAsync(""));
        Assert.Empty(await Db.SearchEventsAsync("   "));
    }

    [Fact]
    public async Task SearchEventsAsync_ReturnsEmptyForNoMatches()
    {
        await Db.SaveEventAsync(CreateTestEvent(title: "Meeting"));

        var results = await Db.SearchEventsAsync("zzzznonexistent");
        Assert.Empty(results);
    }

    // ── Calendar CRUD ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveCalendarAsync_InsertsNewCalendar()
    {
        var cal = new CalendarInfo
        {
            Name = "Test Calendar",
            ColorHex = "#FF0000",
            IsVisible = true,
            IsDefault = false
        };

        int id = await Db.SaveCalendarAsync(cal);
        Assert.True(id > 0);

        var calendars = await Db.GetCalendarsAsync();
        Assert.Equal(4, calendars.Count);
        Assert.Contains(calendars, c => c.Name == "Test Calendar");
    }

    [Fact]
    public async Task SaveCalendarAsync_UpdatesExistingCalendar()
    {
        var calendars = await Db.GetCalendarsAsync();
        var cal = calendars[0];
        cal.Name = "Renamed";

        await Db.SaveCalendarAsync(cal);

        var updated = await Db.GetCalendarsAsync();
        Assert.Contains(updated, c => c.Name == "Renamed" && c.Id == cal.Id);
    }

    [Fact]
    public async Task DeleteCalendarAsync_RemovesCalendar()
    {
        var calendars = await Db.GetCalendarsAsync();
        var toDelete = calendars.First(c => !c.IsDefault);

        await Db.DeleteCalendarAsync(toDelete.Id);

        var remaining = await Db.GetCalendarsAsync();
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, c => c.Id == toDelete.Id);
    }

    [Fact]
    public async Task DeleteCalendarAsync_AlsoDeletesAssociatedEvents()
    {
        var calendars = await Db.GetCalendarsAsync();
        int calId = calendars[0].Id;

        // Add events to this calendar
        await Db.SaveEventAsync(CreateTestEvent(title: "Event A", calendarId: calId));
        await Db.SaveEventAsync(CreateTestEvent(title: "Event B", calendarId: calId));

        // Verify events exist
        var eventsBefore = await Db.GetEventsByCalendarAsync(calId);
        Assert.Equal(2, eventsBefore.Count);

        // Delete the calendar
        await Db.DeleteCalendarAsync(calId);

        // Verify events are gone
        var eventsAfter = await Db.GetEventsByCalendarAsync(calId);
        Assert.Empty(eventsAfter);
    }

    // ── Settings ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingAsync_ReturnsDefaultWhenNotSet()
    {
        string value = await Db.GetSettingAsync("NonExistent", "fallback");
        Assert.Equal("fallback", value);
    }

    [Fact]
    public async Task SetSettingAsync_ThenGet_ReturnsValue()
    {
        await Db.SetSettingAsync("Theme", "2");
        string value = await Db.GetSettingAsync("Theme", "0");
        Assert.Equal("2", value);
    }

    [Fact]
    public async Task SetSettingAsync_UpdatesExistingKey()
    {
        await Db.SetSettingAsync("Theme", "1");
        await Db.SetSettingAsync("Theme", "2");

        string value = await Db.GetSettingAsync("Theme", "0");
        Assert.Equal("2", value);
    }

    [Theory]
    [InlineData("Theme", "0")]
    [InlineData("Theme", "1")]
    [InlineData("Theme", "2")]
    [InlineData("DefaultReminderMinutes", "0")]
    [InlineData("DefaultReminderMinutes", "15")]
    [InlineData("DefaultReminderMinutes", "1440")]
    [InlineData("FirstDayOfWeek", "0")]
    [InlineData("FirstDayOfWeek", "1")]
    public async Task Settings_RoundTrip(string key, string value)
    {
        await Db.SetSettingAsync(key, value);
        string result = await Db.GetSettingAsync(key, "NOT_FOUND");
        Assert.Equal(value, result);
    }

    // ── Edge Cases ──────────────────────────────────────────────────────

    [Fact]
    public async Task SaveEventAsync_HandlesVeryLongTitle()
    {
        string longTitle = new string('A', 256);
        var evt = CreateTestEvent(title: longTitle);
        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Equal(longTitle, fetched.Title);
    }

    [Fact]
    public async Task SaveEventAsync_HandlesNullOptionalFields()
    {
        var evt = new CalendarEvent
        {
            Title = "Minimal Event",
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            IsAllDay = false,
            CalendarId = 1,
            Description = null,
            Location = null,
            ColorHex = null,
            RecurrenceRule = null
        };

        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Null(fetched.Description);
        Assert.Null(fetched.Location);
        Assert.Null(fetched.ColorHex);
        Assert.Null(fetched.RecurrenceRule);
    }

    [Fact]
    public async Task SaveEventAsync_HandlesAllDayEvent()
    {
        var evt = CreateTestEvent(
            title: "Full Day",
            start: new DateTime(2026, 4, 5),
            end: new DateTime(2026, 4, 5),
            isAllDay: true);

        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.True(fetched.IsAllDay);
    }

    [Fact]
    public async Task SaveEventAsync_HandlesZeroReminderMinutes()
    {
        var evt = CreateTestEvent(title: "No Reminder");
        evt.ReminderMinutes = 0;

        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Equal(0, fetched.ReminderMinutes);
    }

    [Fact]
    public async Task SaveEventAsync_HandlesMaxReminderMinutes()
    {
        var evt = CreateTestEvent(title: "Day Before");
        evt.ReminderMinutes = 1440;

        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Equal(1440, fetched.ReminderMinutes);
    }

    [Fact]
    public async Task SaveEventAsync_HandlesRecurrenceRule()
    {
        var evt = CreateTestEvent(title: "Weekly");
        evt.RecurrenceRule = "Weekly";

        await Db.SaveEventAsync(evt);

        var fetched = Assert.IsType<CalendarEvent>(await Db.GetEventAsync(evt.Id));
        Assert.Equal("Weekly", fetched.RecurrenceRule);
    }

    [Fact]
    public async Task MultipleEventsOnSameDay_AllRetrievable()
    {
        var date = new DateTime(2026, 4, 5);

        for (int i = 0; i < 20; i++)
        {
            await Db.SaveEventAsync(CreateTestEvent(
                title: $"Event {i}",
                start: date.AddHours(8 + i * 0.5),
                end: date.AddHours(8 + i * 0.5 + 0.25)));
        }

        var events = await Db.GetEventsForDateAsync(date);
        Assert.Equal(20, events.Count);
    }

    [Fact]
    public async Task GetEventsAsync_ExactBoundaryStart()
    {
        // Event starts exactly at the range start
        await Db.SaveEventAsync(CreateTestEvent(
            title: "Boundary",
            start: new DateTime(2026, 4, 1, 0, 0, 0),
            end: new DateTime(2026, 4, 1, 1, 0, 0)));

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 2));

        Assert.Single(results);
    }

    [Fact]
    public async Task GetEventsAsync_ExactBoundaryEnd_Excluded()
    {
        // Event starts exactly at the range end — should NOT be included
        // because the condition is e.StartTime < endDate
        await Db.SaveEventAsync(CreateTestEvent(
            title: "At End",
            start: new DateTime(2026, 4, 5, 0, 0, 0),
            end: new DateTime(2026, 4, 5, 1, 0, 0)));

        var results = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 5)); // end is exclusive

        // This event starts exactly at endDate, so StartTime >= startDate && StartTime < endDate
        // 4/5 >= 4/1 is true, but 4/5 < 4/5 is false.
        // However, it also checks (e.EndTime > startDate && e.EndTime <= endDate)
        // EndTime = 4/5 1:00 AM > 4/1 is true, EndTime <= 4/5 is false (1am > midnight)
        // And span: StartTime <= startDate && EndTime >= endDate: 4/5 <= 4/1 is false
        // So this should be excluded since there's no overlap with [4/1, 4/5)
        Assert.Empty(results);
    }

    // ── Helper ──────────────────────────────────────────────────────────

    private static CalendarEvent CreateTestEvent(
        string title = "Test Event",
        DateTime? start = null,
        DateTime? end = null,
        bool isAllDay = false,
        int calendarId = 1,
        string? description = null)
    {
        var startTime = start ?? new DateTime(2026, 4, 5, 9, 0, 0);
        return new CalendarEvent
        {
            Title = title,
            StartTime = startTime,
            EndTime = end ?? startTime.AddHours(1),
            IsAllDay = isAllDay,
            CalendarId = calendarId,
            Description = description
        };
    }
}
