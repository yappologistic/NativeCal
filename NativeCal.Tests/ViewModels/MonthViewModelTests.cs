using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.Services;
using NativeCal.ViewModels;

namespace NativeCal.Tests.ViewModels;

public class MonthViewModelTests : TestBase
{
    [Fact]
    public void Constructor_InitializesCurrentMonthAndTitle()
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var viewModel = new MonthViewModel();

        Assert.Equal(currentMonth, viewModel.CurrentMonth);
        Assert.Equal(currentMonth.ToString("MMMM yyyy"), viewModel.MonthYearTitle);
        Assert.Empty(viewModel.DayCells);
    }

    [Fact]
    public async Task NextMonthCommand_LoadsNextMonthEventsAndUpdatesCurrentMonth()
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Next month event",
            StartTime = nextMonth.AddDays(4).AddHours(9),
            EndTime = nextMonth.AddDays(4).AddHours(10),
            CalendarId = 1
        });

        var viewModel = new MonthViewModel();

        await viewModel.NextMonthCommand.ExecuteAsync(null);

        Assert.Equal(nextMonth, viewModel.CurrentMonth);
        Assert.Contains(GetCell(viewModel, nextMonth.AddDays(4)).Events, e => e.Title == "Next month event");
    }

    [Fact]
    public async Task PreviousMonthCommand_LoadsPreviousMonthEventsAndUpdatesCurrentMonth()
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var previousMonth = currentMonth.AddMonths(-1);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Previous month event",
            StartTime = previousMonth.AddDays(9).AddHours(9),
            EndTime = previousMonth.AddDays(9).AddHours(10),
            CalendarId = 1
        });

        var viewModel = new MonthViewModel();

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal(previousMonth, viewModel.CurrentMonth);
        Assert.Contains(GetCell(viewModel, previousMonth.AddDays(9)).Events, e => e.Title == "Previous month event");
    }

    [Fact]
    public async Task GoToTodayCommand_ReturnsToCurrentMonthAndLoadsCurrentMonthEvents()
    {
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Current month event",
            StartTime = currentMonth.AddDays(2).AddHours(9),
            EndTime = currentMonth.AddDays(2).AddHours(10),
            CalendarId = 1
        });

        var viewModel = new MonthViewModel
        {
            CurrentMonth = currentMonth.AddMonths(2)
        };

        await viewModel.GoToTodayCommand.ExecuteAsync(null);

        Assert.Equal(currentMonth, viewModel.CurrentMonth);
        Assert.Contains(GetCell(viewModel, currentMonth.AddDays(2)).Events, e => e.Title == "Current month event");
    }

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
    public async Task LoadMonthCommand_HidesHolidayEventsFromHiddenHolidayCalendars()
    {
        var calendars = await Db.GetCalendarsAsync();
        var canadaCalendar = Assert.Single(calendars, c => c.Name == "Canada Holidays");
        canadaCalendar.IsVisible = false;
        await Db.SaveCalendarAsync(canadaCalendar);

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

        Assert.DoesNotContain(GetCell(viewModel, new DateTime(2026, 7, 1)).Events, e => e.Title == "Canada Day");
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

    [Fact]
    public void DayCell_DateChange_RaisesDerivedPropertyNotifications()
    {
        var cell = new MonthViewModel.DayCell();
        var changes = CaptureChanges(cell);

        cell.Date = DateTime.Today.AddDays(1);

        Assert.Contains(nameof(MonthViewModel.DayCell.DayNumber), changes);
        Assert.Contains(nameof(MonthViewModel.DayCell.IsToday), changes);
    }

    private static MonthViewModel.DayCell GetCell(MonthViewModel viewModel, DateTime date)
    {
        return Assert.Single(viewModel.DayCells, c => c.Date == date.Date);
    }

    private static HashSet<string?> CaptureChanges(INotifyPropertyChanged source)
    {
        var changes = new HashSet<string?>();
        source.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        return changes;
    }
}
