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

public partial class WeekViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime weekStart;

    [ObservableProperty]
    private string weekTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DayColumn> dayColumns = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private ObservableCollection<string> hourLabels = new();

    public WeekViewModel()
    {
        weekStart = DateTimeHelper.GetWeekStart(DateTime.Today);
        weekTitle = FormatWeekTitle(weekStart);
        hourLabels = new ObservableCollection<string>(DateTimeHelper.GetHourLabels());
    }

    partial void OnWeekStartChanged(DateTime value)
    {
        WeekTitle = FormatWeekTitle(value);
    }

    private static string FormatWeekTitle(DateTime start)
    {
        DateTime end = start.AddDays(6);

        if (start.Year != end.Year)
        {
            return $"{start.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)} - {end.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}";
        }

        if (start.Month != end.Month)
        {
            return $"{start.ToString("MMM d", CultureInfo.CurrentCulture)} - {end.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}";
        }

        return $"{start.ToString("MMM d", CultureInfo.CurrentCulture)} - {end.ToString("d, yyyy", CultureInfo.CurrentCulture)}";
    }

    [RelayCommand]
    private async Task LoadWeek(DateTime? date = null)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            if (date.HasValue)
            {
                WeekStart = DateTimeHelper.GetWeekStart(date.Value);
            }

            DateTime weekEnd = WeekStart.AddDays(7);

            List<CalendarEvent> events = await App.Database.GetEventsAsync(WeekStart, weekEnd);

            var columns = new ObservableCollection<DayColumn>();
            for (int i = 0; i < 7; i++)
            {
                DateTime columnDate = WeekStart.AddDays(i);
                DateTime columnEnd = columnDate.AddDays(1);

                var dayEvents = events
                    .Where(e =>
                        !e.IsAllDay &&
                        ((e.StartTime >= columnDate && e.StartTime < columnEnd) ||
                         (e.EndTime > columnDate && e.EndTime <= columnEnd) ||
                         (e.StartTime <= columnDate && e.EndTime >= columnEnd)))
                    .OrderBy(e => e.StartTime)
                    .Select(e => new CalendarEventViewModel(e));

                var allDayEvents = events
                    .Where(e =>
                        e.IsAllDay &&
                        e.StartTime.Date <= columnDate.Date &&
                        e.EndTime.Date >= columnDate.Date)
                    .OrderBy(e => e.Title)
                    .Select(e => new CalendarEventViewModel(e));

                columns.Add(new DayColumn
                {
                    Date = columnDate,
                    Events = new ObservableCollection<CalendarEventViewModel>(dayEvents),
                    AllDayEvents = new ObservableCollection<CalendarEventViewModel>(allDayEvents)
                });
            }

            DayColumns = columns;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NextWeek()
    {
        WeekStart = WeekStart.AddDays(7);
        await LoadWeek();
    }

    [RelayCommand]
    private async Task PreviousWeek()
    {
        WeekStart = WeekStart.AddDays(-7);
        await LoadWeek();
    }

    [RelayCommand]
    private async Task GoToToday()
    {
        WeekStart = DateTimeHelper.GetWeekStart(DateTime.Today);
        await LoadWeek();
    }

    public partial class DayColumn : ObservableObject
    {
        [ObservableProperty]
        private DateTime date;

        [ObservableProperty]
        private ObservableCollection<CalendarEventViewModel> events = new();

        [ObservableProperty]
        private ObservableCollection<CalendarEventViewModel> allDayEvents = new();

        public string DayName => Date.ToString("ddd", CultureInfo.CurrentCulture);

        public int DayNumber => Date.Day;

        public bool IsToday => DateTimeHelper.IsSameDay(Date, DateTime.Today);

        partial void OnDateChanged(DateTime value)
        {
            OnPropertyChanged(nameof(DayName));
            OnPropertyChanged(nameof(DayNumber));
            OnPropertyChanged(nameof(IsToday));
        }
    }
}
