using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.ViewModels;

public partial class CalendarEventViewModel : ObservableObject
{
    private const double PixelsPerMinute = 1.2;

    private CalendarEvent _event;

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string? location;

    [ObservableProperty]
    private DateTime startTime;

    [ObservableProperty]
    private DateTime endTime;

    [ObservableProperty]
    private bool isAllDay;

    [ObservableProperty]
    private int calendarId;

    [ObservableProperty]
    private string? colorHex;

    [ObservableProperty]
    private string? recurrenceRule;

    [ObservableProperty]
    private int reminderMinutes;

    /// <summary>
    /// Formatted time range display, e.g. "9:00 AM - 10:30 AM" or "All Day".
    /// </summary>
    public string TimeDisplay => DateTimeHelper.FormatTimeRange(StartTime, EndTime, IsAllDay);

    /// <summary>
    /// Formatted date display, e.g. "Friday, April 4, 2026".
    /// </summary>
    public string DateDisplay => StartTime.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>
    /// Top offset in pixels for positioning in day/week view.
    /// Calculated as minutes from midnight * pixels per minute.
    /// </summary>
    public double TopOffset => (StartTime.Hour * 60 + StartTime.Minute) * PixelsPerMinute;

    /// <summary>
    /// Height in pixels for the event block in day/week view.
    /// Calculated as duration in minutes * pixels per minute, with a minimum of 18px.
    /// </summary>
    public double Height
    {
        get
        {
            if (IsAllDay)
            {
                return 24.0;
            }

            double durationMinutes = (EndTime - StartTime).TotalMinutes;
            double height = durationMinutes * PixelsPerMinute;
            return Math.Max(height, 18.0);
        }
    }

    public CalendarEventViewModel()
    {
        _event = new CalendarEvent();
    }

    public CalendarEventViewModel(CalendarEvent evt)
    {
        _event = evt ?? throw new ArgumentNullException(nameof(evt));

        id = evt.Id;
        title = evt.Title;
        description = evt.Description;
        location = evt.Location;
        startTime = evt.StartTime;
        endTime = evt.EndTime;
        isAllDay = evt.IsAllDay;
        calendarId = evt.CalendarId;
        colorHex = evt.ColorHex;
        recurrenceRule = evt.RecurrenceRule;
        reminderMinutes = evt.ReminderMinutes;
    }

    /// <summary>
    /// Creates a CalendarEvent model from the current view model state.
    /// </summary>
    public CalendarEvent ToModel()
    {
        return new CalendarEvent
        {
            Id = Id,
            Title = Title,
            Description = Description,
            Location = Location,
            StartTime = StartTime,
            EndTime = EndTime,
            IsAllDay = IsAllDay,
            CalendarId = CalendarId,
            ColorHex = ColorHex,
            RecurrenceRule = RecurrenceRule,
            ReminderMinutes = ReminderMinutes,
            CreatedAt = _event.CreatedAt,
            ModifiedAt = DateTime.UtcNow
        };
    }

    partial void OnStartTimeChanged(DateTime value)
    {
        OnPropertyChanged(nameof(TimeDisplay));
        OnPropertyChanged(nameof(DateDisplay));
        OnPropertyChanged(nameof(TopOffset));
        OnPropertyChanged(nameof(Height));
    }

    partial void OnEndTimeChanged(DateTime value)
    {
        OnPropertyChanged(nameof(TimeDisplay));
        OnPropertyChanged(nameof(Height));
    }

    partial void OnIsAllDayChanged(bool value)
    {
        OnPropertyChanged(nameof(TimeDisplay));
        OnPropertyChanged(nameof(Height));
    }
}
