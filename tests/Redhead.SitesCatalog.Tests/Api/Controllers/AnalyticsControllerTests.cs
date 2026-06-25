using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class AnalyticsControllerTests
{
    [Fact]
    public void AnalyticsController_UsesAnalyticsReadAuthorizationPolicy()
    {
        // Arrange
        var controllerType = typeof(AnalyticsController);
        var methods = new[]
        {
            controllerType.GetMethod(nameof(AnalyticsController.GetBusinessDemand)),
            controllerType.GetMethod(nameof(AnalyticsController.GetExportActivity)),
            controllerType.GetMethod(nameof(AnalyticsController.GetExportLogDetails)),
            controllerType.GetMethod(nameof(AnalyticsController.GetClientOptions))
        };

        // Act
        var controllerPolicies = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToList();

        // Assert
        Assert.Contains(AppPolicies.AnalyticsReadAccess, controllerPolicies);
        Assert.DoesNotContain(AppPolicies.UsersReadAccess, controllerPolicies);
        foreach (var method in methods)
        {
            Assert.NotNull(method);
            Assert.False(method!
                .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
                .Any());
        }
    }

    [Fact]
    public async Task GetExportLogDetails_WhenLogExists_ReturnsDetails()
    {
        // Arrange
        var id = Guid.NewGuid();
        var details = new ExportLogDetailsDto(
            Id: id,
            TimestampUtc: new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            UserId: "client-1",
            Email: "client@example.com",
            DisplayName: "Client One",
            Destination: ExportConstants.DestinationDownload,
            Status: AnalyticsExportStatusLabels.Successful,
            RequestedRows: 10,
            ExportedRows: 10,
            BlockedReason: null,
            OutcomeReason: null,
            ExportMode: ExportConstants.ExportModeSites,
            AppliedFilters: [],
            Sort: new ExportLogSortDetailsDto("No sort", []),
            TechnicalDetails: null);
        var exportActivity = new Mock<IExportActivityAnalyticsService>();
        exportActivity
            .Setup(service => service.GetExportLogDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        var sut = CreateController(exportActivity);

        // Act
        var result = await sut.GetExportLogDetails(id, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(details, ok.Value);
    }

    [Fact]
    public async Task GetExportLogDetails_WhenLogDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var exportActivity = new Mock<IExportActivityAnalyticsService>();
        exportActivity
            .Setup(service => service.GetExportLogDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExportLogDetailsDto?)null);
        var sut = CreateController(exportActivity);

        // Act
        var result = await sut.GetExportLogDetails(id, CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(notFound.Value);
        Assert.Equal("Export log not found.", payload.Message);
    }

    private static AnalyticsController CreateController(
        Mock<IExportActivityAnalyticsService> exportActivity)
        => new(
            new Mock<IBusinessDemandAnalyticsService>().Object,
            exportActivity.Object);
}
