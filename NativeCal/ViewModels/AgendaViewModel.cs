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

public partial class AgendaViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AgendaGroup> agendaGroups = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasNoEvents;

    [ObservableProperty]
    private int daysToLoad = 30;

    public AgendaViewModel()
    {
    }

    [RelayCommand]
    private async Task LoadAgenda()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            DateTime startDate = DateTime.Today;
            DateTime endDate = startDate.AddDays(DaysToLoad);

            List<CalendarEvent> events = await App.Database.GetEventsAsync(startDate, endDate);

            var grouped = new Dictionary<DateTime, List<CalendarEvent>>();
            foreach (var evt in events)
            {
                if (evt.IsAllDay)
                {
                    for (DateTime d = evt.StartTime.Date; d <= evt.EndTime.Date && d < endDate; d = d.AddDays(1))
                    {
                        if (d >= startDate)
                        {
                            if (!grouped.ContainsKey(d))
                                grouped[d] = new List<CalendarEvent>();
                            grouped[d].Add(evt);
                        }
                    }
                }
                else
                {
                    DateTime evtDate = evt.StartTime.Date;
                    if (evtDate >= startDate)
                    {
                        if (!grouped.ContainsKey(evtDate))
                            grouped[evtDate] = new List<CalendarEvent>();
                        grouped[evtDate].Add(evt);
                    }
                }
            }

            var groups = new ObservableCollection<AgendaGroup>();
            foreach (var kvp in grouped.OrderBy(k => k.Key))
            {
                DateTime date = kvp.Key;
                string relative = DateTimeHelper.GetRelativeDate(date);
                string dayFormatted = date.ToString("dddd, MMMM d", CultureInfo.CurrentCulture);

                string dateHeader = relative == dayFormatted
                    ? dayFormatted
                    : $"{relative} - {dayFormatted}";

                var eventViewModels = kvp.Value
                    .OrderBy(e => e.IsAllDay ? 0 : 1)
                    .ThenBy(e => e.StartTime)
                    .Select(e => new CalendarEventViewModel(e));

                groups.Add(new AgendaGroup
                {
                    Date = date,
                    DateHeader = dateHeader,
                    RelativeDate = relative,
                    Events = new ObservableCollection<CalendarEventViewModel>(eventViewModels)
                });
            }

            AgendaGroups = groups;
            HasNoEvents = groups.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        DaysToLoad += 30;
        await LoadAgenda();
    }

    public partial class AgendaGroup : ObservableObject
    {
        [ObservableProperty]
        private DateTime date;

        [ObservableProperty]
        private string dateHeader = string.Empty;

        [ObservableProperty]
        private string relativeDate = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CalendarEventViewModel> events = new();
    }
}
