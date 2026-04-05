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

public class DayWeekViewModelTests : TestBase
{
    [Fact]
    public void DayViewModel_Constructor_InitializesTodayStateAndHourLabels()
    {
        var today = DateTime.Today;
        var viewModel = new DayViewModel();

        Assert.Equal(today, viewModel.CurrentDate);
        Assert.Equal(today.ToString("dddd, MMMM d, yyyy"), viewModel.DayTitle);
        Assert.True(viewModel.IsToday);
        Assert.Equal(24, viewModel.HourLabels.Count);
    }

    [Fact]
    public async Task NextDayCommand_LoadsNextDayEventsAndClearsTodayFlag()
    {
        var today = DateTime.Today;
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Tomorrow event",
            StartTime = today.AddDays(1).AddHours(9),
            EndTime = today.AddDays(1).AddHours(10),
            CalendarId = 1
        });

        var viewModel = new DayViewModel();

        await viewModel.NextDayCommand.ExecuteAsync(null);

        Assert.Equal(today.AddDays(1), viewModel.CurrentDate);
        Assert.False(viewModel.IsToday);
        Assert.Contains(viewModel.Events, e => e.Title == "Tomorrow event");
    }

    [Fact]
    public async Task PreviousDayCommand_LoadsPreviousDayEventsAndClearsTodayFlag()
    {
        var today = DateTime.Today;
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Yesterday event",
            StartTime = today.AddDays(-1).AddHours(9),
            EndTime = today.AddDays(-1).AddHours(10),
            CalendarId = 1
        });

        var viewModel = new DayViewModel();

        await viewModel.PreviousDayCommand.ExecuteAsync(null);

        Assert.Equal(today.AddDays(-1), viewModel.CurrentDate);
        Assert.False(viewModel.IsToday);
        Assert.Contains(viewModel.Events, e => e.Title == "Yesterday event");
    }

    [Fact]
    public async Task GoToTodayCommand_ReturnsToTodayAndLoadsTodayEvents()
    {
        var today = DateTime.Today;
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Today event",
            StartTime = today.AddHours(11),
            EndTime = today.AddHours(12),
            CalendarId = 1
        });

        var viewModel = new DayViewModel
        {
            CurrentDate = today.AddDays(5)
        };

        await viewModel.GoToTodayCommand.ExecuteAsync(null);

        Assert.Equal(today, viewModel.CurrentDate);
        Assert.True(viewModel.IsToday);
        Assert.Contains(viewModel.Events, e => e.Title == "Today event");
    }

    [Fact]
    public async Task LoadDayCommand_SplitsTimedAndAllDayEventsAndOrdersTimedEvents()
    {
        var date = new DateTime(2026, 4, 6);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Late sync",
            StartTime = date.AddHours(15),
            EndTime = date.AddHours(16),
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "All day focus",
            StartTime = date,
            EndTime = date.AddHours(23).AddMinutes(59),
            IsAllDay = true,
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Early sync",
            StartTime = date.AddHours(9),
            EndTime = date.AddHours(10),
            CalendarId = 1
        });

        var viewModel = new DayViewModel();

        await viewModel.LoadDayCommand.ExecuteAsync(date);

        Assert.Equal(date, viewModel.CurrentDate);
        Assert.Equal(new[] { "Early sync", "Late sync" }, viewModel.Events.Select(e => e.Title).ToArray());
        Assert.Equal(new[] { "All day focus" }, viewModel.AllDayEvents.Select(e => e.Title).ToArray());
    }

    [Fact]
    public async Task LoadDayCommand_IncludesVisibleHolidayEventsAsReadOnlyAllDayEvents()
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

        var viewModel = new DayViewModel();

        await viewModel.LoadDayCommand.ExecuteAsync(new DateTime(2026, 7, 1));

        var holiday = Assert.Single(viewModel.AllDayEvents, e => e.Title == "Canada Day");
        Assert.True(holiday.IsReadOnly);
        Assert.True(holiday.IsOfficialHoliday);
    }

    [Fact]
    public async Task LoadDayCommand_HidesHolidayEventsFromHiddenHolidayCalendars()
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

        var viewModel = new DayViewModel();

        await viewModel.LoadDayCommand.ExecuteAsync(new DateTime(2026, 7, 1));

        Assert.DoesNotContain(viewModel.AllDayEvents, e => e.Title == "Canada Day");
    }

    [Fact]
    public void WeekViewModel_Constructor_InitializesCurrentWeekStateAndHourLabels()
    {
        var expectedWeekStart = DateTimeHelper.GetWeekStart(DateTime.Today);
        var viewModel = new WeekViewModel();

        Assert.Equal(expectedWeekStart, viewModel.WeekStart);
        Assert.Equal(24, viewModel.HourLabels.Count);
        Assert.Empty(viewModel.DayColumns);
    }

    [Fact]
    public async Task NextWeekCommand_LoadsNextWeekEventsAndUpdatesWeekStart()
    {
        var currentWeekStart = DateTimeHelper.GetWeekStart(DateTime.Today);
        var nextWeekDate = currentWeekStart.AddDays(7);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Next week planning",
            StartTime = nextWeekDate.AddHours(9),
            EndTime = nextWeekDate.AddHours(10),
            CalendarId = 1
        });

        var viewModel = new WeekViewModel();

        await viewModel.NextWeekCommand.ExecuteAsync(null);

        Assert.Equal(nextWeekDate, viewModel.WeekStart);
        var firstDay = Assert.Single(viewModel.DayColumns, c => c.Date == nextWeekDate);
        Assert.Contains(firstDay.Events, e => e.Title == "Next week planning");
    }

    [Fact]
    public async Task PreviousWeekCommand_LoadsPreviousWeekEventsAndUpdatesWeekStart()
    {
        var currentWeekStart = DateTimeHelper.GetWeekStart(DateTime.Today);
        var previousWeekDate = currentWeekStart.AddDays(-7);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Previous week review",
            StartTime = previousWeekDate.AddHours(14),
            EndTime = previousWeekDate.AddHours(15),
            CalendarId = 1
        });

        var viewModel = new WeekViewModel();

        await viewModel.PreviousWeekCommand.ExecuteAsync(null);

        Assert.Equal(previousWeekDate, viewModel.WeekStart);
        var firstDay = Assert.Single(viewModel.DayColumns, c => c.Date == previousWeekDate);
        Assert.Contains(firstDay.Events, e => e.Title == "Previous week review");
    }

    [Fact]
    public async Task GoToTodayCommand_ReturnsToCurrentWeekAndLoadsTodayWeekEvents()
    {
        var todayWeekStart = DateTimeHelper.GetWeekStart(DateTime.Today);
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "This week event",
            StartTime = todayWeekStart.AddDays(2).AddHours(9),
            EndTime = todayWeekStart.AddDays(2).AddHours(10),
            CalendarId = 1
        });

        var viewModel = new WeekViewModel
        {
            WeekStart = todayWeekStart.AddDays(14)
        };

        await viewModel.GoToTodayCommand.ExecuteAsync(null);

        Assert.Equal(todayWeekStart, viewModel.WeekStart);
        Assert.Contains(viewModel.DayColumns.SelectMany(c => c.Events), e => e.Title == "This week event");
    }

    [Fact]
    public async Task LoadWeekCommand_PutsOvernightTimedEventsOnBothDaysAndAllDayEventsAcrossSpan()
    {
        var weekDate = new DateTime(2026, 4, 6);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Maintenance",
            StartTime = new DateTime(2026, 4, 6, 23, 0, 0),
            EndTime = new DateTime(2026, 4, 7, 1, 0, 0),
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Conference",
            StartTime = new DateTime(2026, 4, 6),
            EndTime = new DateTime(2026, 4, 8, 23, 59, 59),
            IsAllDay = true,
            CalendarId = 1
        });

        var viewModel = new WeekViewModel();

        await viewModel.LoadWeekCommand.ExecuteAsync(weekDate);

        var monday = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 4, 6));
        var tuesday = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 4, 7));
        var wednesday = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 4, 8));

        Assert.Contains(monday.Events, e => e.Title == "Maintenance");
        Assert.Contains(tuesday.Events, e => e.Title == "Maintenance");
        Assert.Single(monday.Events, e => e.Title == "Maintenance");
        Assert.Single(tuesday.Events, e => e.Title == "Maintenance");
        Assert.Contains(monday.AllDayEvents, e => e.Title == "Conference");
        Assert.Contains(tuesday.AllDayEvents, e => e.Title == "Conference");
        Assert.Contains(wednesday.AllDayEvents, e => e.Title == "Conference");
    }

    [Fact]
    public async Task LoadWeekCommand_ReflectsUpdatedMultiDayTimedEventSpanAndTimeDisplayAfterReload()
    {
        var evt = new CalendarEvent
        {
            Title = "Migration",
            StartTime = new DateTime(2026, 4, 6, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 7, 1, 0, 0),
            CalendarId = 1
        };

        await Db.SaveEventAsync(evt);

        var viewModel = new WeekViewModel();
        await viewModel.LoadWeekCommand.ExecuteAsync(new DateTime(2026, 4, 6));

        evt.EndTime = new DateTime(2026, 4, 8, 3, 30, 0);
        await Db.SaveEventAsync(evt);

        await viewModel.LoadWeekCommand.ExecuteAsync(new DateTime(2026, 4, 6));

        var monday = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 4, 6));
        var tuesday = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 4, 7));
        var wednesday = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 4, 8));

        Assert.Contains(monday.Events, e => e.Title == "Migration" && e.TimeDisplay == "10:00 PM - 3:30 AM");
        Assert.Contains(tuesday.Events, e => e.Title == "Migration" && e.TimeDisplay == "10:00 PM - 3:30 AM");
        Assert.Contains(wednesday.Events, e => e.Title == "Migration" && e.TimeDisplay == "10:00 PM - 3:30 AM");
    }

    [Fact]
    public async Task LoadWeekCommand_OrdersTimedEventsByStartTimeWithinEachDay()
    {
        var weekDate = new DateTime(2026, 4, 6);

        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Late sync",
            StartTime = weekDate.AddHours(15),
            EndTime = weekDate.AddHours(16),
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Early sync",
            StartTime = weekDate.AddHours(9),
            EndTime = weekDate.AddHours(10),
            CalendarId = 1
        });

        var viewModel = new WeekViewModel();

        await viewModel.LoadWeekCommand.ExecuteAsync(weekDate);

        var monday = Assert.Single(viewModel.DayColumns, c => c.Date == weekDate);

        Assert.Equal(new[] { "Early sync", "Late sync" }, monday.Events.Select(e => e.Title).ToArray());
    }

    [Fact]
    public async Task LoadWeekCommand_IncludesVisibleHolidayAsReadOnlyAllDayEvent()
    {
        App.HolidayService = new HolidayService((_, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            countryCode == "US"
                ? new[]
                {
                    new HolidayService.HolidayRecord
                    {
                        Date = new DateTime(2026, 7, 4),
                        LocalName = "Independence Day",
                        EnglishName = "Independence Day",
                        Types = new[] { "Public" }
                    }
                }
                : Array.Empty<HolidayService.HolidayRecord>()));

        var viewModel = new WeekViewModel();

        await viewModel.LoadWeekCommand.ExecuteAsync(new DateTime(2026, 7, 4));

        var day = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 7, 4));
        var holiday = Assert.Single(day.AllDayEvents, e => e.Title == "Independence Day");
        Assert.True(holiday.IsReadOnly);
        Assert.True(holiday.IsOfficialHoliday);
    }

    [Fact]
    public async Task LoadWeekCommand_HidesHolidayEventsFromHiddenHolidayCalendars()
    {
        var calendars = await Db.GetCalendarsAsync();
        var holidayCalendar = Assert.Single(calendars, c => c.Name == "US Holidays");
        holidayCalendar.IsVisible = false;
        await Db.SaveCalendarAsync(holidayCalendar);

        App.HolidayService = new HolidayService((_, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            countryCode == "US"
                ? new[]
                {
                    new HolidayService.HolidayRecord
                    {
                        Date = new DateTime(2026, 7, 4),
                        LocalName = "Independence Day",
                        EnglishName = "Independence Day",
                        Types = new[] { "Public" }
                    }
                }
                : Array.Empty<HolidayService.HolidayRecord>()));

        var viewModel = new WeekViewModel();

        await viewModel.LoadWeekCommand.ExecuteAsync(new DateTime(2026, 7, 4));

        var day = Assert.Single(viewModel.DayColumns, c => c.Date == new DateTime(2026, 7, 4));
        Assert.DoesNotContain(day.AllDayEvents, e => e.Title == "Independence Day");
    }

    [Fact]
    public void DayColumn_DateChange_RaisesDerivedPropertyNotifications()
    {
        var viewModel = new WeekViewModel.DayColumn();
        var changes = CaptureChanges(viewModel);

        viewModel.Date = DateTime.Today.AddDays(1);

        Assert.Contains(nameof(WeekViewModel.DayColumn.DayName), changes);
        Assert.Contains(nameof(WeekViewModel.DayColumn.DayNumber), changes);
        Assert.Contains(nameof(WeekViewModel.DayColumn.IsToday), changes);
    }

    private static HashSet<string?> CaptureChanges(INotifyPropertyChanged source)
    {
        var changes = new HashSet<string?>();
        source.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        return changes;
    }
}
