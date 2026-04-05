using System;
using System.Collections.Generic;
using System.ComponentModel;
using NativeCal.Models;
using NativeCal.ViewModels;

namespace NativeCal.Tests.ViewModels;

public class CalendarEventViewModelTests
{
    [Fact]
    public void Height_UsesMinimumForZeroOrNegativeDurationTimedEvents()
    {
        var start = new DateTime(2026, 4, 5, 9, 0, 0);

        var zeroDuration = new CalendarEventViewModel(new CalendarEvent
        {
            Title = "Zero",
            StartTime = start,
            EndTime = start,
            CalendarId = 1
        });

        var negativeDuration = new CalendarEventViewModel(new CalendarEvent
        {
            Title = "Negative",
            StartTime = start,
            EndTime = start.AddMinutes(-5),
            CalendarId = 1
        });

        Assert.Equal(18d, zeroDuration.Height);
        Assert.Equal(18d, negativeDuration.Height);
        Assert.Equal(648d, zeroDuration.TopOffset);
    }

    [Fact]
    public void ToModel_PreservesCreatedAtAndRefreshesModifiedAt()
    {
        var original = new CalendarEvent
        {
            Id = 42,
            Title = "Original",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 3,
            CreatedAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ModifiedAt = new DateTime(2025, 1, 3, 3, 4, 5, DateTimeKind.Utc)
        };

        var viewModel = new CalendarEventViewModel(original)
        {
            Title = "Updated"
        };

        var beforeConversion = DateTime.UtcNow;
        var model = viewModel.ToModel();

        Assert.Equal(original.CreatedAt, model.CreatedAt);
        Assert.Equal(42, model.Id);
        Assert.Equal("Updated", model.Title);
        Assert.True(model.ModifiedAt >= beforeConversion.AddSeconds(-1));
    }

    [Fact]
    public void ChangingStartTime_RaisesAllDerivedPropertyNotifications()
    {
        var viewModel = new CalendarEventViewModel(new CalendarEvent
        {
            Title = "Sync",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });

        var changes = CaptureChanges(viewModel);

        viewModel.StartTime = new DateTime(2026, 4, 6, 11, 30, 0);

        Assert.Contains(nameof(CalendarEventViewModel.TimeDisplay), changes);
        Assert.Contains(nameof(CalendarEventViewModel.DateDisplay), changes);
        Assert.Contains(nameof(CalendarEventViewModel.TopOffset), changes);
        Assert.Contains(nameof(CalendarEventViewModel.Height), changes);
    }

    [Fact]
    public void ChangingEndTimeAndAllDay_RaisesDerivedPropertyNotifications()
    {
        var viewModel = new CalendarEventViewModel(new CalendarEvent
        {
            Title = "Sync",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });

        var changes = CaptureChanges(viewModel);

        viewModel.EndTime = new DateTime(2026, 4, 5, 11, 0, 0);
        viewModel.IsAllDay = true;

        Assert.Contains(nameof(CalendarEventViewModel.TimeDisplay), changes);
        Assert.Contains(nameof(CalendarEventViewModel.Height), changes);
    }

    private static HashSet<string?> CaptureChanges(INotifyPropertyChanged source)
    {
        var changes = new HashSet<string?>();
        source.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        return changes;
    }
}
