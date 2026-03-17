using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
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
                PriceCasinoStatus = ServiceAvailabilityStatus.Available,
                PriceCrypto = 120m,
                PriceCryptoStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsert = 80m,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
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
                PriceCasinoStatus = ServiceAvailabilityStatus.NotAvailable,
                PriceCrypto = 180m,
                PriceCryptoStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsert = null,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
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
                PriceCasinoStatus = ServiceAvailabilityStatus.Available,
                PriceCrypto = null,
                PriceCryptoStatus = ServiceAvailabilityStatus.NotAvailable,
                PriceLinkInsert = 400m,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
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
                PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
                PriceCrypto = 170m,
                PriceCryptoStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsert = null,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.NotAvailable,
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
                PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
                PriceCrypto = null,
                PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
                PriceLinkInsert = 30m,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
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
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.Available, site.PriceCasinoStatus));
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
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.Available, site.PriceCryptoStatus));
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
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.Available, site.PriceLinkInsertStatus));
    }

    [Fact]
    public async Task GetSitesAsync_WithCasinoAvailabilityNotAvailable_ReturnsOnlyNotAvailableSites()
    {
        var query = new SitesQuery
        {
            CasinoAvailability = ServiceAvailabilityFilter.NotAvailable,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Single(result.Items);
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceCasinoStatus));
    }

    [Fact]
    public async Task GetSitesAsync_WithCryptoAvailabilityUnknown_ReturnsOnlyUnknownSites()
    {
        var query = new SitesQuery
        {
            CryptoAvailability = ServiceAvailabilityFilter.Unknown,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Single(result.Items);
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.Unknown, site.PriceCryptoStatus));
    }

    [Fact]
    public async Task GetSitesAsync_WithLinkInsertAvailabilityNotAvailable_ReturnsOnlyNotAvailableSites()
    {
        var query = new SitesQuery
        {
            LinkInsertAvailability = ServiceAvailabilityFilter.NotAvailable,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Single(result.Items);
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceLinkInsertStatus));
    }

    [Fact]
    public async Task GetSitesAsync_WhenBothLegacyAndNewFiltersProvided_NewAvailabilityWins()
    {
        var query = new SitesQuery
        {
            CasinoAllowed = true,
            CasinoAvailability = ServiceAvailabilityFilter.NotAvailable,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Single(result.Items);
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceCasinoStatus));
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

    #region MultiSearch Tests

    [Fact]
    public async Task MultiSearchSitesAsync_ExactMatchOnly_NoSubstringMatch()
    {
        var normalizedDomains = new List<string> { "example", "example.com", "test.com" };
        var duplicates = new List<string>();

        var result = await _service.MultiSearchSitesAsync(normalizedDomains, duplicates);

        Assert.Equal(2, result.Found.Count);
        Assert.Contains(result.Found, s => s.Domain == "example.com");
        Assert.Contains(result.Found, s => s.Domain == "test.com");
        Assert.Single(result.NotFound);
        Assert.Equal("example", result.NotFound[0]);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public async Task MultiSearchSitesAsync_ReturnsFoundAndNotFound()
    {
        var normalizedDomains = new List<string> { "example.com", "nonexistent.com", "crypto.com" };
        var duplicates = new List<string>();

        var result = await _service.MultiSearchSitesAsync(normalizedDomains, duplicates);

        Assert.Equal(2, result.Found.Count);
        Assert.Contains(result.Found, s => s.Domain == "example.com");
        Assert.Contains(result.Found, s => s.Domain == "crypto.com");
        Assert.Single(result.NotFound);
        Assert.Equal("nonexistent.com", result.NotFound[0]);
    }

    [Fact]
    public async Task MultiSearchSitesAsync_ReturnsDuplicatesFromInput()
    {
        var normalizedDomains = new List<string> { "example.com" };
        var duplicates = new List<string> { "example.com", "b.com" };

        var result = await _service.MultiSearchSitesAsync(normalizedDomains, duplicates);

        Assert.Single(result.Found);
        Assert.Equal("example.com", result.Found[0].Domain);
        Assert.Empty(result.NotFound);
        Assert.Equal(2, result.Duplicates.Count);
        Assert.Contains("example.com", result.Duplicates);
        Assert.Contains("b.com", result.Duplicates);
    }

    [Fact]
    public async Task MultiSearchSitesAsync_EmptyDomains_ReturnsOnlyDuplicates()
    {
        var normalizedDomains = new List<string>();
        var duplicates = new List<string> { "dup.com" };

        var result = await _service.MultiSearchSitesAsync(normalizedDomains, duplicates);

        Assert.Empty(result.Found);
        Assert.Empty(result.NotFound);
        Assert.Single(result.Duplicates);
        Assert.Equal("dup.com", result.Duplicates[0]);
    }

    [Fact]
    public async Task MultiSearchSitesAsync_AllNotFound_ReturnsEmptyFound()
    {
        var normalizedDomains = new List<string> { "missing1.com", "missing2.com" };
        var duplicates = new List<string>();

        var result = await _service.MultiSearchSitesAsync(normalizedDomains, duplicates);

        Assert.Empty(result.Found);
        Assert.Equal(2, result.NotFound.Count);
        Assert.Contains("missing1.com", result.NotFound);
        Assert.Contains("missing2.com", result.NotFound);
    }

    [Fact]
    public async Task MultiSearchSitesAsync_SingleQuery_ReturnsFullSiteDto()
    {
        var normalizedDomains = new List<string> { "gambling.com" };
        var duplicates = new List<string>();

        var result = await _service.MultiSearchSitesAsync(normalizedDomains, duplicates);

        Assert.Single(result.Found);
        var site = result.Found[0];
        Assert.Equal("gambling.com", site.Domain);
        Assert.Equal(90, site.DR);
        Assert.Equal(100000, site.Traffic);
        Assert.True(site.IsQuarantined);
        Assert.Equal("Under review", site.QuarantineReason);
    }

    #endregion

    #region UpdateSiteAsync Tests

    private static UpdateSiteRequest RequestFrom(Site site, bool isQuarantined = false, string? quarantineReason = null)
    {
        return new UpdateSiteRequest
        {
            DR = site.DR,
            Traffic = site.Traffic,
            Location = site.Location,
            PriceUsd = site.PriceUsd,
            PriceCasino = site.PriceCasino,
            PriceCasinoStatus = site.PriceCasinoStatus,
            PriceCrypto = site.PriceCrypto,
            PriceCryptoStatus = site.PriceCryptoStatus,
            PriceLinkInsert = site.PriceLinkInsert,
            PriceLinkInsertStatus = site.PriceLinkInsertStatus,
            Niche = site.Niche,
            Categories = site.Categories,
            IsQuarantined = isQuarantined,
            QuarantineReason = quarantineReason
        };
    }

    [Fact]
    public async Task UpdateSiteAsync_ExistingDomain_TurnOnQuarantine_SetsQuarantineAndReason()
    {
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site, isQuarantined: true, "Policy violation");

        var updated = await _service.UpdateSiteAsync("example.com", request, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.True(updated.IsQuarantined);
        Assert.Equal("Policy violation", updated.QuarantineReason);
        Assert.NotNull(updated.QuarantineUpdatedAtUtc);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.True(dbSite.IsQuarantined);
        Assert.Equal("Policy violation", dbSite.QuarantineReason);
    }

    [Fact]
    public async Task UpdateSiteAsync_ExistingDomain_TurnOffQuarantine_ClearsReason()
    {
        var site = await _context.Sites.FirstAsync(s => s.Domain == "gambling.com");
        var request = RequestFrom(site, isQuarantined: false, null);

        var updated = await _service.UpdateSiteAsync("gambling.com", request, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.False(updated.IsQuarantined);
        Assert.Null(updated.QuarantineReason);
        Assert.Null(updated.QuarantineUpdatedAtUtc);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "gambling.com");
        Assert.False(dbSite.IsQuarantined);
        Assert.Null(dbSite.QuarantineReason);
    }

    [Fact]
    public async Task UpdateSiteAsync_NormalizesDomain_MatchesExistingSite()
    {
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site, isQuarantined: true, "Test");

        var updated = await _service.UpdateSiteAsync("HTTPS://www.Example.COM/", request, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("example.com", updated.Domain);
        Assert.True(updated.IsQuarantined);
    }

    [Fact]
    public async Task UpdateSiteAsync_UnknownDomain_ReturnsNull()
    {
        var request = new UpdateSiteRequest
        {
            DR = 50,
            Traffic = 1000,
            Location = "US",
            PriceUsd = 100m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            IsQuarantined = true,
            QuarantineReason = "X"
        };

        var updated = await _service.UpdateSiteAsync("nonexistent.org", request, CancellationToken.None);

        Assert.Null(updated);
    }

    [Fact]
    public async Task UpdateSiteAsync_EmptyDomainAfterNormalize_ReturnsNull()
    {
        var request = new UpdateSiteRequest
        {
            DR = 50,
            Traffic = 1000,
            Location = "US",
            PriceUsd = 100m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            IsQuarantined = true,
            QuarantineReason = "X"
        };

        var updated = await _service.UpdateSiteAsync("  ", request, CancellationToken.None);

        Assert.Null(updated);
    }

    [Fact]
    public async Task UpdateSiteAsync_UpdatesEditableFields()
    {
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = new UpdateSiteRequest
        {
            DR = 60,
            Traffic = 20000,
            Location = "CA",
            PriceUsd = 150m,
            PriceCasino = 200m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Available,
            PriceCrypto = null,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsert = 90m,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
            Niche = "Updated niche",
            Categories = "Updated categories",
            IsQuarantined = site.IsQuarantined,
            QuarantineReason = site.QuarantineReason
        };

        var updated = await _service.UpdateSiteAsync("example.com", request, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(60, updated.DR);
        Assert.Equal(20000L, updated.Traffic);
        Assert.Equal("CA", updated.Location);
        Assert.Equal(150m, updated.PriceUsd);
        Assert.Equal(200m, updated.PriceCasino);
        Assert.Null(updated.PriceCrypto);
        Assert.Equal(90m, updated.PriceLinkInsert);
        Assert.Equal("Updated niche", updated.Niche);
        Assert.Equal("Updated categories", updated.Categories);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Equal(60, dbSite.DR);
        Assert.Equal("CA", dbSite.Location);
    }

    #endregion
}
