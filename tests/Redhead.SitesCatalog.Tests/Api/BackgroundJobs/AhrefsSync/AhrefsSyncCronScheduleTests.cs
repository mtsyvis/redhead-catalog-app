using Redhead.SitesCatalog.Api.BackgroundJobs.AhrefsSync;
using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Api.BackgroundJobs.AhrefsSync;

public sealed class AhrefsSyncCronScheduleTests
{
    [Fact]
    public void DefaultCron_RunsAt0100UtcOnFourteenth()
    {
        // Arrange
        var now = new DateTime(2026, 7, 14, 2, 0, 0, DateTimeKind.Utc);

        // Act
        var parsed = AhrefsSyncCronSchedule.TryParse("0 1 14 * *", out var schedule);
        var due = schedule.GetDueOccurrenceUtc(now);
        var next = schedule.GetNextOccurrenceUtc(now);

        // Assert
        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 7, 14, 1, 0, 0, DateTimeKind.Utc), due);
        Assert.Equal(new DateTime(2026, 8, 14, 1, 0, 0, DateTimeKind.Utc), next);
    }

    [Theory]
    [InlineData(AhrefsSyncRunStatus.Failed, true)]
    [InlineData(AhrefsSyncRunStatus.Succeeded, false)]
    [InlineData(AhrefsSyncRunStatus.SucceededPartial, false)]
    [InlineData(AhrefsSyncRunStatus.SkippedInsufficientUnits, false)]
    public void ShouldRetry_DependsOnTerminalRunStatus(
        AhrefsSyncRunStatus status,
        bool expected)
    {
        // Arrange
        var result = AhrefsSyncRunResult.Completed(new AhrefsSyncRun { Status = status });

        // Act
        var shouldRetry = AhrefsSyncHostedService.ShouldRetry(result);

        // Assert
        Assert.Equal(expected, shouldRetry);
    }

    [Fact]
    public void ShouldRetry_WhenAnotherRunOwnsTheLock_ReturnsTrue()
    {
        // Arrange
        var result = AhrefsSyncRunResult.AlreadyRunning();

        // Act
        var shouldRetry = AhrefsSyncHostedService.ShouldRetry(result);

        // Assert
        Assert.True(shouldRetry);
    }
}
