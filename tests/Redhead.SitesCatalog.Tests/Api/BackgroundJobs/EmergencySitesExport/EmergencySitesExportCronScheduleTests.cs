using Redhead.SitesCatalog.Api.BackgroundJobs.EmergencySitesExport;

namespace Redhead.SitesCatalog.Tests.Api.BackgroundJobs.EmergencySitesExport;

public sealed class EmergencySitesExportCronScheduleTests
{
    [Fact]
    public void TryParse_WithDefaultSchedule_ReturnsMonday0330Utc()
    {
        // Arrange
        var cron = "30 3 * * MON";
        var utcNow = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var parsed = EmergencySitesExportCronSchedule.TryParse(cron, out var schedule);
        var dueOccurrenceUtc = schedule.GetDueOccurrenceUtc(utcNow);
        var nextOccurrenceUtc = schedule.GetNextOccurrenceUtc(utcNow);

        // Assert
        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 6, 1, 3, 30, 0, DateTimeKind.Utc), dueOccurrenceUtc);
        Assert.Equal(new DateTime(2026, 6, 8, 3, 30, 0, DateTimeKind.Utc), nextOccurrenceUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0 30 3 ? * MON")]
    [InlineData("30 25 * * MON")]
    public void TryParse_WithUnsupportedSchedule_ReturnsFalse(string cron)
    {
        // Arrange

        // Act
        var parsed = EmergencySitesExportCronSchedule.TryParse(cron, out _);

        // Assert
        Assert.False(parsed);
    }
}
