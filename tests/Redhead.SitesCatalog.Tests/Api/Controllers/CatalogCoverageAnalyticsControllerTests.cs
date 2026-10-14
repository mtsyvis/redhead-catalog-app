using Microsoft.AspNetCore.Mvc;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services.Analytics.CatalogCoverageAnalytics;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class CatalogCoverageAnalyticsControllerTests
{
    [Fact]
    public async Task Get_ReturnsCurrentSummary()
    {
        // Arrange
        var report = new CatalogCoverageAnalyticsDto(10, 8, 2, 80, 12, 10, 2, 1, 2, 1, DateTime.UtcNow);
        var service = new Mock<ICatalogCoverageAnalyticsService>();
        service.Setup(item => item.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(report);
        var controller = new CatalogCoverageAnalyticsController(service.Object);

        // Act
        var result = await controller.Get(CancellationToken.None);

        // Assert
        Assert.Same(report, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Export_ReturnsExcelDownloadWithDatedFilename()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3]);
        var service = new Mock<ICatalogCoverageAnalyticsService>();
        service.Setup(item => item.ExportDomainsWithoutOffersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stream);
        var controller = new CatalogCoverageAnalyticsController(service.Object);

        // Act
        var result = await controller.ExportDomainsWithoutOffers(CancellationToken.None);

        // Assert
        Assert.Same(stream, result.FileStream);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        Assert.Matches(@"^catalog-domains-without-offers-\d{8}-\d{6}\.xlsx$", result.FileDownloadName);
    }
}
