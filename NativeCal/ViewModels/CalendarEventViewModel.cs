using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using NativeCal.Helpers;
using NativeCal.Models;

namespace NativeCal.ViewModels;

/// <summary>
/// Observable wrapper around <see cref="CalendarEvent"/> for data-binding.
/// Exposes computed properties (<see cref="TimeDisplay"/>, <see cref="TopOffset"/>,
/// <see cref="Height"/>) that update when the underlying times change.
/// </summary>
public partial class CalendarEventViewModel : ObservableObject
{
    /// <summary>Pixels-per-minute scaling factor for computed <see cref="TopOffset"/> and <see cref="Height"/>.</summary>
    private const double PixelsPerMinute = 1.2;

    private CalendarEvent _event;

    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial string? Location { get; set; }

    [ObservableProperty]
    public partial DateTime StartTime { get; set; }

    [ObservableProperty]
    public partial DateTime EndTime { get; set; }

    [ObservableProperty]
    public partial bool IsAllDay { get; set; }

    [ObservableProperty]
    public partial int CalendarId { get; set; }

    [ObservableProperty]
    public partial string? ColorHex { get; set; }

    [ObservableProperty]
    public partial string? RecurrenceRule { get; set; }

    [ObservableProperty]
    public partial int ReminderMinutes { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial bool IsOfficialHoliday { get; set; }

    /// <summary>Formatted time range (e.g. "9:00 AM - 10:00 AM" or "All Day").</summary>
    public string TimeDisplay => DateTimeHelper.FormatTimeRange(StartTime, EndTime, IsAllDay);

    /// <summary>Formatted full date (e.g. "Friday, April 5, 2026").</summary>
    public string DateDisplay => StartTime.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>Vertical offset in pixels from the top of a 24-hour grid.</summary>
    public double TopOffset => (StartTime.Hour * 60 + StartTime.Minute) * PixelsPerMinute;

    /// <summary>
    /// Computed height in pixels. All-day events use a fixed 24px chip;
    /// timed events scale by duration with a minimum of 18px.
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

    /// <summary>Creates an empty view model backed by a default <see cref="CalendarEvent"/>.</summary>
    public CalendarEventViewModel()
    {
        _event = new CalendarEvent();
        Title = string.Empty;
    }

    /// <summary>Wraps an existing <see cref="CalendarEvent"/> model for data-binding.</summary>
    public CalendarEventViewModel(CalendarEvent evt)
    {
        _event = evt ?? throw new ArgumentNullException(nameof(evt));

        Id = evt.Id;
        Title = evt.Title;
        Description = evt.Description;
        Location = evt.Location;
        StartTime = evt.StartTime;
        EndTime = evt.EndTime;
        IsAllDay = evt.IsAllDay;
        CalendarId = evt.CalendarId;
        ColorHex = evt.ColorHex;
        RecurrenceRule = evt.RecurrenceRule;
        ReminderMinutes = evt.ReminderMinutes;
        IsReadOnly = evt.IsReadOnly;
        IsOfficialHoliday = evt.IsOfficialHoliday;
    }

    /// <summary>
    /// Converts back to a <see cref="CalendarEvent"/> model for database persistence.
    /// Preserves the original <see cref="CalendarEvent.CreatedAt"/> and sets
    /// <see cref="CalendarEvent.ModifiedAt"/> to now.
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
            ModifiedAt = DateTime.UtcNow,
            IsReadOnly = IsReadOnly,
            IsOfficialHoliday = IsOfficialHoliday
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
