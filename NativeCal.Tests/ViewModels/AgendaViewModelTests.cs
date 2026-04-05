using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;
using NativeCal.Services;
using NativeCal.ViewModels;

namespace NativeCal.Tests.ViewModels;

public class AgendaViewModelTests : TestBase
{
    [Fact]
    public async Task LoadAgendaCommand_IncludesTimedEventsThatStartedBeforeTodayWhenTheyStillOverlapToday()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Overnight deploy",
            StartTime = DateTime.Today.AddDays(-1).AddHours(23),
            EndTime = DateTime.Today.AddHours(2),
            CalendarId = 1
        });

        var viewModel = new AgendaViewModel();

        await viewModel.LoadAgendaCommand.ExecuteAsync(null);

        var todayGroup = Assert.Single(viewModel.AgendaGroups, g => g.Date == DateTime.Today);
        Assert.Contains(todayGroup.Events, e => e.Title == "Overnight deploy");
        Assert.False(viewModel.HasNoEvents);
    }

    [Fact]
    public async Task LoadAgendaCommand_ShowsTimedMultiDayEventsOnEachOverlappingAgendaDay()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Incident bridge",
            StartTime = DateTime.Today.AddHours(22),
            EndTime = DateTime.Today.AddDays(1).AddHours(1),
            CalendarId = 1
        });

        var viewModel = new AgendaViewModel();

        await viewModel.LoadAgendaCommand.ExecuteAsync(null);

        var todayGroup = Assert.Single(viewModel.AgendaGroups, g => g.Date == DateTime.Today);
        var tomorrowGroup = Assert.Single(viewModel.AgendaGroups, g => g.Date == DateTime.Today.AddDays(1));

        Assert.Contains(todayGroup.Events, e => e.Title == "Incident bridge");
        Assert.Contains(tomorrowGroup.Events, e => e.Title == "Incident bridge");
    }

    [Fact]
    public async Task LoadAgendaCommand_IncludesLegacyAllDayEventOnItsFinalDay()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Festival",
            StartTime = DateTime.Today.AddDays(-2),
            EndTime = DateTime.Today,
            IsAllDay = true,
            CalendarId = 1
        });

        var viewModel = new AgendaViewModel();

        await viewModel.LoadAgendaCommand.ExecuteAsync(null);

        var todayGroup = Assert.Single(viewModel.AgendaGroups, g => g.Date == DateTime.Today);
        Assert.Contains(todayGroup.Events, e => e.Title == "Festival");
    }

    [Fact]
    public async Task LoadAgendaCommand_IncludesVisibleHolidayEventsAsReadOnlyEntries()
    {
        App.HolidayService = new HolidayService((_, countryCode) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(
            countryCode == "CA"
                ? new[]
                {
                    new HolidayService.HolidayRecord
                    {
                        Date = DateTime.Today.AddDays(1),
                        LocalName = "Canada Day",
                        EnglishName = "Canada Day",
                        Types = new[] { "Public" }
                    }
                }
                : Array.Empty<HolidayService.HolidayRecord>()));

        var viewModel = new AgendaViewModel();

        await viewModel.LoadAgendaCommand.ExecuteAsync(null);

        var group = Assert.Single(viewModel.AgendaGroups, g => g.Date == DateTime.Today.AddDays(1));
        var holiday = Assert.Single(group.Events, e => e.Title == "Canada Day");
        Assert.True(holiday.IsReadOnly);
        Assert.True(holiday.IsOfficialHoliday);
    }

    [Fact]
    public async Task LoadAgendaCommand_HidesHolidayEventsFromHiddenHolidayCalendars()
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
                        Date = DateTime.Today.AddDays(1),
                        LocalName = "Canada Day",
                        EnglishName = "Canada Day",
                        Types = new[] { "Public" }
                    }
                }
                : Array.Empty<HolidayService.HolidayRecord>()));

        var viewModel = new AgendaViewModel();

        await viewModel.LoadAgendaCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.AgendaGroups.SelectMany(g => g.Events), e => e.Title == "Canada Day");
    }

    [Fact]
    public async Task LoadAgendaCommand_OrdersAllDayEventsBeforeTimedEventsWithinDay()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Timed event",
            StartTime = DateTime.Today.AddHours(10),
            EndTime = DateTime.Today.AddHours(11),
            CalendarId = 1
        });
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "All day event",
            StartTime = DateTime.Today,
            EndTime = DateTime.Today.AddHours(23).AddMinutes(59),
            IsAllDay = true,
            CalendarId = 1
        });

        var viewModel = new AgendaViewModel();

        await viewModel.LoadAgendaCommand.ExecuteAsync(null);

        var todayGroup = Assert.Single(viewModel.AgendaGroups, g => g.Date == DateTime.Today);
        Assert.Equal(new[] { "All day event", "Timed event" }, todayGroup.Events.Select(e => e.Title).ToArray());
    }

    [Fact]
    public async Task LoadMoreCommand_LoadsFurtherFutureEvents()
    {
        await Db.SaveEventAsync(new CalendarEvent
        {
            Title = "Far future",
            StartTime = DateTime.Today.AddDays(45).AddHours(9),
            EndTime = DateTime.Today.AddDays(45).AddHours(10),
            CalendarId = 1
        });

        var viewModel = new AgendaViewModel();

        await viewModel.LoadAgendaCommand.ExecuteAsync(null);
        Assert.DoesNotContain(viewModel.AgendaGroups.SelectMany(g => g.Events), e => e.Title == "Far future");

        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.AgendaGroups.SelectMany(g => g.Events), e => e.Title == "Far future");
        Assert.Equal(60, viewModel.DaysToLoad);
    }

    [Fact]
    public async Task LoadMoreCommand_DoesNotAdvanceDaysToLoadWhenAnotherLoadIsAlreadyRunning()
    {
        var viewModel = new AgendaViewModel
        {
            DaysToLoad = 30,
            IsLoading = true
        };

        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(30, viewModel.DaysToLoad);
    }
}
