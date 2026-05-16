using Microsoft.AspNetCore.Mvc;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;

using AppUpdateSiteRequest = Redhead.SitesCatalog.Application.Models.UpdateSiteRequest;
using ApiUpdateSiteRequest = Redhead.SitesCatalog.Api.Models.Sites.UpdateSiteRequest;

namespace Redhead.SitesCatalog.Tests;

public class SitesControllerTests
{
    [Fact]
    public void SitesQueryRequest_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var query = new SitesQueryRequest();

        // Assert
        Assert.Equal(PaginationDefaults.DefaultPage, query.Page);
        Assert.Equal(PaginationDefaults.DefaultPageSize, query.PageSize);
        Assert.Equal(SortingDefaults.DefaultSortBy, query.SortBy);
        Assert.Equal(SortingDefaults.DefaultSortDirection, query.SortDir);
        Assert.Equal(QuarantineFilterValues.All, query.Quarantine);
        Assert.Null(query.Search);
        Assert.Null(query.DrMin);
        Assert.Null(query.DrMax);
        Assert.Null(query.TrafficMin);
        Assert.Null(query.TrafficMax);
        Assert.Null(query.PriceMin);
        Assert.Null(query.PriceMax);
        Assert.Null(query.Locations);
        Assert.Null(query.CategorySearchTerms);
        Assert.Null(query.CasinoAllowed);
        Assert.Null(query.CryptoAllowed);
        Assert.Null(query.LinkInsertAllowed);
        Assert.Null(query.CasinoAvailability);
        Assert.Null(query.CryptoAvailability);
        Assert.Null(query.LinkInsertAvailability);
        Assert.Null(query.LinkInsertCasinoAvailability);
        Assert.Null(query.DatingAvailability);
        Assert.Null(query.StopListDomains);
    }

    [Fact]
    public async Task MultiSearch_WithStopList_ReturnsBadRequest_AndDoesNotCallService()
    {
        var sitesService = new Mock<ISitesService>();
        var controller = new SitesController(sitesService.Object);
        var request = new MultiSearchRequest
        {
            QueryText = "example.com",
            StopListDomains = new List<string> { "example.com" }
        };

        var result = await controller.MultiSearch(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StopListConstants.MultiSearchNotSupportedMessage, problem.Detail);
        sitesService.Verify(
            service => service.MultiSearchSitesAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MultiSearch_WithFilterStopList_ReturnsBadRequest_AndDoesNotCallService()
    {
        var sitesService = new Mock<ISitesService>();
        var controller = new SitesController(sitesService.Object);
        var request = new MultiSearchRequest
        {
            QueryText = "example.com",
            Filters = new SitesQueryRequest
            {
                StopListDomains = new List<string> { "example.com" }
            }
        };

        var result = await controller.MultiSearch(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StopListConstants.MultiSearchNotSupportedMessage, problem.Detail);
        sitesService.Verify(
            service => service.MultiSearchSitesAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchSites_WithInvalidStopList_ThrowsValidationException_AndDoesNotCallService()
    {
        var sitesService = new Mock<ISitesService>();
        var controller = new SitesController(sitesService.Object);
        var request = new SitesQueryRequest
        {
            StopListDomains = new List<string> { "example.com", "https:///path" }
        };

        var ex = await Assert.ThrowsAsync<RequestValidationException>(
            () => controller.SearchSites(request, CancellationToken.None));

        Assert.Contains("Invalid stop-list domain", ex.Message);
        sitesService.Verify(
            service => service.GetSitesAsync(
                It.IsAny<SitesQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SitesListResponse_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var response = new SitesListResponse();

        // Assert
        Assert.NotNull(response.Items);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.Total);
    }

    [Fact]
    public void SiteResponse_CanBeCreated()
    {
        // Arrange & Act
        var site = new SiteResponse
        {
            Domain = "example.com",
            DR = 50,
            Traffic = 10000,
            Location = "US",
            Language = "EN",
            PriceUsd = 100.50m,
            PriceCasino = 150.00m,
            PriceCrypto = null,
            PriceLinkInsert = 75.00m,
            PriceLinkInsertCasino = 85.00m,
            PriceDating = null,
            NumberDFLinks = 2,
            Niche = "Tech",
            Categories = "Technology, News",
            IsQuarantined = false,
            QuarantineReason = null,
            QuarantineUpdatedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("example.com", site.Domain);
        Assert.Equal(50, site.DR);
        Assert.Equal(10000, site.Traffic);
        Assert.Equal("US", site.Location);
        Assert.Equal("EN", site.Language);
        Assert.Equal(100.50m, site.PriceUsd);
        Assert.Equal(150.00m, site.PriceCasino);
        Assert.Null(site.PriceCrypto);
        Assert.Equal(75.00m, site.PriceLinkInsert);
        Assert.Equal(85.00m, site.PriceLinkInsertCasino);
        Assert.Null(site.PriceDating);
        Assert.Equal(2, site.NumberDFLinks);
        Assert.False(site.IsQuarantined);
    }

    [Fact]
    public async Task UpdateSite_WithLanguage_NormalizesBeforeCallingService()
    {
        var sitesService = new Mock<ISitesService>();
        AppUpdateSiteRequest? capturedRequest = null;
        sitesService
            .Setup(service => service.UpdateSiteAsync(
                "example.com",
                It.IsAny<AppUpdateSiteRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, AppUpdateSiteRequest, CancellationToken>((_, request, _) => capturedRequest = request)
            .ReturnsAsync((string _, AppUpdateSiteRequest request, CancellationToken _) => new SiteDto
            {
                Domain = "example.com",
                DR = request.DR,
                Traffic = request.Traffic,
                Location = request.Location,
                Language = request.Language,
                PriceUsd = request.PriceUsd,
                IsQuarantined = request.IsQuarantined,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        var controller = new SitesController(sitesService.Object);
        var request = BuildValidUpdateRequest();
        request.Language = "english";

        var result = await controller.UpdateSite("example.com", request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SiteResponse>(ok.Value);
        Assert.Equal("EN", response.Language);
        Assert.NotNull(capturedRequest);
        Assert.Equal("EN", capturedRequest.Language);
    }

    [Fact]
    public async Task UpdateSite_WithInvalidLanguage_ReturnsValidationErrorAndDoesNotCallService()
    {
        var sitesService = new Mock<ISitesService>();
        var controller = new SitesController(sitesService.Object);
        var request = BuildValidUpdateRequest();
        request.Language = "english-us";

        var result = await controller.UpdateSite("example.com", request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        sitesService.Verify(
            service => service.UpdateSiteAsync(
                It.IsAny<string>(),
                It.IsAny<AppUpdateSiteRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ApiUpdateSiteRequest BuildValidUpdateRequest()
    {
        return new ApiUpdateSiteRequest
        {
            DR = 50,
            Traffic = 10000,
            Location = "US",
            PriceUsd = 100m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
            IsQuarantined = false
        };
    }
}
