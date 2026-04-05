using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.Services;

namespace NativeCal.ViewModels;

public partial class DayViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime currentDate;

    [ObservableProperty]
    private string dayTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CalendarEventViewModel> events = new();

    [ObservableProperty]
    private ObservableCollection<CalendarEventViewModel> allDayEvents = new();

    [ObservableProperty]
    private ObservableCollection<string> hourLabels = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isToday;

    public DayViewModel()
    {
        currentDate = DateTime.Today;
        dayTitle = FormatDayTitle(currentDate);
        isToday = DateTimeHelper.IsSameDay(currentDate, DateTime.Today);
        hourLabels = new ObservableCollection<string>(DateTimeHelper.GetHourLabels());
    }

    partial void OnCurrentDateChanged(DateTime value)
    {
        DayTitle = FormatDayTitle(value);
        IsToday = DateTimeHelper.IsSameDay(value, DateTime.Today);
    }

    private static string FormatDayTitle(DateTime date)
    {
        return date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task LoadDay(DateTime? date = null)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            if (date.HasValue)
            {
                CurrentDate = date.Value.Date;
            }

            List<CalendarEvent> dayEvents = await App.Database.GetEventsForDateAsync(CurrentDate);

            var timedEvents = dayEvents
                .Where(e => !e.IsAllDay)
                .OrderBy(e => e.StartTime)
                .Select(e => new CalendarEventViewModel(e));

            var allDay = dayEvents
                .Where(e => e.IsAllDay)
                .OrderBy(e => e.Title)
                .Select(e => new CalendarEventViewModel(e));

            Events = new ObservableCollection<CalendarEventViewModel>(timedEvents);
            AllDayEvents = new ObservableCollection<CalendarEventViewModel>(allDay);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NextDay()
    {
        CurrentDate = CurrentDate.AddDays(1);
        await LoadDay();
    }

    [RelayCommand]
    private async Task PreviousDay()
    {
        CurrentDate = CurrentDate.AddDays(-1);
        await LoadDay();
    }

    [RelayCommand]
    private async Task GoToToday()
    {
        CurrentDate = DateTime.Today;
        await LoadDay();
    }
}
