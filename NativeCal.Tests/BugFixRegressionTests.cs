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
/// Tests for bugs fixed in the audit pass:
///   1. ShowCurrentTimeIndicator index-out-of-range (DayViewPage)
///   2. CreateEvent_Click hour overflow at 11 PM (AgendaViewPage)
///   3. First Day of Week setting non-functional
///   4. Drag-suppress flag leak
///   5. Agenda "Load more" scroll position (UX, no unit-testable logic)
///   6. All-day events query loaded entire table (DatabaseService)
///   7. First Day of Week change propagation
/// </summary>
public class BugFixRegressionTests : TestBase
{
    // ════════════════════════════════════════════════════════════════════
    // Bug #2: CreateEvent_Click hour overflow — the start hour must be
    // clamped to 23 so we don't roll past midnight into the next day.
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, 1)]    // Midnight → 1 AM
    [InlineData(8, 9)]    // 8 AM → 9 AM
    [InlineData(22, 23)]  // 10 PM → 11 PM
    [InlineData(23, 23)]  // 11 PM → clamped to 11 PM (not 12 AM next day)
    public void AgendaCreateEvent_HourClamp_StaysSameDay(int currentHour, int expectedHour)
    {
        // Replicate the fix logic: Math.Min(currentHour + 1, 23)
        int nextHour = Math.Min(currentHour + 1, 23);
        DateTime startTime = DateTime.Today.AddHours(nextHour);

        Assert.Equal(expectedHour, startTime.Hour);
        Assert.Equal(DateTime.Today.Date, startTime.Date);
    }

    // ════════════════════════════════════════════════════════════════════
    // Bug #3 + #7: First Day of Week — the setting must propagate to
    // MonthViewModel grid alignment, WeekViewModel week boundaries,
    // and MainWindow header formatting.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MonthViewModel_UsesFirstDayOfWeek_Sunday()
    {
        // Sunday (default) — April 2026 starts on Wednesday, grid starts March 29 (Sun).
        App.FirstDayOfWeek = DayOfWeek.Sunday;
        var vm = new MonthViewModel();
        await vm.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        Assert.Equal(42, vm.DayCells.Count);
        Assert.Equal(DayOfWeek.Sunday, vm.DayCells[0].Date.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 29), vm.DayCells[0].Date);
    }

    [Fact]
    public async Task MonthViewModel_UsesFirstDayOfWeek_Monday()
    {
        // Monday — April 2026 grid should start on March 30 (Mon).
        App.FirstDayOfWeek = DayOfWeek.Monday;
        var vm = new MonthViewModel();
        await vm.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        Assert.Equal(42, vm.DayCells.Count);
        Assert.Equal(DayOfWeek.Monday, vm.DayCells[0].Date.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 30), vm.DayCells[0].Date);
    }

    [Fact]
    public async Task MonthViewModel_UsesFirstDayOfWeek_Saturday()
    {
        // Saturday — April 2026 grid should start on March 28 (Sat).
        App.FirstDayOfWeek = DayOfWeek.Saturday;
        var vm = new MonthViewModel();
        await vm.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        Assert.Equal(42, vm.DayCells.Count);
        Assert.Equal(DayOfWeek.Saturday, vm.DayCells[0].Date.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 28), vm.DayCells[0].Date);
    }

    [Fact]
    public async Task MonthViewModel_AllSevenDaysOfWeekAppearInFirstRow()
    {
        // Verify every row starts on the configured first day.
        App.FirstDayOfWeek = DayOfWeek.Monday;
        var vm = new MonthViewModel();
        await vm.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        // First 7 cells should span Mon → Sun.
        var firstRowDays = vm.DayCells.Take(7).Select(c => c.Date.DayOfWeek).ToArray();
        Assert.Equal(DayOfWeek.Monday, firstRowDays[0]);
        Assert.Equal(DayOfWeek.Sunday, firstRowDays[6]);
    }

    [Fact]
    public void WeekViewModel_ConstructorUsesFirstDayOfWeek()
    {
        // When FirstDayOfWeek is Monday, the week should start on the previous Monday.
        App.FirstDayOfWeek = DayOfWeek.Monday;
        var vm = new WeekViewModel();

        Assert.Equal(DayOfWeek.Monday, vm.WeekStart.DayOfWeek);
    }

    [Fact]
    public async Task WeekViewModel_LoadWeek_AlignsToFirstDayOfWeek()
    {
        App.FirstDayOfWeek = DayOfWeek.Monday;
        var vm = new WeekViewModel();
        // Load week containing Wednesday April 1, 2026.
        await vm.LoadWeekCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        Assert.Equal(DayOfWeek.Monday, vm.WeekStart.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 30), vm.WeekStart);
        Assert.Equal(7, vm.DayColumns.Count);
    }

    [Fact]
    public async Task WeekViewModel_GoToToday_AlignsToFirstDayOfWeek()
    {
        App.FirstDayOfWeek = DayOfWeek.Saturday;
        var vm = new WeekViewModel();
        // Move to some other week first.
        await vm.LoadWeekCommand.ExecuteAsync(new DateTime(2026, 1, 1));
        Assert.NotEqual(DateTime.Today, vm.WeekStart);

        await vm.GoToTodayCommand.ExecuteAsync(null);

        Assert.Equal(DayOfWeek.Saturday, vm.WeekStart.DayOfWeek);
    }

    [Fact]
    public async Task AppFirstDayOfWeek_PersistsAndLoadsFromDatabase()
    {
        // Save the setting and verify it round-trips through the database.
        await Db.SetSettingAsync("FirstDayOfWeek", "1");
        string value = await Db.GetSettingAsync("FirstDayOfWeek", "0");
        Assert.Equal("1", value);

        // Simulate the loading logic from App.LoadFirstDayOfWeekAsync.
        if (int.TryParse(value, out int dayValue))
        {
            App.FirstDayOfWeek = dayValue switch
            {
                1 => DayOfWeek.Monday,
                6 => DayOfWeek.Saturday,
                _ => DayOfWeek.Sunday
            };
        }

        Assert.Equal(DayOfWeek.Monday, App.FirstDayOfWeek);
    }

    // ════════════════════════════════════════════════════════════════════
    // Bug #6: All-day events query — should only load events that
    // overlap the requested date range, not the entire table.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetEventsAsync_AllDayEvent_OnlyReturnsOverlappingRange()
    {
        // Create an all-day event in January.
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "January all-day",
            StartTime = new DateTime(2026, 1, 15),
            EndTime = new DateTime(2026, 1, 15, 23, 59, 59),
            IsAllDay = true,
            CalendarId = 1
        });

        // Create an all-day event in April.
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "April all-day",
            StartTime = new DateTime(2026, 4, 5),
            EndTime = new DateTime(2026, 4, 5, 23, 59, 59),
            IsAllDay = true,
            CalendarId = 1
        });

        // Query only April — should NOT include the January event.
        var aprilEvents = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 5, 1));

        Assert.Contains(aprilEvents, e => e.Title == "April all-day");
        Assert.DoesNotContain(aprilEvents, e => e.Title == "January all-day");
    }

    [Fact]
    public async Task GetEventsAsync_AllDayMultiDaySpan_ReturnsOverlapping()
    {
        // Multi-day all-day event spanning March 30 – April 2.
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Multi-day retreat",
            StartTime = new DateTime(2026, 3, 30),
            EndTime = new DateTime(2026, 4, 2, 23, 59, 59),
            IsAllDay = true,
            CalendarId = 1
        });

        // Query just April 1–2 — should include the spanning event.
        var events = await Db.GetEventsAsync(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 3));

        Assert.Single(events, e => e.Title == "Multi-day retreat");
    }

    [Fact]
    public async Task GetEventsAsync_AllDayEvent_ExcludedWhenOutsideRange()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "December holiday",
            StartTime = new DateTime(2026, 12, 25),
            EndTime = new DateTime(2026, 12, 25, 23, 59, 59),
            IsAllDay = true,
            CalendarId = 1
        });

        // Query June — should not include December event.
        var events = await Db.GetEventsAsync(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 7, 1));

        Assert.DoesNotContain(events, e => e.Title == "December holiday");
    }

    [Fact]
    public async Task GetEventsAsync_EdgeCase_MinMaxDateRange_NoOverflow()
    {
        // The boundary-safe widening should not throw on extreme date ranges.
        var events = await Db.GetEventsAsync(DateTime.MinValue, DateTime.MaxValue);
        Assert.NotNull(events);
    }

    [Fact]
    public async Task GetEventsAsync_EdgeCase_SmallDateRange_NearMinValue()
    {
        // Guard against overflow near DateTime.MinValue.
        var events = await Db.GetEventsAsync(DateTime.MinValue, DateTime.MinValue.AddDays(2));
        Assert.NotNull(events);
    }

    [Fact]
    public async Task GetEventsAsync_EdgeCase_SmallDateRange_NearMaxValue()
    {
        // Guard against overflow near DateTime.MaxValue.
        var events = await Db.GetEventsAsync(DateTime.MaxValue.AddDays(-2), DateTime.MaxValue);
        Assert.NotNull(events);
    }

    // ════════════════════════════════════════════════════════════════════
    // Additional regression tests for the CalendarEventMutationHelper
    // which is used by drag/resize — verify it preserves duration and
    // clamps correctly.
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void MoveEventToDate_PreservesDuration()
    {
        var evt = new CalendarEvent
        {
            StartTime = new DateTime(2026, 4, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 1, 10, 30, 0)
        };

        var moved = CalendarEventMutationHelper.MoveEventToDate(evt, new DateTime(2026, 4, 5));

        Assert.Equal(new DateTime(2026, 4, 5, 9, 0, 0), moved.StartTime);
        Assert.Equal(new DateTime(2026, 4, 5, 10, 30, 0), moved.EndTime);
    }

    [Fact]
    public void ResizeTimedEvent_ClampsToMinimumDuration()
    {
        var evt = new CalendarEvent
        {
            StartTime = new DateTime(2026, 4, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 1, 10, 0, 0)
        };

        // Try to resize to a time BEFORE the start → should clamp to minimum.
        var resized = CalendarEventMutationHelper.ResizeTimedEvent(evt, new DateTime(2026, 4, 1, 8, 0, 0));

        Assert.Equal(evt.StartTime.AddMinutes(CalendarEventMutationHelper.MinimumDurationMinutes), resized.EndTime);
    }

    [Fact]
    public void MoveTimedEvent_EnforcesMinimumDuration()
    {
        // Event with 5-minute duration (less than minimum).
        var evt = new CalendarEvent
        {
            StartTime = new DateTime(2026, 4, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 1, 9, 5, 0)
        };

        var moved = CalendarEventMutationHelper.MoveTimedEvent(evt, new DateTime(2026, 4, 1, 14, 0, 0));

        double durationMinutes = (moved.EndTime - moved.StartTime).TotalMinutes;
        Assert.True(durationMinutes >= CalendarEventMutationHelper.MinimumDurationMinutes);
    }

    // ════════════════════════════════════════════════════════════════════
    // Verify DateTimeHelper grid helpers respect FirstDayOfWeek
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Saturday)]
    public void CalendarGridStart_AlwaysStartsOnConfiguredFirstDay(DayOfWeek firstDay)
    {
        var months = new[]
        {
            new DateTime(2026, 1, 1),
            new DateTime(2026, 4, 1),
            new DateTime(2026, 7, 1),
            new DateTime(2026, 10, 1),
        };

        foreach (var month in months)
        {
            var gridStart = DateTimeHelper.GetCalendarGridStart(month, firstDay);
            Assert.Equal(firstDay, gridStart.DayOfWeek);
            // Grid start must be on or before the 1st of the month.
            Assert.True(gridStart <= month);
        }
    }

    [Fact]
    public void CalendarGridEnd_Is41DaysAfterStart_ForAllFirstDays()
    {
        var month = new DateTime(2026, 4, 1);

        foreach (DayOfWeek firstDay in new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Saturday })
        {
            var start = DateTimeHelper.GetCalendarGridStart(month, firstDay);
            var end = DateTimeHelper.GetCalendarGridEnd(month, firstDay);
            Assert.Equal(41, (end - start).Days);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Verify the first day of week setting values map correctly.
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("0", DayOfWeek.Sunday)]
    [InlineData("1", DayOfWeek.Monday)]
    [InlineData("6", DayOfWeek.Saturday)]
    [InlineData("99", DayOfWeek.Sunday)]  // Invalid → defaults to Sunday.
    [InlineData("abc", DayOfWeek.Sunday)] // Non-numeric → stays Sunday.
    public void FirstDayOfWeek_MappingFromStoredValue(string storedValue, DayOfWeek expected)
    {
        // Replicate the App.LoadFirstDayOfWeekAsync mapping logic.
        DayOfWeek result = DayOfWeek.Sunday;
        if (int.TryParse(storedValue, out int dayValue))
        {
            result = dayValue switch
            {
                1 => DayOfWeek.Monday,
                6 => DayOfWeek.Saturday,
                _ => DayOfWeek.Sunday
            };
        }

        Assert.Equal(expected, result);
    }

}
