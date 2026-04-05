using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.Services;

namespace NativeCal.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string ThemeSettingKey = "Theme";
    private const string DefaultReminderKey = "DefaultReminderMinutes";
    private const string FirstDayOfWeekKey = "FirstDayOfWeek";

    [ObservableProperty]
    private int selectedThemeIndex; // 0=System, 1=Light, 2=Dark

    [ObservableProperty]
    private ObservableCollection<CalendarInfo> calendars = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string appVersion = string.Empty;

    [ObservableProperty]
    private int defaultReminderMinutes = 15;

    [ObservableProperty]
    private int firstDayOfWeekIndex; // 0=Sunday, 1=Monday, 6=Saturday

    public SettingsViewModel()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        appVersion = version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "1.0.0";
    }

    [RelayCommand]
    private async Task LoadSettings()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            // Load theme preference
            string themeValue = await App.Database.GetSettingAsync(ThemeSettingKey, "0");
            if (int.TryParse(themeValue, out int themeIndex))
            {
                SelectedThemeIndex = Math.Clamp(themeIndex, 0, 2);
            }

            // Load default reminder
            string reminderValue = await App.Database.GetSettingAsync(DefaultReminderKey, "15");
            if (int.TryParse(reminderValue, out int reminderMinutes))
            {
                DefaultReminderMinutes = Math.Max(0, reminderMinutes);
            }

            // Load first day of week
            string firstDayValue = await App.Database.GetSettingAsync(FirstDayOfWeekKey, "0");
            if (int.TryParse(firstDayValue, out int firstDay))
            {
                // Valid values: 0 (Sunday), 1 (Monday), 6 (Saturday)
                // Clamp to known valid values
                if (firstDay != 0 && firstDay != 1 && firstDay != 6)
                    firstDay = 0;
                FirstDayOfWeekIndex = firstDay;
            }

            // Load calendars
            var calendarList = await App.Database.GetCalendarsAsync();
            Calendars = new ObservableCollection<CalendarInfo>(calendarList);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveTheme()
    {
        await App.Database.SetSettingAsync(ThemeSettingKey, SelectedThemeIndex.ToString());
    }

    [RelayCommand]
    private async Task AddCalendar()
    {
        int colorIndex = Calendars.Count % ColorHelper.CalendarColors.Length;
        string color = ColorHelper.CalendarColors[colorIndex];

        var newCalendar = new CalendarInfo
        {
            Name = "New Calendar",
            ColorHex = color,
            IsVisible = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        await App.Database.SaveCalendarAsync(newCalendar);

        // Reload calendars to get the auto-incremented Id
        var calendarList = await App.Database.GetCalendarsAsync();
        Calendars = new ObservableCollection<CalendarInfo>(calendarList);
    }

    [RelayCommand]
    private async Task DeleteCalendar(CalendarInfo? calendar)
    {
        if (calendar is null)
            return;

        // Prevent deleting the last calendar
        if (Calendars.Count <= 1)
            return;

        await App.Database.DeleteCalendarAsync(calendar.Id);

        var calendarList = await App.Database.GetCalendarsAsync();
        Calendars = new ObservableCollection<CalendarInfo>(calendarList);
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        await App.Database.SetSettingAsync(ThemeSettingKey, SelectedThemeIndex.ToString());
        await App.Database.SetSettingAsync(DefaultReminderKey, DefaultReminderMinutes.ToString());
        await App.Database.SetSettingAsync(FirstDayOfWeekKey, FirstDayOfWeekIndex.ToString());
    }
}
