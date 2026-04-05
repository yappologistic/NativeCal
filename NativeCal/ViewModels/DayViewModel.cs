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
    public partial DateTime CurrentDate { get; set; }

    [ObservableProperty]
    public partial string DayTitle { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CalendarEventViewModel> Events { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CalendarEventViewModel> AllDayEvents { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> HourLabels { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsToday { get; set; }

    public DayViewModel()
    {
        DayTitle = string.Empty;
        Events = new();
        AllDayEvents = new();
        HourLabels = new ObservableCollection<string>(DateTimeHelper.GetHourLabels());
        CurrentDate = DateTime.Today;
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

            var calendars = await App.Database.GetCalendarsAsync();
            var visibleCalendarIds = calendars.Where(c => c.IsVisible).Select(c => c.Id).ToHashSet();

            List<CalendarEvent> dayEvents = (await App.Database.GetEventsForDateAsync(CurrentDate))
                .Where(e => visibleCalendarIds.Contains(e.CalendarId))
                .ToList();

            dayEvents.AddRange(await App.HolidayService.GetHolidayEventsAsync(CurrentDate.Date, CurrentDate.Date.AddDays(1), calendars));

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
