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
    public partial ObservableCollection<AgendaGroup> AgendaGroups { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasNoEvents { get; set; }

    [ObservableProperty]
    public partial int DaysToLoad { get; set; }

    public AgendaViewModel()
    {
        AgendaGroups = new();
        DaysToLoad = 30;
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

            var calendars = await App.Database.GetCalendarsAsync();
            var visibleCalendarIds = calendars.Where(c => c.IsVisible).Select(c => c.Id).ToHashSet();

            List<CalendarEvent> events = (await App.Database.GetEventsAsync(startDate, endDate))
                .Where(e => visibleCalendarIds.Contains(e.CalendarId))
                .ToList();

            events.AddRange(await App.HolidayService.GetHolidayEventsAsync(startDate, endDate, calendars));

            var grouped = new Dictionary<DateTime, List<CalendarEvent>>();
            DateTime lastAgendaDay = endDate.AddDays(-1);

            foreach (var evt in events)
            {
                if (evt.IsAllDay)
                {
                    DateTime startDay = evt.StartTime.Date < startDate ? startDate : evt.StartTime.Date;
                    DateTime endDay = evt.EndTime.Date > lastAgendaDay ? lastAgendaDay : evt.EndTime.Date;

                    for (DateTime d = startDay; d <= endDay; d = d.AddDays(1))
                    {
                        if (!grouped.ContainsKey(d))
                            grouped[d] = new List<CalendarEvent>();
                        grouped[d].Add(evt);
                    }

                    continue;
                }

                DateTime firstCandidateDay = evt.StartTime.Date < startDate ? startDate : evt.StartTime.Date;
                DateTime lastCandidateDay = evt.EndTime.Date > lastAgendaDay ? lastAgendaDay : evt.EndTime.Date;

                for (DateTime d = firstCandidateDay; d <= lastCandidateDay; d = d.AddDays(1))
                {
                    DateTime dayStart = d;
                    DateTime dayEnd = d.AddDays(1);

                    if (evt.StartTime < dayEnd && evt.EndTime > dayStart)
                    {
                        if (!grouped.ContainsKey(d))
                            grouped[d] = new List<CalendarEvent>();
                        grouped[d].Add(evt);
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
        if (IsLoading)
            return;

        DaysToLoad += 30;
        await LoadAgenda();
    }

    public partial class AgendaGroup : ObservableObject
    {
        [ObservableProperty]
        public partial DateTime Date { get; set; }

        [ObservableProperty]
        public partial string DateHeader { get; set; }

        [ObservableProperty]
        public partial string RelativeDate { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<CalendarEventViewModel> Events { get; set; }

        public AgendaGroup()
        {
            DateHeader = string.Empty;
            RelativeDate = string.Empty;
            Events = new();
        }
    }
}
