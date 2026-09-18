using Redhead.SitesCatalog.Application.Services.Analytics.MissingDomainsAnalytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models.Analytics;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class MissingDomainsAnalyticsControllerTests
{
    [Fact]
    public void Controller_RequiresOnlyIndependentMissingDomainsPolicy()
    {
        // Arrange
        var controllerType = typeof(MissingDomainsAnalyticsController);

        // Act
        var policies = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Select(x => x.Policy);

        // Assert
        Assert.Equal([AppPolicies.MissingDomainsAnalyticsReadAccess], policies);
    }

    [Theory]
    [InlineData("invalid-date")]
    [InlineData("reversed")]
    [InlineData("max-date")]
    [InlineData("mixed-all-time")]
    [InlineData("role")]
    [InlineData("status")]
    [InlineData("page")]
    [InlineData("page-size")]
    [InlineData("overflow")]
    [InlineData("domain-length")]
    public async Task Get_InvalidFilters_ReturnsBadRequest(string scenario)
    {
        // Arrange
        var request = new MissingDomainsAnalyticsRequest { From = "2026-09-01", To = "2026-09-16" };
        switch (scenario)
        {
            case "invalid-date": request.From = "not-a-date"; break;
            case "reversed": request.From = "2026-09-17"; break;
            case "max-date": request.To = "9999-12-31"; break;
            case "mixed-all-time": request.AllTime = true; break;
            case "role": request.Role = "Admin"; break;
            case "status": request.CatalogStatus = "invalid"; break;
            case "page": request.Page = 0; break;
            case "page-size": request.PageSize = 999; break;
            case "overflow": request.Page = int.MaxValue; break;
            case "domain-length": request.Domain = new string('x', 254); break;
        }
        var controller = new MissingDomainsAnalyticsController(Mock.Of<IMissingDomainsAnalyticsService>());

        // Act
        var result = await controller.Get(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_MapsDatesFiltersAndPagination(bool allTime)
    {
        // Arrange
        MissingDomainsAnalyticsQuery? captured = null;
        var service = new Mock<IMissingDomainsAnalyticsService>();
        service.Setup(x => x.GetAsync(It.IsAny<MissingDomainsAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<MissingDomainsAnalyticsQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync(new MissingDomainsAnalyticsDto(0, 0, 0, [], 2, 10));
        var controller = new MissingDomainsAnalyticsController(service.Object);

        // Act
        var result = await controller.Get(new MissingDomainsAnalyticsRequest
        {
            AllTime = allTime, From = allTime ? null : "2026-09-01", To = allTime ? null : "2026-09-16",
            Role = " lite ", CatalogStatus = "added", Domain = "https://www.Example.com/path", Page = 2, PageSize = 10
        }, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(captured);
        Assert.Equal(allTime ? null : new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), captured.FromUtc);
        Assert.Equal(allTime ? null : new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc), captured.ToUtc);
        Assert.Equal(AppRoles.Lite, captured.Role);
        Assert.True(captured.IsInCatalog);
        Assert.Equal("example.com", captured.Domain);
        Assert.Equal(2, captured.Page);
        Assert.Equal(10, captured.PageSize);
    }
}
