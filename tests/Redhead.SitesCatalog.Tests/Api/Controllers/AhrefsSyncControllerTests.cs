using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class AhrefsSyncControllerTests
{
    [Fact]
    public void Controller_UsesSuperAdminOnlyPolicy()
    {
        // Arrange

        // Act
        var policies = typeof(AhrefsSyncController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        // Assert
        Assert.Contains(AppPolicies.SuperAdminOnly, policies);
    }

    [Theory]
    [InlineData(null, true, AhrefsSyncRunKind.ManualFull)]
    [InlineData(10, false, AhrefsSyncRunKind.ManualLimited)]
    public async Task Run_AppliesSnapshotDefaults(
        int? maxSitesOverride,
        bool expectedSaveSnapshots,
        AhrefsSyncRunKind expectedKind)
    {
        // Arrange
        AhrefsSyncRequest? captured = null;
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.RunAsync(
                It.IsAny<AhrefsSyncRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<AhrefsSyncRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(AhrefsSyncRunResult.Completed(new AhrefsSyncRun()));
        var sut = CreateController(service.Object);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        // Act
        var response = await sut.Run(
            new AhrefsSyncRunRequest(maxSitesOverride, SaveSnapshots: null),
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(response);
        Assert.NotNull(captured);
        Assert.Equal(expectedSaveSnapshots, captured!.SaveSnapshots);
        Assert.Equal(expectedKind, captured.RunKind);
    }

    [Fact]
    public async Task GetRun_PassesPaginationToService()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetRunAsync(
                runId,
                3,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AhrefsSyncRunDetails(
                new AhrefsSyncRun { Id = runId },
                [],
                3,
                25,
                0,
                0));
        var sut = CreateController(service.Object);

        // Act
        var response = await sut.GetRun(runId, page: 3, pageSize: 25);

        // Assert
        Assert.IsType<OkObjectResult>(response);
        service.VerifyAll();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public async Task GetRun_WhenPaginationIsInvalid_ReturnsBadRequest(
        int page,
        int pageSize)
    {
        // Arrange
        var service = new Mock<IAhrefsSyncService>(MockBehavior.Strict);
        var sut = CreateController(service.Object);

        // Act
        var response = await sut.GetRun(Guid.NewGuid(), page, pageSize);

        // Assert
        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public async Task GetStatus_WhenCurrentMonthIsDue_ReturnsDueNow()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(checkedAt));
        var sut = CreateController(
            service.Object,
            new AhrefsSyncOptions
            {
                Enabled = true,
                Cron = AhrefsSyncOptions.DefaultCron
            });

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.True(payload.IsDueNow);
        Assert.Equal(
            new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc),
            payload.DueOccurrenceUtc);
        Assert.Equal(
            new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc),
            payload.NextScheduledRunUtc);
    }

    [Fact]
    public async Task GetStatus_BeforeNotBeforeUtc_ReturnsFutureStartWithoutDueState()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var notBeforeUtc = new DateTimeOffset(
            2026,
            7,
            1,
            1,
            0,
            0,
            TimeSpan.Zero);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(checkedAt));
        var sut = CreateController(
            service.Object,
            new AhrefsSyncOptions
            {
                Enabled = true,
                Cron = AhrefsSyncOptions.DefaultCron,
                NotBeforeUtc = notBeforeUtc
            });

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.False(payload.IsDueNow);
        Assert.False(payload.IsWaitingForUsageReset);
        Assert.False(payload.CanStartRun);
        Assert.Equal(notBeforeUtc.UtcDateTime, payload.NotBeforeUtc);
        Assert.Equal(notBeforeUtc.UtcDateTime, payload.NextScheduledRunUtc);
    }

    [Fact]
    public async Task GetStatus_WhenSchedulerIsDisabled_ReturnsNextConfiguredOccurrence()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(checkedAt));
        var sut = CreateController(
            service.Object,
            new AhrefsSyncOptions
            {
                Enabled = false,
                Cron = AhrefsSyncOptions.DefaultCron
            });

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.False(payload.SchedulerEnabled);
        Assert.False(payload.IsDueNow);
        Assert.Equal(
            new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc),
            payload.NextScheduledRunUtc);
    }

    [Fact]
    public async Task GetStatus_WhenUsageResetIsStale_ReturnsWaitingState()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(
                checkedAt,
                usageResetDate: checkedAt.AddMinutes(-1)));
        var sut = CreateController(
            service.Object,
            new AhrefsSyncOptions
            {
                Enabled = true,
                Cron = AhrefsSyncOptions.DefaultCron
            });

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.True(payload.IsWaitingForUsageReset);
        Assert.False(payload.IsDueNow);
        Assert.False(payload.CanStartRun);
    }

    [Fact]
    public async Task GetStatus_CalculatesBudgetAndMaxSitesCapacity()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(
                checkedAt,
                eligibleSitesCount: 100,
                effectiveAvailableUnits: 145,
                safetyBufferUnits: 25,
                maxSitesPerRun: 5));
        var sut = CreateController(service.Object);

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.Equal(120, payload.SpendableUnits);
        Assert.Equal(10, payload.AffordableSitesCount);
        Assert.Equal(5, payload.PlannedSitesCount);
        Assert.Equal(60, payload.PlannedEstimatedUnits);
        Assert.True(payload.CanStartRun);
        Assert.False(payload.FullCatalogFitsBudget);
        Assert.Equal(1_080, payload.FullCatalogShortfallUnits);
        Assert.False(payload.ConfiguredRunLimitedByBudget);
        Assert.True(payload.ConfiguredRunLimitedByMaxSites);
    }

    [Fact]
    public async Task GetStatus_WhenNothingIsAffordable_ReturnsCannotStart()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(
                checkedAt,
                eligibleSitesCount: 100,
                effectiveAvailableUnits: 74,
                safetyBufferUnits: 25));
        var sut = CreateController(service.Object);

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.Equal(49, payload.SpendableUnits);
        Assert.Equal(0, payload.AffordableSitesCount);
        Assert.Equal(0, payload.PlannedSitesCount);
        Assert.Equal(0, payload.PlannedEstimatedUnits);
        Assert.False(payload.CanStartRun);
        Assert.True(payload.ConfiguredRunLimitedByBudget);
        Assert.False(payload.ConfiguredRunLimitedByMaxSites);
    }

    [Fact]
    public async Task GetStatus_WhenBudgetAllowsOnlyPartOfConfiguredRun_ReturnsBudgetLimit()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(
                checkedAt,
                eligibleSitesCount: 10,
                effectiveAvailableUnits: 85,
                safetyBufferUnits: 25));
        var sut = CreateController(service.Object);

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.Equal(60, payload.SpendableUnits);
        Assert.Equal(5, payload.AffordableSitesCount);
        Assert.Equal(5, payload.PlannedSitesCount);
        Assert.Equal(60, payload.PlannedEstimatedUnits);
        Assert.True(payload.CanStartRun);
        Assert.False(payload.FullCatalogFitsBudget);
        Assert.Equal(60, payload.FullCatalogShortfallUnits);
        Assert.True(payload.ConfiguredRunLimitedByBudget);
        Assert.False(payload.ConfiguredRunLimitedByMaxSites);
    }

    [Fact]
    public async Task GetStatus_WhenFullCatalogIsAffordable_ReturnsFullCapacity()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(
                checkedAt,
                eligibleSitesCount: 10,
                effectiveAvailableUnits: 145,
                safetyBufferUnits: 25));
        var sut = CreateController(service.Object);

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.Equal(10, payload.PlannedSitesCount);
        Assert.Equal(120, payload.PlannedEstimatedUnits);
        Assert.True(payload.FullCatalogFitsBudget);
        Assert.Equal(0, payload.FullCatalogShortfallUnits);
        Assert.False(payload.ConfiguredRunLimitedByBudget);
        Assert.False(payload.ConfiguredRunLimitedByMaxSites);
    }

    [Fact]
    public async Task GetStatus_PassesRefreshToMonitoringService()
    {
        // Arrange
        var service = new Mock<IAhrefsSyncService>();
        service.Setup(candidate => candidate.GetMonitoringDataAsync(
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringData(DateTime.UtcNow));
        var sut = CreateController(service.Object);

        // Act
        await sut.GetStatus(refresh: true);

        // Assert
        service.VerifyAll();
    }

    private static AhrefsSyncController CreateController(
        IAhrefsSyncService service,
        AhrefsSyncOptions? options = null)
        => new(service, Options.Create(options ?? new AhrefsSyncOptions()));

    private static AhrefsSyncMonitoringData CreateMonitoringData(
        DateTime checkedAt,
        int eligibleSitesCount = 10,
        long effectiveAvailableUnits = 975,
        int safetyBufferUnits = 25,
        int maxSitesPerRun = 100_000,
        DateTime? usageResetDate = null)
        => new(
            new Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs.AhrefsLimitsAndUsage(
                1_000,
                0,
                975,
                0,
                usageResetDate ?? checkedAt.AddDays(10)),
            checkedAt,
            ActiveRun: null,
            HasCompletedMonthlyRunForSnapshotMonth: false,
            IsWaitingForUsageReset:
                (usageResetDate ?? checkedAt.AddDays(10)) <= checkedAt,
            new DateOnly(checkedAt.Year, checkedAt.Month, 1),
            EligibleSitesCount: eligibleSitesCount,
            FullEstimatedUnits: AhrefsSyncCostCalculator.EstimateUnits(
                eligibleSitesCount,
                batchSize: 100),
            ApiKeyRemainingUnits: 975,
            WorkspaceRemainingUnits: 1_000,
            AppBudgetRemainingUnits: 975,
            EffectiveAvailableUnits: effectiveAvailableUnits,
            SafetyBufferUnits: safetyBufferUnits,
            BatchSize: 100,
            MaxSitesPerRun: maxSitesPerRun,
            TargetMode: "subdomains",
            Protocol: "both",
            VolumeMode: "monthly");
}
