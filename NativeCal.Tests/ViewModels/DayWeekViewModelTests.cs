using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;
using NativeCal.Services;
using NativeCal.ViewModels;

namespace NativeCal.Tests.ViewModels;

public class DayWeekViewModelTests : TestBase
{
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
}
