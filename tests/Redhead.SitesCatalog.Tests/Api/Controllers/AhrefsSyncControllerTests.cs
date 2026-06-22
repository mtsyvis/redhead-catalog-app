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
                Cron = "0 1 14 * *"
            });

        // Act
        var response = await sut.GetStatus(refresh: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<AhrefsSyncStatusResponse>(ok.Value);
        Assert.True(payload.IsDueNow);
        Assert.Equal(
            new DateTime(2026, 6, 14, 1, 0, 0, DateTimeKind.Utc),
            payload.DueOccurrenceUtc);
        Assert.Equal(
            new DateTime(2026, 7, 14, 1, 0, 0, DateTimeKind.Utc),
            payload.NextScheduledRunUtc);
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

    private static AhrefsSyncMonitoringData CreateMonitoringData(DateTime checkedAt)
        => new(
            new Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs.AhrefsLimitsAndUsage(
                1_000,
                0,
                975,
                0,
                checkedAt.AddDays(10)),
            checkedAt,
            ActiveRun: null,
            HasSuccessfulFullRunForSnapshotMonth: false,
            new DateOnly(checkedAt.Year, checkedAt.Month, 1),
            EligibleSitesCount: 10,
            FullEstimatedUnits: 120,
            ApiKeyRemainingUnits: 975,
            WorkspaceRemainingUnits: 1_000,
            AppBudgetRemainingUnits: 975,
            EffectiveAvailableUnits: 975,
            SafetyBufferUnits: 25,
            BatchSize: 100,
            MaxSitesPerRun: 100_000,
            TargetMode: "subdomains",
            Protocol: "both",
            VolumeMode: "monthly");
}
