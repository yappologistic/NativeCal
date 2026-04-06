using System;
using System.Linq;
using System.Threading.Tasks;
using NativeCal.Models;
using NativeCal.Services;
using SQLite;

namespace NativeCal.Tests.Services;

/// <summary>
/// Regression tests for service-layer safeguards added during the polish pass.
/// </summary>
public class DatabaseServicePolishTests : TestBase
{
    [Fact]
    public async Task GetDefaultReminderMinutesAsync_ReturnsStoredValueWhenValid()
    {
        await Db.SetSettingAsync("DefaultReminderMinutes", "30");

        int reminderMinutes = await Db.GetDefaultReminderMinutesAsync();

        Assert.Equal(30, reminderMinutes);
    }

    [Fact]
    public async Task GetDefaultReminderMinutesAsync_FallsBackWhenStoredValueIsInvalid()
    {
        await Db.SetSettingAsync("DefaultReminderMinutes", "not-a-number");

        int reminderMinutes = await Db.GetDefaultReminderMinutesAsync();

        Assert.Equal(15, reminderMinutes);
    }

    [Fact]
    public async Task GetDefaultReminderMinutesAsync_FallsBackWhenStoredValueIsUnsupported()
    {
        await Db.SetSettingAsync("DefaultReminderMinutes", "999");

        int reminderMinutes = await Db.GetDefaultReminderMinutesAsync();

        Assert.Equal(15, reminderMinutes);
    }

    [Fact]
    public async Task SaveCalendarAsync_RejectsReservedHolidayNameForNewCalendar()
    {
        var reservedCalendar = new CalendarInfo
        {
            Name = "US Holidays",
            ColorHex = "#123456",
            IsVisible = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.SaveCalendarAsync(reservedCalendar));
    }

    [Fact]
    public async Task SaveCalendarAsync_RejectsRenamingUserCalendarToReservedHolidayName()
    {
        var calendar = new CalendarInfo
        {
            Name = "Project Alpha",
            ColorHex = "#123456",
            IsVisible = true
        };
        await Db.SaveCalendarAsync(calendar);

        calendar.Name = "Canada Holidays";

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.SaveCalendarAsync(calendar));
    }

    [Fact]
    public async Task SaveCalendarAsync_AllowsSavingLegacyReservedNameWhenUnchanged()
    {
        var dbField = typeof(DatabaseService).GetField("_db", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(dbField);
        var rawDb = (SQLiteAsyncConnection)dbField!.GetValue(Db)!;

        var legacyCalendar = new CalendarInfo
        {
            Name = "US Holidays",
            ColorHex = "#123456",
            IsVisible = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };
        await rawDb.InsertAsync(legacyCalendar);

        legacyCalendar.ColorHex = "#654321";

        int savedId = await Db.SaveCalendarAsync(legacyCalendar);

        Assert.Equal(legacyCalendar.Id, savedId);
        var reloaded = (await Db.GetCalendarsAsync()).First(c => c.Id == legacyCalendar.Id);
        Assert.Equal("US Holidays", reloaded.Name);
        Assert.Equal("#654321", reloaded.ColorHex);
    }

    [Fact]
    public async Task SaveCalendarAsync_MissingExistingCalendar_ThrowsInsteadOfPretendingSuccess()
    {
        var missingCalendar = new CalendarInfo
        {
            Id = 999999,
            Name = "Missing",
            ColorHex = "#123456",
            IsVisible = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.SaveCalendarAsync(missingCalendar));
    }

    [Fact]
    public async Task SaveEventAsync_MissingExistingEvent_ThrowsInsteadOfPretendingSuccess()
    {
        var missingEvent = new CalendarEvent
        {
            Id = 999999,
            Title = "Missing",
            StartTime = new DateTime(2026, 4, 5, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 5, 10, 0, 0),
            CalendarId = 1
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.SaveEventAsync(missingEvent));
    }
}
