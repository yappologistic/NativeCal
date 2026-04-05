using System;
using System.Threading.Tasks;
using NativeCal.Helpers;
using NativeCal.ViewModels;

namespace NativeCal.Tests.ViewModels;

public class SettingsViewModelTests : TestBase
{
    [Fact]
    public async Task LoadSettingsCommand_ClampsInvalidStoredValuesAndPreservesSaturday()
    {
        await Db.SetSettingAsync("Theme", "9");
        await Db.SetSettingAsync("DefaultReminderMinutes", "-5");
        await Db.SetSettingAsync("FirstDayOfWeek", "6");

        var viewModel = new SettingsViewModel();

        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.SelectedThemeIndex);
        Assert.Equal(0, viewModel.DefaultReminderMinutes);
        Assert.Equal(6, viewModel.FirstDayOfWeekIndex);
        Assert.Equal(5, viewModel.Calendars.Count);
    }

    [Fact]
    public async Task LoadSettingsCommand_FallsBackToSundayForUnknownFirstDayValue()
    {
        await Db.SetSettingAsync("FirstDayOfWeek", "4");

        var viewModel = new SettingsViewModel();

        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(0, viewModel.FirstDayOfWeekIndex);
    }

    [Fact]
    public async Task SaveThemeCommand_PersistsSelectedThemeIndex()
    {
        var viewModel = new SettingsViewModel
        {
            SelectedThemeIndex = 2
        };

        await viewModel.SaveThemeCommand.ExecuteAsync(null);

        Assert.Equal("2", await Db.GetSettingAsync("Theme", "0"));
    }

    [Fact]
    public async Task SaveSettingsCommand_PersistsThemeReminderAndFirstDay()
    {
        var viewModel = new SettingsViewModel
        {
            SelectedThemeIndex = 1,
            DefaultReminderMinutes = 45,
            FirstDayOfWeekIndex = 6
        };

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal("1", await Db.GetSettingAsync("Theme", "0"));
        Assert.Equal("45", await Db.GetSettingAsync("DefaultReminderMinutes", "15"));
        Assert.Equal("6", await Db.GetSettingAsync("FirstDayOfWeek", "0"));
    }

    [Fact]
    public async Task AddCalendarCommand_UsesCalendarPaletteInRoundRobinOrder()
    {
        var viewModel = new SettingsViewModel();
        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        for (int i = 0; i < ColorHelper.CalendarColors.Length - 2; i++)
        {
            await viewModel.AddCalendarCommand.ExecuteAsync(null);
        }

        var calendars = await Db.GetCalendarsAsync();

        Assert.Equal(ColorHelper.CalendarColors[5], calendars[5].ColorHex);
        Assert.Equal(ColorHelper.CalendarColors[2], calendars[^1].ColorHex);
    }

    [Fact]
    public async Task DeleteCalendarCommand_RefreshesCalendarsAndKeepsExactlyOneDefault()
    {
        var viewModel = new SettingsViewModel();
        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        var defaultCalendar = Assert.Single(viewModel.Calendars, c => c.IsDefault);

        await viewModel.DeleteCalendarCommand.ExecuteAsync(defaultCalendar);

        Assert.Equal(4, viewModel.Calendars.Count);
        Assert.Single(viewModel.Calendars, c => c.IsDefault);

        var calendarsInDatabase = await Db.GetCalendarsAsync();
        Assert.Equal(4, calendarsInDatabase.Count);
        Assert.Single(calendarsInDatabase, c => c.IsDefault);
    }

    [Fact]
    public async Task DeleteCalendarCommand_DoesNotRemoveProtectedHolidayCalendars()
    {
        var viewModel = new SettingsViewModel();
        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        var holidayCalendar = Assert.Single(viewModel.Calendars, c => c.Name == "Canada Holidays");

        await viewModel.DeleteCalendarCommand.ExecuteAsync(holidayCalendar);

        Assert.Contains(viewModel.Calendars, c => c.Id == holidayCalendar.Id);
        Assert.Contains(await Db.GetCalendarsAsync(), c => c.Id == holidayCalendar.Id);
    }
}
