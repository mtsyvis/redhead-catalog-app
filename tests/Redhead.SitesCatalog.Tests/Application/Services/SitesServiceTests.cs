using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests;

public class SitesServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SitesService _service;

    public SitesServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var queryBuilder = new SitesQueryBuilder();
        _service = new SitesService(_context, queryBuilder);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        var sites = new List<Site>
        {
            new()
            {
                Domain = "example.com",
                DR = 50,
                Traffic = 10000,
                Location = "US",
                PriceUsd = 100m,
                PriceCasino = 150m,
                PriceCrypto = 120m,
                PriceLinkInsert = 80m,
                Niche = "Tech",
                Categories = "Technology",
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Domain = "test.com",
                DR = 70,
                Traffic = 50000,
                Location = "UK",
                PriceUsd = 200m,
                PriceCasino = null,
                PriceCrypto = 180m,
                PriceLinkInsert = null,
                Niche = "News",
                Categories = "News",
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Domain = "gambling.com",
                DR = 90,
                Traffic = 100000,
                Location = "US",
                PriceUsd = 500m,
                PriceCasino = 600m,
                PriceCrypto = null,
                PriceLinkInsert = 400m,
                Niche = "Casino",
                Categories = "Gambling",
                IsQuarantined = true,
                QuarantineReason = "Under review",
                QuarantineUpdatedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Domain = "crypto.com",
                DR = 60,
                Traffic = 30000,
                Location = "CA",
                PriceUsd = 150m,
                PriceCasino = null,
                PriceCrypto = 170m,
                PriceLinkInsert = null,
                Niche = "Crypto",
                Categories = "Cryptocurrency",
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Domain = "lowdr.com",
                DR = 20,
                Traffic = 1000,
                Location = "US",
                PriceUsd = 50m,
                PriceCasino = null,
                PriceCrypto = null,
                PriceLinkInsert = 30m,
                Niche = "General",
                Categories = "General",
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        };

        _context.Sites.AddRange(sites);
        _context.SaveChanges();
    }

    #region Search Tests

    [Fact]
    public async Task GetSitesAsync_WithSearchFilter_ReturnsMatchingSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Search = "example",
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("example.com", result.Items[0].Domain);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_WithSearchNormalization_ReturnsMatchingSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Search = "https://www.Example.COM/path?q=1",
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("example.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_WithNoSearchMatch_ReturnsEmpty()
    {
        // Arrange
        var query = new SitesQuery
        {
            Search = "nonexistent",
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    #endregion

    #region Range Filter Tests

    [Fact]
    public async Task GetSitesAsync_WithDrMinFilter_ReturnsFilteredSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            DrMin = 60,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // test.com(70), gambling.com(90), crypto.com(60)
        Assert.All(result.Items, site => Assert.True(site.DR >= 60));
    }

    [Fact]
    public async Task GetSitesAsync_WithDrMaxFilter_ReturnsFilteredSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            DrMax = 60,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // example.com(50), crypto.com(60), lowdr.com(20)
        Assert.All(result.Items, site => Assert.True(site.DR <= 60));
    }

    [Fact]
    public async Task GetSitesAsync_WithDrRange_ReturnsFilteredSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            DrMin = 50,
            DrMax = 70,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // example.com(50), test.com(70), crypto.com(60)
        Assert.All(result.Items, site => Assert.True(site.DR >= 50 && site.DR <= 70));
    }

    [Fact]
    public async Task GetSitesAsync_WithTrafficRange_ReturnsFilteredSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            TrafficMin = 10000,
            TrafficMax = 50000,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // example.com, test.com, crypto.com
        Assert.All(result.Items, site => Assert.True(site.Traffic >= 10000 && site.Traffic <= 50000));
    }

    [Fact]
    public async Task GetSitesAsync_WithPriceRange_ReturnsFilteredSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            PriceMin = 100m,
            PriceMax = 200m,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // example.com(100), test.com(200), crypto.com(150)
        Assert.All(result.Items, site => Assert.True(site.PriceUsd >= 100m && site.PriceUsd <= 200m));
    }

    #endregion

    #region Location Filter Tests

    [Fact]
    public async Task GetSitesAsync_WithLocationFilter_ReturnsFilteredSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Locations = new List<string> { "US" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // example.com, gambling.com, lowdr.com
        Assert.All(result.Items, site => Assert.Equal("US", site.Location));
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleLocations_ReturnsFilteredSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Locations = new List<string> { "US", "UK" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(4, result.Total); // example.com, test.com, gambling.com, lowdr.com
        Assert.All(result.Items, site => Assert.Contains(site.Location, new[] { "US", "UK" }));
    }

    #endregion

    #region Allowed Flags Tests

    [Fact]
    public async Task GetSitesAsync_WithCasinoAllowed_ReturnsOnlyCasinoSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAllowed = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(2, result.Total); // example.com, gambling.com
        Assert.All(result.Items, site => Assert.NotNull(site.PriceCasino));
    }

    [Fact]
    public async Task GetSitesAsync_WithCryptoAllowed_ReturnsOnlyCryptoSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            CryptoAllowed = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // example.com, test.com, crypto.com
        Assert.All(result.Items, site => Assert.NotNull(site.PriceCrypto));
    }

    [Fact]
    public async Task GetSitesAsync_WithLinkInsertAllowed_ReturnsOnlyLinkInsertSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            LinkInsertAllowed = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total); // example.com, gambling.com, lowdr.com
        Assert.All(result.Items, site => Assert.NotNull(site.PriceLinkInsert));
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleAllowedFlags_AppliesAndLogic()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAllowed = true,
            CryptoAllowed = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Single(result.Items); // Only example.com has both
        Assert.NotNull(result.Items[0].PriceCasino);
        Assert.NotNull(result.Items[0].PriceCrypto);
    }

    [Fact]
    public async Task GetSitesAsync_WithAllThreeAllowedFlags_ReturnsOnlyFullySupportedSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAllowed = true,
            CryptoAllowed = true,
            LinkInsertAllowed = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Single(result.Items); // Only example.com has all three
        Assert.Equal("example.com", result.Items[0].Domain);
        Assert.NotNull(result.Items[0].PriceCasino);
        Assert.NotNull(result.Items[0].PriceCrypto);
        Assert.NotNull(result.Items[0].PriceLinkInsert);
    }

    #endregion

    #region Quarantine Filter Tests

    [Fact]
    public async Task GetSitesAsync_WithQuarantineAll_ReturnsAllSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Quarantine = QuarantineFilterValues.All,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(5, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_WithQuarantineOnly_ReturnsOnlyQuarantinedSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Quarantine = QuarantineFilterValues.Only,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Single(result.Items); // Only gambling.com
        Assert.Equal("gambling.com", result.Items[0].Domain);
        Assert.True(result.Items[0].IsQuarantined);
    }

    [Fact]
    public async Task GetSitesAsync_WithQuarantineExclude_ReturnsOnlyNonQuarantinedSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Quarantine = QuarantineFilterValues.Exclude,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(4, result.Total); // All except gambling.com
        Assert.All(result.Items, site => Assert.False(site.IsQuarantined));
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task GetSitesAsync_SortByDomainAsc_ReturnsSortedResults()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(5, result.Total);
        Assert.Equal("crypto.com", result.Items[0].Domain);
        Assert.Equal("example.com", result.Items[1].Domain);
        Assert.Equal("gambling.com", result.Items[2].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_SortByDrDesc_ReturnsSortedResults()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.DR,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(5, result.Total);
        Assert.Equal(90, result.Items[0].DR); // gambling.com
        Assert.Equal(70, result.Items[1].DR); // test.com
        Assert.Equal(60, result.Items[2].DR); // crypto.com
    }

    [Fact]
    public async Task GetSitesAsync_SortByTrafficAsc_ReturnsSortedResults()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Traffic,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(5, result.Total);
        Assert.Equal(1000, result.Items[0].Traffic); // lowdr.com
        Assert.Equal(10000, result.Items[1].Traffic); // example.com
        Assert.Equal(30000, result.Items[2].Traffic); // crypto.com
    }

    [Fact]
    public async Task GetSitesAsync_SortByPriceUsdDesc_ReturnsSortedResults()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.PriceUsd,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(5, result.Total);
        Assert.Equal(500m, result.Items[0].PriceUsd); // gambling.com
        Assert.Equal(200m, result.Items[1].PriceUsd); // test.com
        Assert.Equal(150m, result.Items[2].PriceUsd); // crypto.com
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task GetSitesAsync_FirstPage_ReturnsCorrectSubset()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 2,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.Total);
        Assert.Equal("crypto.com", result.Items[0].Domain);
        Assert.Equal("example.com", result.Items[1].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_SecondPage_ReturnsCorrectSubset()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 2,
            PageSize = 2,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.Total);
        Assert.Equal("gambling.com", result.Items[0].Domain);
        Assert.Equal("lowdr.com", result.Items[1].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_LastPage_ReturnsRemainingItems()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 3,
            PageSize = 2,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Single(result.Items); // Only test.com remains
        Assert.Equal(5, result.Total);
        Assert.Equal("test.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_PageSizeExceedsMax_ClampsTo1000()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 2000, // Exceeds max
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(5, result.Items.Count); // Returns all since under 1000
        Assert.Equal(5, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_PageBelowOne_ClampsToOne()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 0, // Invalid
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(5, result.Total);
    }

    #endregion

    #region Combined Filters Tests

    [Fact]
    public async Task GetSitesAsync_WithCombinedFilters_AppliesAllCorrectly()
    {
        // Arrange
        var query = new SitesQuery
        {
            DrMin = 50,
            Locations = new List<string> { "US" },
            CryptoAllowed = true,
            Quarantine = QuarantineFilterValues.Exclude,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Single(result.Items); // Only example.com matches all criteria
        Assert.Equal("example.com", result.Items[0].Domain);
        Assert.True(result.Items[0].DR >= 50);
        Assert.Equal("US", result.Items[0].Location);
        Assert.NotNull(result.Items[0].PriceCrypto);
        Assert.False(result.Items[0].IsQuarantined);
    }

    #endregion

    #region GetLocations Tests

    [Fact]
    public async Task GetLocationsAsync_ReturnsDistinctLocationsSorted()
    {
        // Act
        var result = await _service.GetLocationsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("CA", result[0]);
        Assert.Equal("UK", result[1]);
        Assert.Equal("US", result[2]);
    }

    [Fact]
    public async Task GetLocationsAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        _context.Sites.RemoveRange(_context.Sites);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetLocationsAsync();

        // Assert
        Assert.Empty(result);
    }

    #endregion
}
