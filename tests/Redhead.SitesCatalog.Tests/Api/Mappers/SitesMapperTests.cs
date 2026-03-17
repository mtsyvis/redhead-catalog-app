using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Tests;

public class SitesMapperTests
{
    [Fact]
    public void ToQuery_MapsAllProperties_Correctly()
    {
        // Arrange
        var request = new SitesQueryRequest
        {
            Page = 2,
            PageSize = 50,
            SortBy = SortFields.Traffic,
            SortDir = SortingDefaults.Descending,
            Search = "example.com",
            DrMin = 50,
            DrMax = 90,
            TrafficMin = 10000,
            TrafficMax = 50000,
            PriceMin = 100m,
            PriceMax = 500m,
            Locations = new List<string> { "US", "UK" },
            CasinoAllowed = true,
            CryptoAllowed = false,
            LinkInsertAllowed = true,
            CasinoAvailability = "notAvailable",
            CryptoAvailability = "unknown",
            LinkInsertAvailability = "available",
            Quarantine = QuarantineFilterValues.Exclude
        };

        // Act
        var query = SitesMapper.ToQuery(request);

        // Assert
        Assert.Equal(2, query.Page);
        Assert.Equal(50, query.PageSize);
        Assert.Equal(SortFields.Traffic, query.SortBy);
        Assert.Equal(SortingDefaults.Descending, query.SortDir);
        Assert.Equal("example.com", query.Search);
        Assert.Equal(50, query.DrMin);
        Assert.Equal(90, query.DrMax);
        Assert.Equal(10000, query.TrafficMin);
        Assert.Equal(50000, query.TrafficMax);
        Assert.Equal(100m, query.PriceMin);
        Assert.Equal(500m, query.PriceMax);
        Assert.Equal(2, query.Locations!.Count);
        Assert.Contains("US", query.Locations);
        Assert.Contains("UK", query.Locations);
        Assert.True(query.CasinoAllowed);
        Assert.False(query.CryptoAllowed);
        Assert.True(query.LinkInsertAllowed);
        Assert.Equal(ServiceAvailabilityFilter.NotAvailable, query.CasinoAvailability);
        Assert.Equal(ServiceAvailabilityFilter.Unknown, query.CryptoAvailability);
        Assert.Equal(ServiceAvailabilityFilter.Available, query.LinkInsertAvailability);
        Assert.Equal(QuarantineFilterValues.Exclude, query.Quarantine);
    }

    [Fact]
    public void ToQuery_WithNullOptionalFields_MapsCorrectly()
    {
        // Arrange
        var request = new SitesQueryRequest
        {
            Page = 1,
            PageSize = 25,
            SortBy = null,
            SortDir = null,
            Search = null,
            Locations = null,
            CasinoAllowed = null,
            CryptoAllowed = null,
            LinkInsertAllowed = null
        };

        // Act
        var query = SitesMapper.ToQuery(request);

        // Assert
        Assert.Equal(1, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal(string.Empty, query.SortBy);
        Assert.Equal(string.Empty, query.SortDir);
        Assert.Null(query.Search);
        Assert.Null(query.Locations);
        Assert.Null(query.CasinoAllowed);
        Assert.Null(query.CryptoAllowed);
        Assert.Null(query.LinkInsertAllowed);
    }

    [Fact]
    public void ToSiteResponse_MapsAllProperties_Correctly()
    {
        // Arrange
        var dto = new SiteDto
        {
            Domain = "example.com",
            DR = 70,
            Traffic = 50000,
            Location = "US",
            PriceUsd = 200m,
            PriceCasino = 250m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Available,
            PriceCrypto = 220m,
            PriceCryptoStatus = ServiceAvailabilityStatus.Available,
            PriceLinkInsert = 180m,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
            Niche = "Tech",
            Categories = "Technology, News",
            IsQuarantined = true,
            QuarantineReason = "Under review",
            QuarantineUpdatedAtUtc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var response = SitesMapper.ToSiteResponse(dto);

        // Assert
        Assert.Equal("example.com", response.Domain);
        Assert.Equal(70, response.DR);
        Assert.Equal(50000, response.Traffic);
        Assert.Equal("US", response.Location);
        Assert.Equal(200m, response.PriceUsd);
        Assert.Equal(250m, response.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, response.PriceCasinoStatus);
        Assert.Equal(220m, response.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Available, response.PriceCryptoStatus);
        Assert.Equal(180m, response.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Available, response.PriceLinkInsertStatus);
        Assert.Equal("Tech", response.Niche);
        Assert.Equal("Technology, News", response.Categories);
        Assert.True(response.IsQuarantined);
        Assert.Equal("Under review", response.QuarantineReason);
        Assert.Equal(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc), response.QuarantineUpdatedAtUtc);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), response.CreatedAtUtc);
        Assert.Equal(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc), response.UpdatedAtUtc);
    }

    [Fact]
    public void ToSiteResponse_WithNullableFields_MapsCorrectly()
    {
        // Arrange
        var dto = new SiteDto
        {
            Domain = "basic.com",
            DR = 50,
            Traffic = 10000,
            Location = "UK",
            PriceUsd = 100m,
            PriceCasino = null,
            PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceCrypto = null,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsert = null,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            Niche = null,
            Categories = null,
            IsQuarantined = false,
            QuarantineReason = null,
            QuarantineUpdatedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        // Act
        var response = SitesMapper.ToSiteResponse(dto);

        // Assert
        Assert.Equal("basic.com", response.Domain);
        Assert.Null(response.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, response.PriceCasinoStatus);
        Assert.Null(response.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, response.PriceCryptoStatus);
        Assert.Null(response.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, response.PriceLinkInsertStatus);
        Assert.Null(response.Niche);
        Assert.Null(response.Categories);
        Assert.False(response.IsQuarantined);
        Assert.Null(response.QuarantineReason);
        Assert.Null(response.QuarantineUpdatedAtUtc);
    }

    [Fact]
    public void ToQuery_WithInvalidAvailability_ThrowsRequestValidationException()
    {
        // Arrange
        var request = new SitesQueryRequest
        {
            CasinoAvailability = "badValue"
        };

        // Act
        var ex = Assert.Throws<RequestValidationException>(() => SitesMapper.ToQuery(request));

        // Assert
        Assert.Contains("Invalid availability filter value", ex.Message);
    }

    [Fact]
    public void ToResponse_MapsListAndTotal_Correctly()
    {
        // Arrange
        var result = new SitesListResult
        {
            Items = new List<SiteDto>
            {
                new()
                {
                    Domain = "site1.com",
                    DR = 50,
                    Traffic = 10000,
                    Location = "US",
                    PriceUsd = 100m,
                    IsQuarantined = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Domain = "site2.com",
                    DR = 60,
                    Traffic = 20000,
                    Location = "UK",
                    PriceUsd = 150m,
                    IsQuarantined = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            },
            Total = 100
        };

        // Act
        var response = SitesMapper.ToResponse(result);

        // Assert
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(100, response.Total);
        Assert.Equal("site1.com", response.Items[0].Domain);
        Assert.Equal("site2.com", response.Items[1].Domain);
        Assert.Equal(50, response.Items[0].DR);
        Assert.Equal(60, response.Items[1].DR);
    }

    [Fact]
    public void ToResponse_WithEmptyList_MapsCorrectly()
    {
        // Arrange
        var result = new SitesListResult
        {
            Items = new List<SiteDto>(),
            Total = 0
        };

        // Act
        var response = SitesMapper.ToResponse(result);

        // Assert
        Assert.Empty(response.Items);
        Assert.Equal(0, response.Total);
    }
}
