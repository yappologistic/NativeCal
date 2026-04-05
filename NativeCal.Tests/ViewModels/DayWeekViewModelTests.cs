using System;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;
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
        Assert.Contains(monday.AllDayEvents, e => e.Title == "Conference");
        Assert.Contains(tuesday.AllDayEvents, e => e.Title == "Conference");
        Assert.Contains(wednesday.AllDayEvents, e => e.Title == "Conference");
    }
}
