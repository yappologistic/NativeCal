using System;
using System.Threading.Tasks;
using NativeCal.Models;

namespace NativeCal.Tests.Models;

public class CalendarEventTests
{
    [Fact]
    public void Clone_ProducesDeepCopy()
    {
        var original = new CalendarEvent
        {
            Id = 42,
            Title = "Team Meeting",
            Description = "Weekly sync",
            Location = "Room 101",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            IsAllDay = false,
            CalendarId = 1,
            ColorHex = "#E74C3C",
            RecurrenceRule = "Weekly",
            ReminderMinutes = 15,
            CreatedAt = new DateTime(2026, 1, 1),
            ModifiedAt = new DateTime(2026, 4, 1)
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.Title, clone.Title);
        Assert.Equal(original.Description, clone.Description);
        Assert.Equal(original.Location, clone.Location);
        Assert.Equal(original.StartTime, clone.StartTime);
        Assert.Equal(original.EndTime, clone.EndTime);
        Assert.Equal(original.IsAllDay, clone.IsAllDay);
        Assert.Equal(original.CalendarId, clone.CalendarId);
        Assert.Equal(original.ColorHex, clone.ColorHex);
        Assert.Equal(original.RecurrenceRule, clone.RecurrenceRule);
        Assert.Equal(original.ReminderMinutes, clone.ReminderMinutes);
        Assert.Equal(original.CreatedAt, clone.CreatedAt);
        Assert.Equal(original.ModifiedAt, clone.ModifiedAt);
    }

    [Fact]
    public void Clone_ModifyingCloneDoesNotAffectOriginal()
    {
        var original = new CalendarEvent
        {
            Id = 1,
            Title = "Original",
            Description = "Original desc",
            ColorHex = "#4A90D9"
        };

        var clone = original.Clone();
        clone.Title = "Modified";
        clone.Description = "Modified desc";
        clone.ColorHex = "#E74C3C";

        Assert.Equal("Original", original.Title);
        Assert.Equal("Original desc", original.Description);
        Assert.Equal("#4A90D9", original.ColorHex);
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var evt = new CalendarEvent();

        Assert.Equal(0, evt.Id);
        Assert.Equal(string.Empty, evt.Title);
        Assert.Null(evt.Description);
        Assert.Null(evt.Location);
        Assert.Null(evt.ColorHex);
        Assert.Null(evt.RecurrenceRule);
        Assert.Equal(15, evt.ReminderMinutes);
        Assert.False(evt.IsAllDay);
        Assert.True(evt.CreatedAt <= DateTime.UtcNow);
        Assert.True(evt.ModifiedAt <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("A very long title that exceeds normal length but is still valid for a calendar event name")]
    public void Title_AcceptsVariousLengths(string title)
    {
        var evt = new CalendarEvent { Title = title };
        Assert.Equal(title, evt.Title);
    }

    [Fact]
    public void StartTime_EndTime_CanSpanMidnight()
    {
        var evt = new CalendarEvent
        {
            StartTime = new DateTime(2026, 4, 5, 22, 0, 0),
            EndTime = new DateTime(2026, 4, 6, 2, 0, 0)
        };

        Assert.True(evt.EndTime > evt.StartTime);
        Assert.NotEqual(evt.StartTime.Date, evt.EndTime.Date);
    }
}
