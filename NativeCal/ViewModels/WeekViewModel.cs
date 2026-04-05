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
    public partial DateTime WeekStart { get; set; }

    [ObservableProperty]
    public partial string WeekTitle { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DayColumn> DayColumns { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> HourLabels { get; set; }

    public WeekViewModel()
    {
        WeekTitle = string.Empty;
        DayColumns = new();
        HourLabels = new ObservableCollection<string>(DateTimeHelper.GetHourLabels());
        WeekStart = DateTimeHelper.GetWeekStart(DateTime.Today);
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

            var calendars = await App.Database.GetCalendarsAsync();
            var visibleCalendarIds = calendars.Where(c => c.IsVisible).Select(c => c.Id).ToHashSet();

            List<CalendarEvent> events = (await App.Database.GetEventsAsync(WeekStart, weekEnd))
                .Where(e => visibleCalendarIds.Contains(e.CalendarId))
                .ToList();

            events.AddRange(await App.HolidayService.GetHolidayEventsAsync(WeekStart, weekEnd, calendars));

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
        public partial DateTime Date { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<CalendarEventViewModel> Events { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<CalendarEventViewModel> AllDayEvents { get; set; }

        public DayColumn()
        {
            Events = new();
            AllDayEvents = new();
        }

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
