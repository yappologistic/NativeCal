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
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CalendarInfo> Calendars { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string AppVersion { get; set; }

    [ObservableProperty]
    public partial int DefaultReminderMinutes { get; set; }

    [ObservableProperty]
    public partial int FirstDayOfWeekIndex { get; set; }

    public SettingsViewModel()
    {
        Calendars = new();
        AppVersion = "1.0.0";
        DefaultReminderMinutes = 15;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version is not null
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

            string themeValue = await App.Database.GetSettingAsync(ThemeSettingKey, "0");
            if (int.TryParse(themeValue, out int themeIndex))
            {
                SelectedThemeIndex = Math.Clamp(themeIndex, 0, 2);
            }

            string reminderValue = await App.Database.GetSettingAsync(DefaultReminderKey, "15");
            if (int.TryParse(reminderValue, out int reminderMinutes))
            {
                DefaultReminderMinutes = Math.Max(0, reminderMinutes);
            }

            string firstDayValue = await App.Database.GetSettingAsync(FirstDayOfWeekKey, "0");
            if (int.TryParse(firstDayValue, out int firstDay))
            {
                if (firstDay != 0 && firstDay != 1 && firstDay != 6)
                    firstDay = 0;
                FirstDayOfWeekIndex = firstDay;
            }

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

        var calendarList = await App.Database.GetCalendarsAsync();
        Calendars = new ObservableCollection<CalendarInfo>(calendarList);
    }

    [RelayCommand]
    private async Task DeleteCalendar(CalendarInfo? calendar)
    {
        if (calendar is null)
            return;

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
