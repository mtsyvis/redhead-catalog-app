using Redhead.SitesCatalog.Application.SystemJobs;

namespace Redhead.SitesCatalog.Tests.Application.SystemJobs;

public sealed class SystemJobPeriodKeyTests
{
    [Theory]
    [InlineData("2026-06-01T00:00:00Z", "2026-W23")]
    [InlineData("2021-01-01T00:00:00Z", "2020-W53")]
    [InlineData("2021-01-04T00:00:00Z", "2021-W01")]
    public void FromUtcWeek_UsesIsoWeekPeriodKeys(string value, string expected)
    {
        // Arrange
        var utc = DateTime.Parse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        // Act
        var result = SystemJobPeriodKey.FromUtcWeek(utc);

        // Assert
        Assert.Equal(expected, result);
    }
}
