using System;
using System.Linq;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.ViewModels;

namespace NativeCal.Tests.Helpers;

public class EventLayoutHelperTests
{
    [Fact]
    public void CalculateOverlapPlacements_AssignsSeparateColumnsForSameTimeEvents()
    {
        var first = new CalendarEventViewModel(new CalendarEvent
        {
            Id = 1,
            Title = "First",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });
        var second = new CalendarEventViewModel(new CalendarEvent
        {
            Id = 2,
            Title = "Second",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        });

        var placements = EventLayoutHelper.CalculateOverlapPlacements(new[] { first, second });

        Assert.Equal(2, placements.Count);
        Assert.Equal(2, placements.Max(p => p.TotalColumns));
        Assert.Equal(2, placements.Select(p => p.ColumnIndex).Distinct().Count());
    }

    [Fact]
    public void CalculateOverlapPlacements_ReusesColumnForBackToBackEvents()
    {
        var first = CreateEvent(1, "First", 9, 0, 10, 0);
        var second = CreateEvent(2, "Second", 10, 0, 11, 0);

        var placements = EventLayoutHelper.CalculateOverlapPlacements(new[] { first, second });

        Assert.All(placements, placement => Assert.Equal(1, placement.TotalColumns));
        Assert.All(placements, placement => Assert.Equal(0, placement.ColumnIndex));
    }

    [Fact]
    public void CalculateOverlapPlacements_UsesPeakOverlapColumnCountAcrossCluster()
    {
        var first = CreateEvent(1, "First", 9, 0, 11, 0);
        var second = CreateEvent(2, "Second", 9, 30, 10, 30);
        var third = CreateEvent(3, "Third", 10, 0, 12, 0);

        var placements = EventLayoutHelper.CalculateOverlapPlacements(new[] { first, second, third });

        Assert.Equal(3, placements.Count);
        Assert.All(placements, placement => Assert.Equal(3, placement.TotalColumns));
        Assert.Equal(3, placements.Select(p => p.ColumnIndex).Distinct().Count());
    }

    private static CalendarEventViewModel CreateEvent(int id, string title, int startHour, int startMinute, int endHour, int endMinute)
    {
        return new CalendarEventViewModel(new CalendarEvent
        {
            Id = id,
            Title = title,
            StartTime = new DateTime(2026, 4, 5, startHour, startMinute, 0),
            EndTime = new DateTime(2026, 4, 5, endHour, endMinute, 0),
            CalendarId = 1
        });
    }
}
