using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NativeCal.Services;

namespace NativeCal;

/// <summary>
/// Test-only App class that mirrors the real App's static members
/// without requiring a WinUI Application host.
/// </summary>
public static class App
{
    public static DatabaseService Database { get; set; } = null!;
    public static HolidayService HolidayService { get; set; } = new HolidayService((_, _) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(Array.Empty<HolidayService.HolidayRecord>()));

    /// <summary>
    /// First day of week setting used by ViewModels and Helpers.
    /// Mirrors the real App.FirstDayOfWeek property.
    /// Defaults to Sunday.
    /// </summary>
    public static DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;
}
