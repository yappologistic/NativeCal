using System;
using System.Globalization;
using NativeCal.Helpers;

namespace NativeCal.Tests.Helpers;

/// <summary>
/// Additional helper tests for localization-sensitive formatting and
/// new-event draft defaults introduced during the polish pass.
/// </summary>
public class DateTimePolishTests
{
    [Fact]
    public void GetDayOfWeekHeaders_UsesCultureAndConfiguredFirstDay()
    {
        using var culture = new CultureScope("fr-FR");

        var headers = DateTimeHelper.GetDayOfWeekHeaders(DayOfWeek.Monday);

        Assert.Equal(7, headers.Count);
        Assert.Equal(CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(DayOfWeek.Monday), headers[0]);
        Assert.Equal(CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(DayOfWeek.Sunday), headers[6]);
    }

    [Fact]
    public void GetHourLabels_UsesCurrentCultureShortTimePattern()
    {
        using var culture = new CultureScope("fr-FR");

        var labels = DateTimeHelper.GetHourLabels();

        Assert.Equal(24, labels.Count);
        Assert.Equal(DateTime.Today.ToString("t", CultureInfo.CurrentCulture), labels[0]);
        Assert.Equal(DateTime.Today.AddHours(13).ToString("t", CultureInfo.CurrentCulture), labels[13]);
    }

    [Fact]
    public void FormatTimeRange_UsesCurrentCultureShortTimePattern()
    {
        using var culture = new CultureScope("fr-FR");

        var result = DateTimeHelper.FormatTimeRange(
            new DateTime(2026, 4, 5, 13, 0, 0),
            new DateTime(2026, 4, 5, 14, 30, 0),
            isAllDay: false);

        Assert.Equal("13:00 - 14:30", result);
    }

    [Fact]
    public void GetDefaultEventStart_NoSelection_RoundsCurrentTimeUp()
    {
        DateTime now = new DateTime(2026, 4, 5, 10, 5, 33);

        DateTime result = DateTimeHelper.GetDefaultEventStart(selectedDate: null, now);

        Assert.Equal(new DateTime(2026, 4, 5, 10, 30, 0), result);
    }

    [Fact]
    public void GetDefaultEventStart_DateOnlyToday_UsesRoundedCurrentTime()
    {
        DateTime now = new DateTime(2026, 4, 5, 14, 12, 0);

        DateTime result = DateTimeHelper.GetDefaultEventStart(new DateTime(2026, 4, 5), now);

        Assert.Equal(new DateTime(2026, 4, 5, 14, 30, 0), result);
    }

    [Fact]
    public void GetDefaultEventStart_DateOnlyFutureDay_UsesMorningDefault()
    {
        DateTime now = new DateTime(2026, 4, 5, 14, 12, 0);

        DateTime result = DateTimeHelper.GetDefaultEventStart(new DateTime(2026, 4, 9), now);

        Assert.Equal(new DateTime(2026, 4, 9, 9, 0, 0), result);
    }

    [Fact]
    public void GetDefaultEventStart_ExplicitTime_IsPreserved()
    {
        DateTime now = new DateTime(2026, 4, 5, 14, 12, 0);
        DateTime explicitDateTime = new DateTime(2026, 4, 9, 16, 45, 0);

        DateTime result = DateTimeHelper.GetDefaultEventStart(explicitDateTime, now);

        Assert.Equal(explicitDateTime, result);
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
