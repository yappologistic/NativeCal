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
}
