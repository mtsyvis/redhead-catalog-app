using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Domain.Constants;

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
        Assert.Null(query.CasinoAllowed);
        Assert.Null(query.CryptoAllowed);
        Assert.Null(query.LinkInsertAllowed);
        Assert.Null(query.CasinoAvailability);
        Assert.Null(query.CryptoAvailability);
        Assert.Null(query.LinkInsertAvailability);
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
            PriceUsd = 100.50m,
            PriceCasino = 150.00m,
            PriceCrypto = null,
            PriceLinkInsert = 75.00m,
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
        Assert.Equal(100.50m, site.PriceUsd);
        Assert.Equal(150.00m, site.PriceCasino);
        Assert.Null(site.PriceCrypto);
        Assert.Equal(75.00m, site.PriceLinkInsert);
        Assert.False(site.IsQuarantined);
    }
}
