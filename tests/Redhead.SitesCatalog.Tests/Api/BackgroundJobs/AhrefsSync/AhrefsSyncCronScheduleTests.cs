using Redhead.SitesCatalog.Api.BackgroundJobs.AhrefsSync;
using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Redhead.SitesCatalog.Tests.Api.BackgroundJobs.AhrefsSync;

public sealed class AhrefsSyncCronScheduleTests
{
    [Fact]
    public void DefaultCron_RunsAt0100UtcOnFirst()
    {
        // Arrange
        var now = new DateTime(2026, 7, 1, 2, 0, 0, DateTimeKind.Utc);

        // Act
        var parsed = AhrefsSyncCronSchedule.TryParse(AhrefsSyncOptions.DefaultCron, out var schedule);
        var due = schedule.GetDueOccurrenceUtc(now);
        var next = schedule.GetNextOccurrenceUtc(now);

        // Assert
        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc), due);
        Assert.Equal(new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), next);
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

    [Fact]
    public void ShouldRetry_WhenWaitingForUsageReset_ReturnsTrue()
    {
        // Arrange
        var result = AhrefsSyncRunResult.WaitForUsageReset(DateTime.UtcNow);

        // Act
        var shouldRetry = AhrefsSyncHostedService.ShouldRetry(result);

        // Assert
        Assert.True(shouldRetry);
    }

    [Fact]
    public async Task RunLoop_BeforeNotBeforeUtc_WaitsWithoutStartingSync()
    {
        // Arrange
        var currentUtc = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);
        var notBeforeUtc = new DateTimeOffset(
            2026,
            7,
            1,
            1,
            0,
            0,
            TimeSpan.Zero);
        var syncService = new Mock<IAhrefsSyncService>();
        var services = new ServiceCollection();
        services.AddScoped(_ => syncService.Object);
        await using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        TimeSpan? capturedDelay = null;
        Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            capturedDelay = delay;
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }

        var sut = new AhrefsSyncHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AhrefsSyncOptions
            {
                Enabled = true,
                Cron = AhrefsSyncOptions.DefaultCron,
                NotBeforeUtc = notBeforeUtc
            }),
            NullLogger<AhrefsSyncHostedService>.Instance,
            () => currentUtc,
            Delay);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.RunLoopAsync(cancellation.Token));

        // Assert
        Assert.Equal(notBeforeUtc.UtcDateTime - currentUtc, capturedDelay);
        syncService.Verify(
            service => service.RunAsync(
                It.IsAny<AhrefsSyncRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunLoop_WhenUsageHasNotReset_RetriesAfterHourThenCompletesRun()
    {
        // Arrange
        var currentUtc = new DateTime(2026, 7, 1, 2, 0, 0, DateTimeKind.Utc);
        var delays = new List<TimeSpan>();
        using var cancellation = new CancellationTokenSource();
        var syncService = new Mock<IAhrefsSyncService>();

        // The first scheduled attempt sees the previous Ahrefs usage period and must wait.
        // The second attempt simulates Ahrefs confirming the reset and completing the sync.
        syncService.SetupSequence(service => service.RunAsync(
                It.IsAny<AhrefsSyncRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AhrefsSyncRunResult.WaitForUsageReset(
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)))
            .ReturnsAsync(AhrefsSyncRunResult.Completed(new AhrefsSyncRun
            {
                Id = Guid.NewGuid(),
                Status = AhrefsSyncRunStatus.Succeeded,
                RunKind = AhrefsSyncRunKind.Scheduled
            }));
        var services = new ServiceCollection();
        services.AddScoped(_ => syncService.Object);
        await using var provider = services.BuildServiceProvider();
        var delayCalls = 0;
        Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            delays.Add(delay);
            delayCalls++;
            if (delayCalls == 1)
            {
                // Advance the fake clock instead of making the unit test wait for a real hour.
                currentUtc = currentUtc.Add(delay);
                return Task.CompletedTask;
            }

            // After the successful retry, stop the otherwise infinite background-service loop.
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }

        var sut = new AhrefsSyncHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AhrefsSyncOptions
            {
                Enabled = true,
                Cron = AhrefsSyncOptions.DefaultCron
            }),
            NullLogger<AhrefsSyncHostedService>.Instance,
            () => currentUtc,
            Delay);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.RunLoopAsync(cancellation.Token));

        // Assert
        // Waiting for the reset uses the configured hourly retry interval.
        Assert.Equal(TimeSpan.FromHours(1), delays[0]);

        // One call observes the stale period; the next call performs the scheduled sync.
        syncService.Verify(service => service.RunAsync(
                It.Is<AhrefsSyncRequest>(request =>
                    request.RunKind == AhrefsSyncRunKind.Scheduled &&
                    request.SaveSnapshots),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
