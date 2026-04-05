using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;
using NativeCal.Services;
using NativeCal.ViewModels;

namespace NativeCal.Tests.ViewModels;

public class MonthViewModelTests : TestBase
{
    [Fact]
    public async Task LoadMonthCommand_ShowsTimedEventOnEveryVisibleDayItOverlaps()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Release window",
            StartTime = new DateTime(2026, 3, 28, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 1, 2, 0, 0),
            CalendarId = 1
        });

        var viewModel = new MonthViewModel();

        await viewModel.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        Assert.Contains(GetCell(viewModel, new DateTime(2026, 3, 29)).Events, e => e.Title == "Release window");
        Assert.Contains(GetCell(viewModel, new DateTime(2026, 3, 30)).Events, e => e.Title == "Release window");
        Assert.Contains(GetCell(viewModel, new DateTime(2026, 3, 31)).Events, e => e.Title == "Release window");
        Assert.Contains(GetCell(viewModel, new DateTime(2026, 4, 1)).Events, e => e.Title == "Release window");
        Assert.DoesNotContain(GetCell(viewModel, new DateTime(2026, 4, 2)).Events, e => e.Title == "Release window");
    }

    [Fact]
    public async Task LoadMonthCommand_DoesNotShowEventsFromHiddenCalendars()
    {
        var calendars = await Db.GetCalendarsAsync();
        var hiddenCalendar = Assert.Single(calendars.Where(c => !c.IsDefault).Take(1));
        hiddenCalendar.IsVisible = false;
        await Db.SaveCalendarAsync(hiddenCalendar);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Should stay hidden",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = hiddenCalendar.Id
        });

        var viewModel = new MonthViewModel();

        await viewModel.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        Assert.DoesNotContain(GetCell(viewModel, new DateTime(2026, 4, 5)).Events, e => e.Title == "Should stay hidden");
    }

    [Fact]
    public async Task LoadMonthCommand_IncludesVisibleHolidayEventInMatchingCell()
    {
        App.HolidayService = new HolidayService((_, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            countryCode == "CA"
                ? new[]
                {
                    new HolidayService.HolidayRecord
                    {
                        Date = new DateTime(2026, 7, 1),
                        LocalName = "Canada Day",
                        EnglishName = "Canada Day",
                        Types = new[] { "Public" }
                    }
                }
                : Array.Empty<HolidayService.HolidayRecord>()));

        var viewModel = new MonthViewModel();

        await viewModel.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 7, 1));

        var holiday = Assert.Single(GetCell(viewModel, new DateTime(2026, 7, 1)).Events, e => e.Title == "Canada Day");
        Assert.True(holiday.IsReadOnly);
        Assert.True(holiday.IsOfficialHoliday);
    }

    [Fact]
    public async Task LoadMonthCommand_OrdersAllDayBeforeTimedEventsThenByStartTime()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Late timed",
            StartTime = new DateTime(2026, 4, 5, 15, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 16, 0, 0),
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "All day",
            StartTime = new DateTime(2026, 4, 5),
            EndTime = new DateTime(2026, 4, 5, 23, 59, 59),
            IsAllDay = true,
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Early timed",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });

        var viewModel = new MonthViewModel();

        await viewModel.LoadMonthCommand.ExecuteAsync(new DateTime(2026, 4, 1));

        var titles = GetCell(viewModel, new DateTime(2026, 4, 5)).Events.Select(e => e.Title).ToArray();

        Assert.Equal(new[] { "All day", "Early timed", "Late timed" }, titles);
    }

    private static MonthViewModel.DayCell GetCell(MonthViewModel viewModel, DateTime date)
    {
        return Assert.Single(viewModel.DayCells, c => c.Date == date.Date);
    }
}
