using System;
using System.Threading.Tasks;
using NativeCal.Models;
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
