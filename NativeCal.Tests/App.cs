using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NativeCal.Services;

namespace NativeCal;

public static class App
{
    public static DatabaseService Database { get; set; } = null!;
    public static HolidayService HolidayService { get; set; } = new HolidayService((_, _) => Task.FromResult<IReadOnlyList<HolidayService.HolidayRecord>>(Array.Empty<HolidayService.HolidayRecord>()));
}
