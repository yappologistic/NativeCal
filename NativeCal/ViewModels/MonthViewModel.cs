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

public partial class MonthViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime currentMonth;

    [ObservableProperty]
    private ObservableCollection<DayCell> dayCells = new();

    [ObservableProperty]
    private string monthYearTitle = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    public MonthViewModel()
    {
        currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        monthYearTitle = FormatMonthYear(currentMonth);
    }

    partial void OnCurrentMonthChanged(DateTime value)
    {
        MonthYearTitle = FormatMonthYear(value);
    }

    private static string FormatMonthYear(DateTime date)
    {
        return date.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task LoadMonth(DateTime? date = null)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            if (date.HasValue)
            {
                CurrentMonth = new DateTime(date.Value.Year, date.Value.Month, 1);
            }

            DateTime gridStart = DateTimeHelper.GetCalendarGridStart(CurrentMonth);
            DateTime gridEnd = DateTimeHelper.GetCalendarGridEnd(CurrentMonth).AddDays(1);

            List<CalendarEvent> events = await App.Database.GetEventsAsync(gridStart, gridEnd);

            var lookup = new Dictionary<DateTime, List<CalendarEvent>>();
            DateTime lastVisibleDay = gridEnd.AddDays(-1);

            foreach (var evt in events)
            {
                if (evt.IsAllDay)
                {
                    // For all-day events, add to every day they span.
                    DateTime startDay = evt.StartTime.Date < gridStart ? gridStart : evt.StartTime.Date;
                    DateTime endDay = evt.EndTime.Date > lastVisibleDay ? lastVisibleDay : evt.EndTime.Date;

                    for (DateTime d = startDay; d <= endDay; d = d.AddDays(1))
                    {
                        if (!lookup.ContainsKey(d))
                            lookup[d] = new List<CalendarEvent>();
                        lookup[d].Add(evt);
                    }

                    continue;
                }

                DateTime firstCandidateDay = evt.StartTime.Date < gridStart ? gridStart : evt.StartTime.Date;
                DateTime lastCandidateDay = evt.EndTime.Date > lastVisibleDay ? lastVisibleDay : evt.EndTime.Date;

                for (DateTime d = firstCandidateDay; d <= lastCandidateDay; d = d.AddDays(1))
                {
                    DateTime dayStart = d;
                    DateTime dayEnd = d.AddDays(1);

                    if (evt.StartTime < dayEnd && evt.EndTime > dayStart)
                    {
                        if (!lookup.ContainsKey(d))
                            lookup[d] = new List<CalendarEvent>();
                        lookup[d].Add(evt);
                    }
                }
            }

            var cells = new ObservableCollection<DayCell>();
            for (int i = 0; i < 42; i++)
            {
                DateTime cellDate = gridStart.AddDays(i);
                bool isCurrentMonth = cellDate.Month == CurrentMonth.Month && cellDate.Year == CurrentMonth.Year;

                var cellEvents = new ObservableCollection<CalendarEventViewModel>();
                if (lookup.TryGetValue(cellDate, out var dayEvents))
                {
                    foreach (var evt in dayEvents.OrderBy(e => e.IsAllDay ? 0 : 1).ThenBy(e => e.StartTime))
                    {
                        cellEvents.Add(new CalendarEventViewModel(evt));
                    }
                }

                cells.Add(new DayCell
                {
                    Date = cellDate,
                    IsCurrentMonth = isCurrentMonth,
                    Events = cellEvents
                });
            }

            DayCells = cells;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NextMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
        await LoadMonth();
    }

    [RelayCommand]
    private async Task PreviousMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
        await LoadMonth();
    }

    [RelayCommand]
    private async Task GoToToday()
    {
        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadMonth();
    }

    public partial class DayCell : ObservableObject
    {
        [ObservableProperty]
        private DateTime date;

        [ObservableProperty]
        private bool isCurrentMonth;

        [ObservableProperty]
        private ObservableCollection<CalendarEventViewModel> events = new();

        public int DayNumber => Date.Day;

        public bool IsToday => DateTimeHelper.IsSameDay(Date, DateTime.Today);

        partial void OnDateChanged(DateTime value)
        {
            OnPropertyChanged(nameof(DayNumber));
            OnPropertyChanged(nameof(IsToday));
        }
    }
}
