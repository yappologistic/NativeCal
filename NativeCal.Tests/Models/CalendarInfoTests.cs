using System;
using System.Threading.Tasks;
using NativeCal.Models;

namespace NativeCal.Tests.Models;

public class CalendarInfoTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var cal = new CalendarInfo();

        Assert.Equal(0, cal.Id);
        Assert.Equal(string.Empty, cal.Name);
        Assert.Equal("#4A90D9", cal.ColorHex);
        Assert.True(cal.IsVisible);
        Assert.False(cal.IsDefault);
        Assert.True(cal.CreatedAt <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData("#4A90D9")]
    [InlineData("#E74C3C")]
    [InlineData("#27AE60")]
    public void ColorHex_AcceptsValidHex(string hex)
    {
        var cal = new CalendarInfo { ColorHex = hex };
        Assert.Equal(hex, cal.ColorHex);
    }
}
