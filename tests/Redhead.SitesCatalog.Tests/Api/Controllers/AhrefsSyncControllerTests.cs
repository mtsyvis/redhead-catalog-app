using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

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
        var sut = new AhrefsSyncController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
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
        var sut = new AhrefsSyncController(service.Object);

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
        var sut = new AhrefsSyncController(service.Object);

        // Act
        var response = await sut.GetRun(Guid.NewGuid(), page, pageSize);

        // Assert
        Assert.IsType<BadRequestObjectResult>(response);
    }
}
