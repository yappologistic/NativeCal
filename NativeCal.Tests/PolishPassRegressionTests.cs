using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.Services;
using NativeCal.ViewModels;

namespace NativeCal.Tests;

/// <summary>
/// Comprehensive regression tests for all bugs found and fixed in the
/// polishing pass. Each test is named after the specific bug it guards
/// against and includes a description of the original issue.
/// </summary>
public class PolishPassRegressionTests : TestBase
{
    // ════════════════════════════════════════════════════════════════════
    // BUG: SearchEventsAsync must treat %, _, and \ as literal chars
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("100%", "100% Complete", 1)]   // % should not be a wildcard
    [InlineData("_draft", "_draft PR", 1)]      // _ should not be a wildcard
    [InlineData("path\\to", "path\\to\\file", 1)] // \ should be literal
    [InlineData("no match", "Something else", 0)]
    public async Task SearchEventsAsync_EscapesLikeWildcards(string query, string title, int expectedCount)
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = title,
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });

        var results = await Db.SearchEventsAsync(query);

        Assert.Equal(expectedCount, results.Count);
    }

    // ════════════════════════════════════════════════════════════════════
    // BUG: GetEventAsync signature must return nullable CalendarEvent
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetEventAsync_ReturnsNullForMissingId()
    {
        CalendarEvent? result = await Db.GetEventAsync(999999);
        Assert.Null(result);
    }

    // ════════════════════════════════════════════════════════════════════
    // BUG: Calendar deletion must protect the last-calendar and default
    // calendar invariants at the service layer.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteCalendarAsync_CannotDeleteLastNonProtectedCalendar()
    {
        var calendars = await Db.GetCalendarsAsync();

        // Delete all non-protected calendars except one
        var nonProtected = calendars.Where(c => !CalendarCatalogHelper.IsProtectedCalendar(c)).ToList();
        for (int i = 0; i < nonProtected.Count - 1; i++)
        {
            await Db.DeleteCalendarAsync(nonProtected[i].Id);
        }

        // Try to delete the last non-protected calendar — should fail
        var last = nonProtected.Last();
        await Db.DeleteCalendarAsync(last.Id);

        // The calendar still exists plus 2 protected holiday calendars
        var remaining = await Db.GetCalendarsAsync();
        Assert.True(remaining.Count >= 3); // at least 1 user + 2 holiday
        Assert.Contains(remaining, c => c.Id == last.Id);
    }

    [Fact]
    public async Task DeleteCalendarAsync_PromotesNewDefaultWhenDefaultIsDeleted()
    {
        var defaultCal = (await Db.GetCalendarsAsync()).First(c => c.IsDefault);
        await Db.DeleteCalendarAsync(defaultCal.Id);

        var remaining = await Db.GetCalendarsAsync();
        Assert.DoesNotContain(remaining, c => c.Id == defaultCal.Id);
        // Exactly one calendar should now be marked as default
        Assert.Single(remaining, c => c.IsDefault);
    }

    // ════════════════════════════════════════════════════════════════════
    // BUG: HolidayService EndTime must use consistent midnight convention
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HolidayService_EndTimeIsMidnight_NotEndOfDay()
    {
        var service = new HolidayService((year, cc) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            new[]
            {
                new HolidayService.HolidayRecord
                {
                    Date = new DateTime(2026, 12, 25),
                    LocalName = "Christmas",
                    EnglishName = "Christmas Day",
                    Types = new[] { "Public" }
                }
            }));

        var calendars = new[]
        {
            new CalendarInfo { Id = 10, Name = "US Holidays", ColorHex = "#3B82F6", IsVisible = true }
        };

        var events = await service.GetHolidayEventsAsync(
            new DateTime(2026, 12, 25), new DateTime(2026, 12, 26), calendars);

        var holiday = Assert.Single(events);
        // EndTime should be midnight of the holiday date (not 23:59:59)
        Assert.Equal(new DateTime(2026, 12, 25, 0, 0, 0), holiday.EndTime);
        Assert.Equal(TimeSpan.Zero, holiday.EndTime.TimeOfDay);
    }

    // ════════════════════════════════════════════════════════════════════
    // BUG: All-day events stored with midnight EndTime must still appear
    //       on their final day in queries.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetEventsForDateAsync_SingleDayAllDayEvent_MidnightEnd_AppearsOnDay()
    {
        // User creates a single-day all-day event:
        // StartTime = Apr 5 00:00, EndTime = Apr 5 00:00 (midnight convention)
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Conference",
            StartTime = new DateTime(2026, 4, 5, 0, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 0, 0, 0),
            IsAllDay = true,
            CalendarId = 1
        });

        var events = await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5));
        Assert.Single(events, e => e.Title == "Conference");
    }

    [Fact]
    public async Task GetEventsForDateAsync_MultiDayAllDayEvent_MidnightEnd_AppearsOnAllDays()
    {
        // User creates a 3-day all-day event: Apr 3 to Apr 5
        // StartTime = Apr 3 00:00, EndTime = Apr 5 00:00 (midnight convention)
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Team Retreat",
            StartTime = new DateTime(2026, 4, 3, 0, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 0, 0, 0),
            IsAllDay = true,
            CalendarId = 1
        });

        Assert.Single(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 3)), e => e.Title == "Team Retreat");
        Assert.Single(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 4)), e => e.Title == "Team Retreat");
        Assert.Single(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 5)), e => e.Title == "Team Retreat");
        Assert.Empty(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 2)));
        Assert.Empty(await Db.GetEventsForDateAsync(new DateTime(2026, 4, 6)));
    }

    // ════════════════════════════════════════════════════════════════════
    // BUG: AgendaViewModel LoadMore must not advance DaysToLoad when
    //       IsLoading is already true.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AgendaViewModel_LoadMore_DoesNotIncrementWhenBusy()
    {
        var vm = new AgendaViewModel { IsLoading = true };
        int beforeDays = vm.DaysToLoad;

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(beforeDays, vm.DaysToLoad);
    }

    [Fact]
    public async Task AgendaViewModel_LoadMore_IncrementsByExactly30()
    {
        var vm = new AgendaViewModel();
        Assert.Equal(30, vm.DaysToLoad);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(60, vm.DaysToLoad);
    }

    // ════════════════════════════════════════════════════════════════════
    // BUG: MonthViewModel must distribute timed events to every day
    //       they overlap, not just the start day.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MonthViewModel_TimedEventSpanningMidnight_AppearsOnBothDays()
    {
        DateTime monthDate = new DateTime(2026, 4, 1);
        DateTime eventStart = new DateTime(2026, 4, 10, 23, 0, 0);
        DateTime eventEnd = new DateTime(2026, 4, 11, 2, 0, 0);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Late Night",
            StartTime = eventStart,
            EndTime = eventEnd,
            CalendarId = 1
        });

        var vm = new MonthViewModel();
        await vm.LoadMonthCommand.ExecuteAsync(monthDate);

        // Find the cells for Apr 10 and Apr 11
        var apr10Cell = vm.DayCells.FirstOrDefault(c => c.Date == new DateTime(2026, 4, 10));
        var apr11Cell = vm.DayCells.FirstOrDefault(c => c.Date == new DateTime(2026, 4, 11));

        Assert.NotNull(apr10Cell);
        Assert.NotNull(apr11Cell);
        Assert.Contains(apr10Cell.Events, e => e.Title == "Late Night");
        Assert.Contains(apr11Cell.Events, e => e.Title == "Late Night");
    }

    // ════════════════════════════════════════════════════════════════════
    // BUG: AgendaViewModel must show timed events that started before
    //       the load window but still overlap today.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AgendaViewModel_TimedEventStartingYesterday_AppearsToday()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Overnight Shift",
            StartTime = DateTime.Today.AddDays(-1).AddHours(22),
            EndTime = DateTime.Today.AddHours(6),
            CalendarId = 1
        });

        var vm = new AgendaViewModel();
        await vm.LoadAgendaCommand.ExecuteAsync(null);

        var todayGroup = vm.AgendaGroups.FirstOrDefault(g => g.Date == DateTime.Today);
        Assert.NotNull(todayGroup);
        Assert.Contains(todayGroup.Events, e => e.Title == "Overnight Shift");
    }

    // ════════════════════════════════════════════════════════════════════
    // CalendarEventMutationHelper edge cases
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void RoundToIncrement_ClampsToMaxSameDay()
    {
        // 23:55 should round to 23:45 (the last valid 15-min slot)
        // not to 00:00 next day
        var result = CalendarEventMutationHelper.RoundToIncrement(
            new DateTime(2026, 4, 5, 23, 55, 0));

        Assert.Equal(new DateTime(2026, 4, 5), result.Date);
        Assert.Equal(23, result.Hour);
        Assert.Equal(45, result.Minute);
    }

    [Fact]
    public void MoveTimedEvent_PreservesDurationOnMove()
    {
        var evt = new CalendarEvent
        {
            Title = "Meeting",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 30, 0)
        };

        var moved = CalendarEventMutationHelper.MoveTimedEvent(evt, new DateTime(2026, 4, 5, 14, 0, 0));

        Assert.Equal(90, (moved.EndTime - moved.StartTime).TotalMinutes);
    }

    [Fact]
    public void MoveEventToDate_PreservesTimeOfDay()
    {
        var evt = new CalendarEvent
        {
            Title = "Daily Standup",
            StartTime = new DateTime(2026, 4, 5, 9, 30, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0)
        };

        var moved = CalendarEventMutationHelper.MoveEventToDate(evt, new DateTime(2026, 4, 8));

        Assert.Equal(9, moved.StartTime.Hour);
        Assert.Equal(30, moved.StartTime.Minute);
        Assert.Equal(10, moved.EndTime.Hour);
        Assert.Equal(0, moved.EndTime.Minute);
        Assert.Equal(new DateTime(2026, 4, 8), moved.StartTime.Date);
    }

    // ════════════════════════════════════════════════════════════════════
    // TimedEventSpanHelper edge cases
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void SpansMultipleDays_FalseForSameDayEvent()
    {
        Assert.False(TimedEventSpanHelper.SpansMultipleDays(
            new DateTime(2026, 4, 5, 9, 0, 0),
            new DateTime(2026, 4, 5, 17, 0, 0)));
    }

    [Fact]
    public void SpansMultipleDays_TrueForOvernightEvent()
    {
        Assert.True(TimedEventSpanHelper.SpansMultipleDays(
            new DateTime(2026, 4, 5, 23, 0, 0),
            new DateTime(2026, 4, 6, 2, 0, 0)));
    }

    [Fact]
    public void SpansMultipleDays_FalseForMidnightEndingSameDay()
    {
        // Event from 11 PM to midnight — inclusive end date is same day
        // because 00:00 next day with TimeOfDay == Zero means the event
        // ended at midnight, so inclusive end = previous day.
        Assert.False(TimedEventSpanHelper.SpansMultipleDays(
            new DateTime(2026, 4, 5, 23, 0, 0),
            new DateTime(2026, 4, 6, 0, 0, 0)));
    }

    [Fact]
    public void GetInclusiveEndDate_MidnightEndReturnsDay1()
    {
        // If event ends at exactly midnight the next day, the inclusive end
        // date is the previous day (the event didn't actually occupy any
        // time on the next day).
        var result = TimedEventSpanHelper.GetInclusiveEndDate(
            new DateTime(2026, 4, 5, 9, 0, 0),
            new DateTime(2026, 4, 6, 0, 0, 0));

        Assert.Equal(new DateTime(2026, 4, 5), result);
    }

    // ════════════════════════════════════════════════════════════════════
    // DateTimeHelper edge cases
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(DayOfWeek.Sunday, "2026-04-05", "2026-04-05")]  // Sunday on a Sunday
    [InlineData(DayOfWeek.Sunday, "2026-04-06", "2026-04-05")]  // Monday → prev Sunday
    [InlineData(DayOfWeek.Monday, "2026-04-06", "2026-04-06")]  // Monday on a Monday
    [InlineData(DayOfWeek.Monday, "2026-04-05", "2026-03-30")]  // Sunday → prev Monday
    [InlineData(DayOfWeek.Saturday, "2026-04-05", "2026-04-04")] // Sunday → prev Saturday
    public void GetWeekStart_ReturnsCorrectDate(DayOfWeek firstDay, string inputDate, string expectedDate)
    {
        var input = DateTime.Parse(inputDate);
        var expected = DateTime.Parse(expectedDate);

        Assert.Equal(expected, DateTimeHelper.GetWeekStart(input, firstDay));
    }

    [Fact]
    public void GetHourLabels_Returns24Labels()
    {
        var labels = DateTimeHelper.GetHourLabels();
        Assert.Equal(24, labels.Count);
        Assert.Equal(DateTime.Today.ToString("t"), labels[0]);
        Assert.Equal(DateTime.Today.AddHours(12).ToString("t"), labels[12]);
        Assert.Equal(DateTime.Today.AddHours(23).ToString("t"), labels[23]);
    }

    // ════════════════════════════════════════════════════════════════════
    // SettingsViewModel settings roundtrip
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    public async Task SettingsViewModel_FirstDayOfWeek_RoundTrips(int dayValue)
    {
        await Db.SetSettingAsync("FirstDayOfWeek", dayValue.ToString());

        var vm = new SettingsViewModel();
        await vm.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(dayValue, vm.FirstDayOfWeekIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SettingsViewModel_Theme_RoundTrips(int themeIndex)
    {
        await Db.SetSettingAsync("Theme", themeIndex.ToString());

        var vm = new SettingsViewModel();
        await vm.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(themeIndex, vm.SelectedThemeIndex);
    }
}
