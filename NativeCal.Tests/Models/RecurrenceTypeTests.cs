using NativeCal.Models;

namespace NativeCal.Tests.Models;

public class RecurrenceTypeTests
{
    [Fact]
    public void RecurrenceType_HasExpectedValues()
    {
        Assert.Equal(0, (int)RecurrenceType.None);
        Assert.Equal(1, (int)RecurrenceType.Daily);
        Assert.Equal(2, (int)RecurrenceType.Weekly);
        Assert.Equal(3, (int)RecurrenceType.Biweekly);
        Assert.Equal(4, (int)RecurrenceType.Monthly);
        Assert.Equal(5, (int)RecurrenceType.Yearly);
    }

    [Theory]
    [InlineData("None", RecurrenceType.None)]
    [InlineData("Daily", RecurrenceType.Daily)]
    [InlineData("Weekly", RecurrenceType.Weekly)]
    [InlineData("Biweekly", RecurrenceType.Biweekly)]
    [InlineData("Monthly", RecurrenceType.Monthly)]
    [InlineData("Yearly", RecurrenceType.Yearly)]
    public void RecurrenceType_ParsesFromString(string value, RecurrenceType expected)
    {
        Assert.True(Enum.TryParse<RecurrenceType>(value, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RecurrenceType_ToString_MatchesExpected()
    {
        Assert.Equal("None", RecurrenceType.None.ToString());
        Assert.Equal("Daily", RecurrenceType.Daily.ToString());
        Assert.Equal("Weekly", RecurrenceType.Weekly.ToString());
        Assert.Equal("Biweekly", RecurrenceType.Biweekly.ToString());
        Assert.Equal("Monthly", RecurrenceType.Monthly.ToString());
        Assert.Equal("Yearly", RecurrenceType.Yearly.ToString());
    }
}
