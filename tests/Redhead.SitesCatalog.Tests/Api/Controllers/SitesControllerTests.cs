using System.Security.Claims;
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
        Assert.Null(query.TermKey);
        Assert.Null(query.Locations);
        Assert.Null(query.CategorySearchTerms);
        Assert.Null(query.CasinoAvailability);
        Assert.Null(query.CryptoAvailability);
        Assert.Null(query.LinkInsertAvailability);
        Assert.Null(query.LinkInsertCasinoAvailability);
        Assert.Null(query.DatingAvailability);
        Assert.Null(query.StopListDomains);
    }

    [Fact]
    public async Task GetFilterOptions_IncludesTermOptions()
    {
        // Arrange
        var sitesService = new Mock<ISitesService>();
        sitesService
            .Setup(service => service.GetNicheOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        sitesService
            .Setup(service => service.GetLocationFilterOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocationFilterOptionsDto
            {
                Special = new LocationSpecialFilterOptionsDto
                {
                    Unknown = new LocationFilterOptionDto
                    {
                        Key = LocationConstants.UnknownLocationKey,
                        DisplayName = "Unknown"
                    }
                }
            });
        sitesService
            .Setup(service => service.GetTermOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TermFilterOptionDto
                {
                    TermKey = "finite:1:year",
                    Label = "1 year",
                    TermType = TermType.Finite,
                    TermValue = 1,
                    TermUnit = TermUnit.Year
                }
            ]);
        var controller = new SitesController(sitesService.Object);

        // Act
        var result = await controller.GetFilterOptions(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FilterOptionsResponse>(ok.Value);
        var term = Assert.Single(response.Terms);
        Assert.Equal("finite:1:year", term.TermKey);
        Assert.Equal("1 year", term.Label);
        Assert.Equal(TermType.Finite, term.TermType);
        Assert.Equal(1, term.TermValue);
        Assert.Equal(TermUnit.Year, term.TermUnit);
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
    public async Task MultiSearch_ReturnsOrderedResultsInNormalizedInputOrder()
    {
        // Arrange
        var sitesService = new Mock<ISitesService>();
        sitesService
            .Setup(service => service.MultiSearchSitesAsync(
                It.Is<IReadOnlyList<string>>(domains => domains.SequenceEqual(new[]
                {
                    "example.com",
                    "missing.com",
                    "test.com"
                })),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MultiSearchSitesResult
            {
                Found =
                [
                    new SiteDto { Domain = "test.com", Location = "UK" },
                    new SiteDto { Domain = "example.com", Location = "US" }
                ],
                NotFound = ["missing.com"],
                Duplicates = []
            });
        var controller = new SitesController(sitesService.Object);
        SetUser(controller, AppRoles.Admin, "admin@test.com");
        var request = new MultiSearchRequest
        {
            QueryText = "https://www.Example.com/path missing.com test.com"
        };

        // Act
        var result = await controller.MultiSearch(request, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MultiSearchResponse>(ok.Value);
        Assert.Equal(["example.com", "missing.com", "test.com"], response.Results.Select(item => item.Domain).ToArray());
        Assert.True(response.Results[0].Found);
        Assert.Equal("example.com", response.Results[0].Site?.Domain);
        Assert.False(response.Results[1].Found);
        Assert.Null(response.Results[1].Site);
        Assert.True(response.Results[2].Found);
        Assert.Equal("test.com", response.Results[2].Site?.Domain);
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
    public async Task SearchSites_ClientUser_ReturnsCreatedAtAndHidesInternalAuditFields()
    {
        // Arrange
        var sitesService = new Mock<ISitesService>();
        sitesService
            .Setup(service => service.GetSitesAsync(
                It.IsAny<SitesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SitesListResult
            {
                Items =
                [
                    new()
                    {
                        Domain = "example.com",
                        CreatedAtUtc = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAtUtc = new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc),
                        CreatedBy = "creator@test.com",
                        UpdatedBy = "updater@test.com"
                    }
                ],
                Total = 1
            });
        var controller = new SitesController(sitesService.Object);
        SetUser(controller, AppRoles.Client, "client@test.com");

        // Act
        var result = await controller.SearchSites(new SitesQueryRequest(), CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SitesListResponse>(ok.Value);
        var site = Assert.Single(response.Items);
        Assert.Equal(new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc), site.CreatedAtUtc);
        Assert.Equal(default, site.UpdatedAtUtc);
        Assert.Null(site.CreatedBy);
        Assert.Null(site.UpdatedBy);
    }

    [Fact]
    public async Task SearchSites_AdminUser_ReturnsInternalAuditFields()
    {
        // Arrange
        var sitesService = new Mock<ISitesService>();
        sitesService
            .Setup(service => service.GetSitesAsync(
                It.IsAny<SitesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SitesListResult
            {
                Items =
                [
                    new()
                    {
                        Domain = "example.com",
                        CreatedAtUtc = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAtUtc = new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc),
                        CreatedBy = null,
                        UpdatedBy = "updater@test.com"
                    }
                ],
                Total = 1
            });
        var controller = new SitesController(sitesService.Object);
        SetUser(controller, AppRoles.Admin, "admin@test.com");

        // Act
        var result = await controller.SearchSites(new SitesQueryRequest(), CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SitesListResponse>(ok.Value);
        var site = Assert.Single(response.Items);
        Assert.Equal(new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc), site.UpdatedAtUtc);
        Assert.Equal("system", site.CreatedBy);
        Assert.Equal("updater@test.com", site.UpdatedBy);
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
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, AppUpdateSiteRequest, string?, CancellationToken>((_, request, _, _) => capturedRequest = request)
            .ReturnsAsync((string _, AppUpdateSiteRequest request, string? _, CancellationToken _) => new SiteDto
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
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static void SetUser(SitesController controller, string role, string email)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ClaimTypes.Role, role)
                    ],
                    "Test"))
            }
        };
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
