using System;
using System.Globalization;
using NativeCal.Helpers;

namespace NativeCal.Tests.Helpers;

public class DateTimeHelperTests
{
    // ── GetWeekStart ────────────────────────────────────────────────────

    [Fact]
    public void GetWeekStart_SundayFirst_ReturnsPreviousSunday()
    {
        // Wednesday, April 1, 2026
        var date = new DateTime(2026, 4, 1);
        var result = DateTimeHelper.GetWeekStart(date, DayOfWeek.Sunday);

        Assert.Equal(DayOfWeek.Sunday, result.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 29), result); // March 29 is Sunday
    }

    [Fact]
    public void GetWeekStart_MondayFirst_ReturnsPreviousMonday()
    {
        // Wednesday, April 1, 2026
        var date = new DateTime(2026, 4, 1);
        var result = DateTimeHelper.GetWeekStart(date, DayOfWeek.Monday);

        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 30), result); // March 30 is Monday
    }

    [Fact]
    public void GetWeekStart_OnFirstDayOfWeek_ReturnsSameDate()
    {
        var sunday = new DateTime(2026, 4, 5); // Sunday
        var result = DateTimeHelper.GetWeekStart(sunday, DayOfWeek.Sunday);

        Assert.Equal(sunday, result);
    }

    [Fact]
    public void GetWeekStart_SaturdayFirst()
    {
        // Monday, April 6, 2026
        var date = new DateTime(2026, 4, 6);
        var result = DateTimeHelper.GetWeekStart(date, DayOfWeek.Saturday);

        Assert.Equal(DayOfWeek.Saturday, result.DayOfWeek);
        Assert.Equal(new DateTime(2026, 4, 4), result); // Saturday April 4
    }

    // ── GetWeekEnd ──────────────────────────────────────────────────────

    [Fact]
    public void GetWeekEnd_ReturnsLastDayOfWeek()
    {
        // Wednesday, April 1, 2026
        var date = new DateTime(2026, 4, 1);
        var result = DateTimeHelper.GetWeekEnd(date, DayOfWeek.Sunday);

        Assert.Equal(DayOfWeek.Saturday, result.DayOfWeek);
        Assert.Equal(new DateTime(2026, 4, 4), result); // Saturday April 4
    }

    // ── GetMonthStart ───────────────────────────────────────────────────

    [Fact]
    public void GetMonthStart_ReturnsFirstDayOfMonth()
    {
        var date = new DateTime(2026, 4, 15);
        var result = DateTimeHelper.GetMonthStart(date);

        Assert.Equal(new DateTime(2026, 4, 1), result);
    }

    [Fact]
    public void GetMonthStart_AlreadyFirstDay_ReturnsSame()
    {
        var date = new DateTime(2026, 4, 1);
        var result = DateTimeHelper.GetMonthStart(date);

        Assert.Equal(date, result);
    }

    // ── GetMonthEnd ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 1, 31)]
    [InlineData(2026, 2, 28)] // Non-leap year
    [InlineData(2024, 2, 29)] // Leap year
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 5, 31)]
    [InlineData(2026, 6, 30)]
    [InlineData(2026, 12, 31)]
    public void GetMonthEnd_ReturnsLastDay(int year, int month, int expectedDay)
    {
        var date = new DateTime(year, month, 15);
        var result = DateTimeHelper.GetMonthEnd(date);

        Assert.Equal(expectedDay, result.Day);
        Assert.Equal(month, result.Month);
        Assert.Equal(year, result.Year);
    }

    // ── GetCalendarGridStart ────────────────────────────────────────────

    [Fact]
    public void GetCalendarGridStart_April2026_SundayFirst()
    {
        // April 2026 starts on Wednesday. Grid should start on March 29 (Sunday)
        var result = DateTimeHelper.GetCalendarGridStart(new DateTime(2026, 4, 1), DayOfWeek.Sunday);

        Assert.Equal(DayOfWeek.Sunday, result.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 29), result);
    }

    [Fact]
    public void GetCalendarGridStart_April2026_MondayFirst()
    {
        // With Monday as first day, grid starts March 30
        var result = DateTimeHelper.GetCalendarGridStart(new DateTime(2026, 4, 1), DayOfWeek.Monday);

        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 30), result);
    }

    [Fact]
    public void GetCalendarGridStart_MonthStartingOnFirstDay()
    {
        // June 2026 starts on Monday
        var result = DateTimeHelper.GetCalendarGridStart(new DateTime(2026, 6, 1), DayOfWeek.Monday);

        Assert.Equal(new DateTime(2026, 6, 1), result);
    }

    // ── GetCalendarGridEnd ──────────────────────────────────────────────

    [Fact]
    public void GetCalendarGridEnd_Returns41DaysAfterGridStart()
    {
        var month = new DateTime(2026, 4, 1);
        var gridStart = DateTimeHelper.GetCalendarGridStart(month, DayOfWeek.Sunday);
        var gridEnd = DateTimeHelper.GetCalendarGridEnd(month, DayOfWeek.Sunday);

        Assert.Equal(gridStart.AddDays(41), gridEnd);
    }

    // ── FormatTimeRange ─────────────────────────────────────────────────

    [Fact]
    public void FormatTimeRange_AllDay_ReturnsAllDay()
    {
        var result = DateTimeHelper.FormatTimeRange(
            new DateTime(2026, 4, 5, 0, 0, 0),
            new DateTime(2026, 4, 5, 23, 59, 59),
            isAllDay: true);

        Assert.Equal("All Day", result);
    }

    [Fact]
    public void FormatTimeRange_TimedEvent_ReturnsFormattedRange()
    {
        using var culture = new CultureScope("en-US");

        var result = DateTimeHelper.FormatTimeRange(
            new DateTime(2026, 4, 5, 9, 0, 0),
            new DateTime(2026, 4, 5, 10, 30, 0),
            isAllDay: false);

        Assert.Equal("9:00 AM - 10:30 AM", result);
    }

    [Fact]
    public void FormatTimeRange_MidnightToNoon()
    {
        using var culture = new CultureScope("en-US");

        var result = DateTimeHelper.FormatTimeRange(
            new DateTime(2026, 4, 5, 0, 0, 0),
            new DateTime(2026, 4, 5, 12, 0, 0),
            isAllDay: false);

        Assert.Equal("12:00 AM - 12:00 PM", result);
    }

    [Fact]
    public void FormatTimeRange_NoonToMidnight()
    {
        using var culture = new CultureScope("en-US");

        var result = DateTimeHelper.FormatTimeRange(
            new DateTime(2026, 4, 5, 12, 0, 0),
            new DateTime(2026, 4, 5, 23, 59, 0),
            isAllDay: false);

        Assert.Equal("12:00 PM - 11:59 PM", result);
    }

    [Fact]
    public void FormatTimeRange_PMHours()
    {
        using var culture = new CultureScope("en-US");

        var result = DateTimeHelper.FormatTimeRange(
            new DateTime(2026, 4, 5, 14, 30, 0),
            new DateTime(2026, 4, 5, 16, 0, 0),
            isAllDay: false);

        Assert.Equal("2:30 PM - 4:00 PM", result);
    }

    // ── GetRelativeDate ─────────────────────────────────────────────────

    [Fact]
    public void GetRelativeDate_Today()
    {
        var result = DateTimeHelper.GetRelativeDate(DateTime.Today);
        Assert.Equal("Today", result);
    }

    [Fact]
    public void GetRelativeDate_Tomorrow()
    {
        var result = DateTimeHelper.GetRelativeDate(DateTime.Today.AddDays(1));
        Assert.Equal("Tomorrow", result);
    }

    [Fact]
    public void GetRelativeDate_Yesterday()
    {
        var result = DateTimeHelper.GetRelativeDate(DateTime.Today.AddDays(-1));
        Assert.Equal("Yesterday", result);
    }

    [Fact]
    public void GetRelativeDate_Next3Days_ReturnsDayOfWeek()
    {
        for (int i = 2; i <= 7; i++)
        {
            var date = DateTime.Today.AddDays(i);
            var result = DateTimeHelper.GetRelativeDate(date);

            Assert.Equal(date.DayOfWeek.ToString(), result);
        }
    }

    [Fact]
    public void GetRelativeDate_Last3Days_ReturnsLastDayOfWeek()
    {
        for (int i = -2; i >= -7; i--)
        {
            var date = DateTime.Today.AddDays(i);
            var result = DateTimeHelper.GetRelativeDate(date);

            Assert.Equal($"Last {date.DayOfWeek}", result);
        }
    }

    [Fact]
    public void GetRelativeDate_FarFuture_ReturnsFormattedDate()
    {
        var date = DateTime.Today.AddYears(1);
        var result = DateTimeHelper.GetRelativeDate(date);

        Assert.Equal(date.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture), result);
    }

    [Fact]
    public void GetRelativeDate_FarPast_ReturnsFormattedDate()
    {
        var date = DateTime.Today.AddYears(-1);
        var result = DateTimeHelper.GetRelativeDate(date);

        Assert.Equal(date.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture), result);
    }

    // ── IsSameDay ───────────────────────────────────────────────────────

    [Fact]
    public void IsSameDay_SameDate_ReturnsTrue()
    {
        var a = new DateTime(2026, 4, 5, 9, 0, 0);
        var b = new DateTime(2026, 4, 5, 17, 30, 0);

        Assert.True(DateTimeHelper.IsSameDay(a, b));
    }

    [Fact]
    public void IsSameDay_DifferentDate_ReturnsFalse()
    {
        var a = new DateTime(2026, 4, 5, 23, 59, 59);
        var b = new DateTime(2026, 4, 6, 0, 0, 0);

        Assert.False(DateTimeHelper.IsSameDay(a, b));
    }

    [Theory]
    [InlineData(2026, 4, 5, 2026, 4, 5, true)]
    [InlineData(2026, 4, 5, 2026, 4, 6, false)]
    [InlineData(2026, 4, 5, 2025, 4, 5, false)]
    [InlineData(2026, 1, 1, 2026, 1, 1, true)]
    [InlineData(2026, 12, 31, 2027, 1, 1, false)]
    public void IsSameDay_VariousCases(int y1, int m1, int d1, int y2, int m2, int d2, bool expected)
    {
        var a = new DateTime(y1, m1, d1);
        var b = new DateTime(y2, m2, d2);

        Assert.Equal(expected, DateTimeHelper.IsSameDay(a, b));
    }

    // ── GetHourLabels ───────────────────────────────────────────────────

    [Fact]
    public void GetHourLabels_Returns24Labels()
    {
        var labels = DateTimeHelper.GetHourLabels();

        Assert.Equal(24, labels.Count);
    }

    [Fact]
    public void GetHourLabels_FirstHour_Is12AM()
    {
        using var culture = new CultureScope("en-US");
        var labels = DateTimeHelper.GetHourLabels();

        Assert.Equal("12:00 AM", labels[0]);
    }

    [Fact]
    public void GetHourLabels_Noon_Is12PM()
    {
        using var culture = new CultureScope("en-US");
        var labels = DateTimeHelper.GetHourLabels();

        Assert.Equal("12:00 PM", labels[12]);
    }

    [Fact]
    public void GetHourLabels_LastHour_Is11PM()
    {
        using var culture = new CultureScope("en-US");
        var labels = DateTimeHelper.GetHourLabels();

        Assert.Equal("11:00 PM", labels[23]);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
