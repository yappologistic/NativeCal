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
        Assert.Equal(15, viewModel.DefaultReminderMinutes);
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
        Assert.Equal("15", await Db.GetSettingAsync("DefaultReminderMinutes", "15"));
        Assert.Equal("6", await Db.GetSettingAsync("FirstDayOfWeek", "0"));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(1440)]
    public async Task LoadSettingsCommand_PreservesExtendedReminderOptions(int reminderMinutes)
    {
        await Db.SetSettingAsync("DefaultReminderMinutes", reminderMinutes.ToString());

        var viewModel = new SettingsViewModel();

        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(reminderMinutes, viewModel.DefaultReminderMinutes);
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

    /// <summary>
    /// Regression: verifies that loading settings does not overwrite persisted
    /// values with defaults. This catches the bug where XAML-triggered
    /// SelectionChanged events could write "0" back to the database before
    /// LoadSettingsAsync reads the saved value.
    /// </summary>
    [Fact]
    public async Task LoadSettingsCommand_DoesNotOverwritePersistedTheme()
    {
        // Persist Dark theme (index 2)
        await Db.SetSettingAsync("Theme", "2");

        var viewModel = new SettingsViewModel();
        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        // Verify the VM loaded the saved value, not a default
        Assert.Equal(2, viewModel.SelectedThemeIndex);

        // Verify the database still has the saved value (not overwritten)
        string dbValue = await Db.GetSettingAsync("Theme", "MISSING");
        Assert.Equal("2", dbValue);
    }

    /// <summary>
    /// Regression: verifies that loading settings does not overwrite persisted
    /// first-day-of-week value with defaults.
    /// </summary>
    [Fact]
    public async Task LoadSettingsCommand_DoesNotOverwritePersistedFirstDayOfWeek()
    {
        // Persist Saturday (value 6)
        await Db.SetSettingAsync("FirstDayOfWeek", "6");

        var viewModel = new SettingsViewModel();
        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(6, viewModel.FirstDayOfWeekIndex);

        // Verify the database still has the saved value
        string dbValue = await Db.GetSettingAsync("FirstDayOfWeek", "MISSING");
        Assert.Equal("6", dbValue);
    }

    /// <summary>
    /// Regression: verifies that loading settings does not overwrite persisted
    /// default reminder minutes with defaults.
    /// </summary>
    [Fact]
    public async Task LoadSettingsCommand_DoesNotOverwritePersistedReminder()
    {
        // Persist 30 minutes
        await Db.SetSettingAsync("DefaultReminderMinutes", "30");

        var viewModel = new SettingsViewModel();
        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(30, viewModel.DefaultReminderMinutes);

        // Verify the database still has the saved value
        string dbValue = await Db.GetSettingAsync("DefaultReminderMinutes", "MISSING");
        Assert.Equal("30", dbValue);
    }
}
