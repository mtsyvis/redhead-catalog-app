using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Options;

public sealed class LinkDetectiveSyncOptionsTests
{
    [Theory]
    [InlineData("00:10", 500, 10000, 3, true)]
    [InlineData("23:59", 1, 0, 1, true)]
    [InlineData("24:00", 500, 10000, 3, false)]
    [InlineData("00:10", 501, 10000, 3, true)]
    [InlineData("00:10", 1000, 10000, 3, true)]
    [InlineData("00:10", 0, 10000, 3, false)]
    [InlineData("00:10", -1, 10000, 3, false)]
    [InlineData("00:10", 500, -1, 3, false)]
    [InlineData("00:10", 500, 10000, 0, false)]
    public void IsValid_ValidatesScheduleBatchReserveAndAttempts(string time, int batch, int reserve, int attempts, bool expected)
    {
        // Arrange
        var options = new LinkDetectiveSyncOptions { DailySelectionTimeUtc = time, BatchSize = batch, OnDemandReserve = reserve, MaxAttempts = attempts };

        // Act
        var result = LinkDetectiveSyncOptions.IsValid(options);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(0, 10, 4)]
    [InlineData(23, 59, 4)]
    public void NextSelection_UsesNextDailyUtcOccurrence(int hour, int minute, int day)
    {
        // Arrange
        var now = new DateTime(2026, 11, 3, hour, minute, 0, DateTimeKind.Utc);

        // Act
        var result = new LinkDetectiveSyncOptions().NextSelection(now);

        // Assert
        Assert.Equal(new DateTime(2026, 11, day, 0, 10, 0, DateTimeKind.Utc), result);
    }
}
