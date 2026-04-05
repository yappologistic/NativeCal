using System.Threading.Tasks;
using NativeCal.Models;

namespace NativeCal.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var settings = new AppSettings();

        Assert.Equal(0, settings.Id);
        Assert.Equal(string.Empty, settings.Key);
        Assert.Equal(string.Empty, settings.Value);
    }

    [Theory]
    [InlineData("Theme", "0")]
    [InlineData("DefaultReminderMinutes", "15")]
    [InlineData("FirstDayOfWeek", "0")]
    public void CanStoreKnownSettingKeys(string key, string value)
    {
        var settings = new AppSettings { Key = key, Value = value };
        Assert.Equal(key, settings.Key);
        Assert.Equal(value, settings.Value);
    }
}
