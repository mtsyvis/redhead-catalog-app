using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Tests;

public class SitesServiceTests : IDisposable
{
    private const string TestAuditUserEmail = "editor@test.com";

    private readonly ApplicationDbContext _context;
    private readonly MemoryCache _memoryCache;
    private readonly SitesService _service;

    public SitesServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var queryBuilder = new SitesQueryBuilder(_context);
        var nicheFilterOptionsCache = new NicheFilterOptionsCache(_context, _memoryCache);
        _service = new SitesService(_context, queryBuilder, nicheFilterOptionsCache, new LocationNormalizer());

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _memoryCache.Dispose();
    }

    private void SeedTestData()
    {
        SeedLocations();

        var sites = new List<Site>
        {
            new()
            {
                Domain = "example.com",
                DR = 50,
                Traffic = 10000,
                Location = "US",
                LocationKey = "US",
                ImportedLocationRaw = "US",
                Language = "EN",
                PriceUsd = 100m,
                PriceCasino = 150m,
                PriceCasinoStatus = ServiceAvailabilityStatus.Available,
                PriceCrypto = 120m,
                PriceCryptoStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsert = 80m,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsertCasino = 85m,
                PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Available,
                PriceDating = 95m,
                PriceDatingStatus = ServiceAvailabilityStatus.Available,
                NumberDFLinks = 2,
                TermType = TermType.Finite,
                TermValue = 2,
                TermUnit = TermUnit.Year,
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
                LocationKey = "GB",
                ImportedLocationRaw = "UK",
                PriceUsd = 200m,
                PriceCasino = null,
                PriceCasinoStatus = ServiceAvailabilityStatus.NotAvailable,
                PriceCrypto = 180m,
                PriceCryptoStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsert = null,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
                PriceLinkInsertCasino = null,
                PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.NotAvailable,
                PriceDating = null,
                PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
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
                LocationKey = "US",
                ImportedLocationRaw = "US",
                Language = "UNKNOWN",
                PriceUsd = 500m,
                PriceCasino = 600m,
                PriceCasinoStatus = ServiceAvailabilityStatus.Available,
                PriceCrypto = null,
                PriceCryptoStatus = ServiceAvailabilityStatus.NotAvailable,
                PriceLinkInsert = 400m,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsertCasino = null,
                PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.NotAvailable,
                PriceDating = 700m,
                PriceDatingStatus = ServiceAvailabilityStatus.Available,
                TermType = TermType.Permanent,
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
                LocationKey = "CA",
                ImportedLocationRaw = "CA",
                Language = "MULTI",
                PriceUsd = 150m,
                PriceCasino = null,
                PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
                PriceCrypto = 170m,
                PriceCryptoStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsert = null,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.NotAvailable,
                PriceLinkInsertCasino = null,
                PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
                PriceDating = null,
                PriceDatingStatus = ServiceAvailabilityStatus.NotAvailable,
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
                LocationKey = "US",
                ImportedLocationRaw = "US",
                Language = "DE",
                PriceUsd = 50m,
                PriceCasino = null,
                PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
                PriceCrypto = null,
                PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
                PriceLinkInsert = 30m,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
                PriceLinkInsertCasino = 20m,
                PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Available,
                PriceDating = null,
                PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
                Niche = "General",
                Categories = "Sports Betting, General",
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        };

        foreach (var site in sites)
        {
            site.NicheTokens = NicheNormalizer.NormalizeTokens(site.Niche);
        }

        _context.Sites.AddRange(sites);
        _context.SaveChanges();
    }

    private void SeedLocations()
    {
        _context.CanonicalLocations.AddRange(
            new CanonicalLocation { Key = "US", DisplayName = "United States", SortOrder = 1, IsActive = true },
            new CanonicalLocation { Key = "GB", DisplayName = "United Kingdom", SortOrder = 2, IsActive = true },
            new CanonicalLocation { Key = "CA", DisplayName = "Canada", SortOrder = 3, IsActive = true },
            new CanonicalLocation { Key = "FR", DisplayName = "France", SortOrder = 4, IsActive = true },
            new CanonicalLocation { Key = LocationConstants.UnknownLocationKey, DisplayName = "Unknown", SortOrder = 999, IsActive = true });

        _context.LocationGroups.AddRange(
            new LocationGroup { Key = "north-america", DisplayName = "North America", Kind = "Region", SortOrder = 1 },
            new LocationGroup { Key = "first-world", DisplayName = "First World", Kind = "Business", SortOrder = 2 },
            new LocationGroup { Key = "overlap", DisplayName = "Overlap", Kind = "Business", SortOrder = 3 });

        _context.LocationGroupItems.AddRange(
            new LocationGroupItem { GroupKey = "north-america", LocationKey = "US" },
            new LocationGroupItem { GroupKey = "north-america", LocationKey = "CA" },
            new LocationGroupItem { GroupKey = "first-world", LocationKey = "US" },
            new LocationGroupItem { GroupKey = "first-world", LocationKey = "GB" },
            new LocationGroupItem { GroupKey = "overlap", LocationKey = "US" });
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
        Assert.Equal("EN", result.Items[0].Language);
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

    [Fact]
    public async Task GetSitesAsync_WithStopList_ExcludesMatchingDomains()
    {
        var query = new SitesQuery
        {
            StopListDomains = new List<string> { "example.com", "crypto.com" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Equal(3, result.Total);
        Assert.DoesNotContain(result.Items, site => site.Domain == "example.com");
        Assert.DoesNotContain(result.Items, site => site.Domain == "crypto.com");
    }

    [Fact]
    public async Task GetSitesAsync_WithUnnormalizedStopList_NormalizesAndDeduplicatesBeforeFiltering()
    {
        // Arrange
        var query = new SitesQuery
        {
            StopListDomains = new List<string> { "https://www.Example.com/path", "example.com" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(4, result.Total);
        Assert.DoesNotContain(result.Items, site => site.Domain == "example.com");
    }

    [Fact]
    public async Task GetSitesAsync_WithStopList_AppliesBeforeSortingPaginationAndCount()
    {
        var query = new SitesQuery
        {
            StopListDomains = new List<string> { "gambling.com" },
            Locations = new List<string> { "US" },
            Page = 1,
            PageSize = 1,
            SortBy = SortFields.DR,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Equal(2, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("example.com", result.Items[0].Domain);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetSitesAsync_WithNullOrEmptyStopList_BehavesLikeCurrentSearch(bool useEmptyList)
    {
        var query = new SitesQuery
        {
            StopListDomains = useEmptyList ? new List<string>() : null,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Equal(5, result.Total);
        Assert.Equal(5, result.Items.Count);
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
        Assert.All(result.Items, site => Assert.Equal("United States", site.Location));
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
        Assert.All(result.Items, site => Assert.Contains(site.Location, new[] { "United States", "United Kingdom" }));
    }

    [Fact]
    public async Task GetSitesAsync_WithLocationGroupFilter_ReturnsGroupLocations()
    {
        // Arrange
        var query = new SitesQuery
        {
            LocationGroupKeys = ["north-america"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "example.com", "gambling.com", "lowdr.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithOverlappingLocationGroups_DoesNotDuplicateResults()
    {
        // Arrange
        var query = new SitesQuery
        {
            LocationGroupKeys = ["north-america", "overlap"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(4, result.Total);
        Assert.Equal(4, result.Items.Count);
        Assert.Equal(result.Items.Select(site => site.Domain).Distinct(StringComparer.Ordinal).Count(), result.Items.Count);
    }

    [Fact]
    public async Task GetSitesAsync_WithOverlappingLocationGroupsAndExcludedLocation_ReturnsUnionMinusExcluded()
    {
        // Arrange
        _context.LocationGroups.Add(new LocationGroup
        {
            Key = "europe",
            DisplayName = "Europe",
            Kind = "Region",
            SortOrder = 4
        });
        _context.LocationGroupItems.Add(new LocationGroupItem { GroupKey = "europe", LocationKey = "GB" });
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            LocationGroupKeys = ["first-world", "europe"],
            ExcludedLocationKeys = ["GB"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["example.com", "gambling.com", "lowdr.com"], result.Items.Select(site => site.Domain).ToArray());
        Assert.DoesNotContain(result.Items, site => site.Domain == "test.com");
    }

    [Fact]
    public async Task GetSitesAsync_WithLocationKeysAndExcludedLocation_ReturnsSelectedLocationsMinusExcluded()
    {
        // Arrange
        var query = new SitesQuery
        {
            LocationKeys = ["US", "GB"],
            ExcludedLocationKeys = ["GB"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["example.com", "gambling.com", "lowdr.com"], result.Items.Select(site => site.Domain).ToArray());
        Assert.DoesNotContain(result.Items, site => site.Domain == "test.com");
    }

    [Fact]
    public async Task GetSitesAsync_WithUnknownLocationFilter_ReturnsUnknownLocationRows()
    {
        // Arrange
        _context.Sites.Add(CreateSite("unknown-location.com", LocationConstants.UnknownLocationKey));
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            IncludeUnknownLocation = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        var site = Assert.Single(result.Items);
        Assert.Equal("unknown-location.com", site.Domain);
        Assert.Equal("Unknown", site.Location);
    }

    [Fact]
    public async Task GetSitesAsync_WithOtherLocationFilter_ReturnsNullLocationKeyRows()
    {
        // Arrange
        _context.Sites.Add(CreateSite("other-location.com", null));
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            IncludeOtherLocation = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        var site = Assert.Single(result.Items);
        Assert.Equal("other-location.com", site.Domain);
        Assert.Equal("Other", site.Location);
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleLocationFilters_UsesOrSemantics()
    {
        // Arrange
        _context.Sites.Add(CreateSite("other-location.com", null));
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            LocationKeys = ["GB"],
            LocationGroupKeys = ["north-america"],
            IncludeOtherLocation = true,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "example.com", "gambling.com", "lowdr.com", "other-location.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    #endregion

    #region Language Filter Tests

    [Theory]
    [InlineData("EN")]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("english")]
    public async Task GetSitesAsync_WithLanguageEnglishFilter_ReturnsOnlyEnSites(string language)
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Languages = new List<string> { language },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("example.com", result.Items[0].Domain);
        Assert.Equal("EN", result.Items[0].Language);
    }

    [Fact]
    public async Task GetSitesAsync_WithLanguageMultiFilter_ReturnsOnlyMultiSites()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Languages = new List<string> { "MULTI" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("crypto.com", result.Items[0].Domain);
        Assert.Equal("MULTI", result.Items[0].Language);
    }

    [Fact]
    public async Task GetSitesAsync_WithLanguageUnknownFilter_ReturnsUnknownAndNullLanguageSites()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Languages = new List<string> { "UNKNOWN" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Equal(["gambling.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
        Assert.Contains(result.Items, site => site.Language == "UNKNOWN");
        Assert.Contains(result.Items, site => site.Language is null);
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleLanguageFilters_UsesAnySemantics()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Languages = new List<string> { "EN", "DE", "UNKNOWN" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Equal(["example.com", "gambling.com", "lowdr.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    #endregion

    #region Niche Filter Tests

    [Fact]
    public async Task GetSitesAsync_WithSingleNicheFilter_ReturnsMatchingSites()
    {
        var query = new SitesQuery
        {
            Niches = new List<string> { "crypto" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Single(result.Items);
        Assert.Equal("crypto.com", result.Items[0].Domain);
        Assert.Equal(["crypto"], result.Items[0].NicheTokens);
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleNicheFilters_UsesAnySemantics()
    {
        var query = new SitesQuery
        {
            Niches = new List<string> { "Crypto", "casino" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Equal(["crypto.com", "gambling.com"], result.Items.Select(s => s.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithInvalidNicheFilter_DoesNotApplyNicheFiltering()
    {
        var query = new SitesQuery
        {
            Niches = new List<string> { "N/A", " " },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Equal(5, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_NicheFilter_DoesNotMatchEmptyTokens()
    {
        _context.Sites.Add(new Site
        {
            Domain = "empty-niche.com",
            DR = 40,
            Traffic = 4000,
            Location = "US",
            PriceUsd = 100m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
            Niche = "N/A",
            NicheTokens = NicheNormalizer.NormalizeTokens("N/A"),
            IsQuarantined = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Niches = new List<string> { "crypto" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("crypto.com", result.Items[0].Domain);
        Assert.DoesNotContain(result.Items, s => s.Domain == "empty-niche.com");
    }

    [Fact]
    public async Task GetSitesAsync_NicheFilterCombinesWithExistingFilters()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Niches = new List<string> { "casino", "crypto" },
            Locations = new List<string> { "US" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("gambling.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetNicheOptionsAsync_ReturnsDistinctSortedOptions_ExcludingEmptyTokens()
    {
        _context.Sites.AddRange(
            new Site
            {
                Domain = "mental.com",
                DR = 40,
                Traffic = 4000,
                Location = "US",
                PriceUsd = 100m,
                Niche = "Mental health, crypto",
                NicheTokens = NicheNormalizer.NormalizeTokens("Mental health, crypto"),
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Site
            {
                Domain = "invalid-niche.com",
                DR = 40,
                Traffic = 4000,
                Location = "US",
                PriceUsd = 100m,
                Niche = "N/A",
                NicheTokens = NicheNormalizer.NormalizeTokens("N/A"),
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await _context.SaveChangesAsync();

        var result = await _service.GetNicheOptionsAsync();

        Assert.Equal(
            ["casino", "crypto", "general", "mental health", "news", "tech"],
            result.Select(option => option.Value).ToArray());
        Assert.Equal("Mental Health", result.Single(option => option.Value == "mental health").Label);
        Assert.DoesNotContain(result, option => option.Value is "n/a" or "na" or "-" or "none" or "null");
    }

    #endregion

    #region Category Search Filter Tests

    [Fact]
    public async Task GetSitesAsync_WithSingleCategorySearchTerm_ReturnsMatchingSites()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = ["tech"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("example.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleCategorySearchTerms_UsesAnySemantics()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = ["news", "crypto"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Equal(["crypto.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithCategoryPhrase_PreservesSpaces()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = ["sports betting"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("lowdr.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_WithCategorySearchTerm_IsCaseInsensitive()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = ["gAmBlInG"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("gambling.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_WithCategorySearchTerm_DoesNotMatchNullOrEmptyCategories()
    {
        _context.Sites.AddRange(
            SiteForCategorySearch("empty-categories.com", string.Empty),
            SiteForCategorySearch("null-categories.com", null));
        await _context.SaveChangesAsync();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = ["technology"],
            Page = 1,
            PageSize = 20,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("example.com", result.Items[0].Domain);
        Assert.DoesNotContain(result.Items, site => site.Domain is "empty-categories.com" or "null-categories.com");
    }

    [Fact]
    public async Task GetSitesAsync_WithOnlyEmptyCategorySearchTerms_DoesNotApplyCategoryFilter()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = ["", " ", null!],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Equal(5, result.Total);
    }

    [Theory]
    [InlineData("%", "percent-category.com")]
    [InlineData("_", "underscore-category.com")]
    [InlineData("\\", "slash-category.com")]
    public async Task GetSitesAsync_WithCategoryWildcardCharacters_TreatsThemLiterally(
        string term,
        string expectedDomain)
    {
        _context.Sites.AddRange(
            SiteForCategorySearch("plain-category.com", "plain category"),
            SiteForCategorySearch("percent-category.com", "literal % category"),
            SiteForCategorySearch("underscore-category.com", "literal _ category"),
            SiteForCategorySearch("slash-category.com", @"literal \ category"));
        await _context.SaveChangesAsync();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = [term],
            Page = 1,
            PageSize = 20,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Equal([expectedDomain], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_CategorySearchFilterCombinesWithOtherFiltersUsingAnd()
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            CategorySearchTerms = ["technology", "news"],
            Locations = ["US"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Single(result.Items);
        Assert.Equal("example.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_WithTopicFitExpandMode_UsesNicheOrCategorySemantics()
    {
        // Arrange
        var query = new SitesQuery
        {
            Niches = ["crypto"],
            CategorySearchTerms = ["sports betting"],
            TopicFitMode = TopicFitModeValues.Expand,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "lowdr.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithTopicFitNarrowMode_UsesNicheAndCategorySemantics()
    {
        // Arrange
        var query = new SitesQuery
        {
            Niches = ["crypto"],
            CategorySearchTerms = ["sports betting"],
            TopicFitMode = TopicFitModeValues.Narrow,
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
    }

    [Fact]
    public async Task GetSitesAsync_WithExcludedNicheFilter_ExcludesMatchingSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            ExcludedNiches = ["casino", "crypto"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["example.com", "lowdr.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithExcludedCategorySearchTerms_ExcludesMatchingSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            ExcludedCategorySearchTerms = ["news", "gambling"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "example.com", "lowdr.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithTopicFitExpandMode_StillAppliesExclusions()
    {
        // Arrange
        var query = new SitesQuery
        {
            Niches = ["crypto"],
            CategorySearchTerms = ["sports betting"],
            TopicFitMode = TopicFitModeValues.Expand,
            ExcludedCategorySearchTerms = ["sports betting"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithOnlyInvalidExcludedNiches_DoesNotApplyExclusion()
    {
        // Arrange
        var query = new SitesQuery
        {
            ExcludedNiches = ["N/A", " "],
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
    }

    #endregion

    #region Availability Filter Tests

    [Fact]
    public async Task GetSitesAsync_WithCasinoAvailabilityNotAvailable_ReturnsOnlyNotAvailableSites()
    {
        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.NotAvailable],
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
    public async Task GetSitesAsync_WithCasinoAvailabilityAvailable_ReturnsOnlyAvailableSites()
    {
        // Arrange
        _context.Sites.Add(new Site
        {
            Domain = "yes-casino.com",
            DR = 40,
            Traffic = 4000,
            Location = "US",
            PriceUsd = 100m,
            PriceCasino = null,
            PriceCasinoStatus = ServiceAvailabilityStatus.AvailableWithUnknownPrice,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
            IsQuarantined = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.Available],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["example.com", "gambling.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithCasinoAvailabilityAvailableWithUnknownPrice_ReturnsOnlyAvailableWithUnknownPriceSites()
    {
        // Arrange
        _context.Sites.Add(new Site
        {
            Domain = "yes-casino.com",
            DR = 40,
            Traffic = 4000,
            Location = "US",
            PriceUsd = 100m,
            PriceCasino = null,
            PriceCasinoStatus = ServiceAvailabilityStatus.AvailableWithUnknownPrice,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
            IsQuarantined = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.AvailableWithUnknownPrice],
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
        Assert.Equal("yes-casino.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_WithCryptoAvailabilityUnknown_ReturnsOnlyUnknownSites()
    {
        var query = new SitesQuery
        {
            CryptoAvailability = [ServiceAvailabilityStatus.Unknown],
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
            LinkInsertAvailability = [ServiceAvailabilityStatus.NotAvailable],
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
    public async Task GetSitesAsync_WithLinkInsertCasinoAvailabilityNotAvailable_ReturnsOnlyNotAvailableSites()
    {
        var query = new SitesQuery
        {
            LinkInsertCasinoAvailability = [ServiceAvailabilityStatus.NotAvailable],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceLinkInsertCasinoStatus));
    }

    [Fact]
    public async Task GetSitesAsync_WithDatingAvailabilityUnknown_ReturnsOnlyUnknownSites()
    {
        var query = new SitesQuery
        {
            DatingAvailability = [ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.GetSitesAsync(query);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, site => Assert.Equal(ServiceAvailabilityStatus.Unknown, site.PriceDatingStatus));
    }

    [Fact]
    public async Task GetSitesAsync_WithEmptyAvailabilityFilters_DoesNotFilter()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAvailability = [],
            CryptoAvailability = null,
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
        Assert.Equal(["crypto.com", "example.com", "gambling.com", "lowdr.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleCasinoAvailabilityValues_UsesOrSemantics()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.NotAvailable, ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "lowdr.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleCryptoAvailabilityValues_UsesOrSemantics()
    {
        // Arrange
        var query = new SitesQuery
        {
            CryptoAvailability = [ServiceAvailabilityStatus.Available, ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "example.com", "lowdr.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleLinkInsertAvailabilityValues_UsesOrSemantics()
    {
        // Arrange
        var query = new SitesQuery
        {
            LinkInsertAvailability = [ServiceAvailabilityStatus.NotAvailable, ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleLinkInsertCasinoAvailabilityValues_UsesOrSemantics()
    {
        // Arrange
        var query = new SitesQuery
        {
            LinkInsertCasinoAvailability = [ServiceAvailabilityStatus.Available, ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "example.com", "lowdr.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleDatingAvailabilityValues_UsesOrSemantics()
    {
        // Arrange
        var query = new SitesQuery
        {
            DatingAvailability = [ServiceAvailabilityStatus.NotAvailable, ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "lowdr.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleAvailabilityFiltersAcrossServices_CombinesWithAnd()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.NotAvailable, ServiceAvailabilityStatus.Unknown],
            CryptoAvailability = [ServiceAvailabilityStatus.Available],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(["crypto.com", "test.com"], result.Items.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleAvailabilityValuesAndLanguageFilter_CombinesWithAnd()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.NotAvailable, ServiceAvailabilityStatus.Unknown],
            Languages = ["DE"],
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
        Assert.Equal("lowdr.com", result.Items[0].Domain);
    }

    [Fact]
    public async Task GetSitesAsync_WithMultipleAvailabilityValues_ReturnsCorrectCountAndPage()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.NotAvailable, ServiceAvailabilityStatus.Unknown],
            Page = 2,
            PageSize = 2,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.GetSitesAsync(query);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("test.com", result.Items[0].Domain);
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

    [Fact]
    public async Task GetSitesAsync_SortByCreatedAtAsc_ReturnsSortedResults()
    {
        // Arrange
        SetAuditDatesForSorting();
        await _context.SaveChangesAsync();

        // Act
        var domains = await GetSortedDomainsAsync(SortFields.CreatedAt, SortingDefaults.Ascending);

        // Assert
        Assert.Equal(["lowdr.com", "example.com", "test.com", "crypto.com", "gambling.com"], domains);
    }

    [Fact]
    public async Task GetSitesAsync_SortByUpdatedAtDesc_ReturnsSortedResults()
    {
        // Arrange
        SetAuditDatesForSorting();
        await _context.SaveChangesAsync();

        // Act
        var domains = await GetSortedDomainsAsync(SortFields.UpdatedAt, SortingDefaults.Descending);

        // Assert
        Assert.Equal(["gambling.com", "crypto.com", "test.com", "example.com", "lowdr.com"], domains);
    }

    public static TheoryData<string, string, string[]> ServicePriceSortCases => new()
    {
        {
            SortFields.PriceCasino,
            SortingDefaults.Ascending,
            new[] { "example.com", "gambling.com", "test.com", "crypto.com", "lowdr.com" }
        },
        {
            SortFields.PriceCasino,
            SortingDefaults.Descending,
            new[] { "gambling.com", "example.com", "test.com", "crypto.com", "lowdr.com" }
        },
        {
            SortFields.PriceCrypto,
            SortingDefaults.Ascending,
            new[] { "example.com", "crypto.com", "test.com", "gambling.com", "lowdr.com" }
        },
        {
            SortFields.PriceCrypto,
            SortingDefaults.Descending,
            new[] { "test.com", "crypto.com", "example.com", "gambling.com", "lowdr.com" }
        },
        {
            SortFields.PriceLinkInsert,
            SortingDefaults.Ascending,
            new[] { "lowdr.com", "example.com", "gambling.com", "crypto.com", "test.com" }
        },
        {
            SortFields.PriceLinkInsert,
            SortingDefaults.Descending,
            new[] { "gambling.com", "example.com", "lowdr.com", "crypto.com", "test.com" }
        },
        {
            SortFields.PriceLinkInsertCasino,
            SortingDefaults.Ascending,
            new[] { "lowdr.com", "example.com", "gambling.com", "test.com", "crypto.com" }
        },
        {
            SortFields.PriceLinkInsertCasino,
            SortingDefaults.Descending,
            new[] { "example.com", "lowdr.com", "gambling.com", "test.com", "crypto.com" }
        },
        {
            SortFields.PriceDating,
            SortingDefaults.Ascending,
            new[] { "example.com", "gambling.com", "crypto.com", "lowdr.com", "test.com" }
        },
        {
            SortFields.PriceDating,
            SortingDefaults.Descending,
            new[] { "gambling.com", "example.com", "crypto.com", "lowdr.com", "test.com" }
        }
    };

    [Theory]
    [MemberData(nameof(ServicePriceSortCases))]
    public async Task GetSitesAsync_SortByServicePrice_ReturnsUnavailableAndUnknownLast(
        string sortBy,
        string sortDir,
        string[] expectedDomains)
    {
        var domains = await GetSortedDomainsAsync(sortBy, sortDir);

        Assert.Equal(expectedDomains, domains);
    }

    [Theory]
    [InlineData(SortingDefaults.Ascending, new[] { "known-cheap.com", "known-expensive.com", "yes-price.com", "no-price.com", "unknown-price.com" })]
    [InlineData(SortingDefaults.Descending, new[] { "known-expensive.com", "known-cheap.com", "yes-price.com", "no-price.com", "unknown-price.com" })]
    public async Task GetSitesAsync_SortByServicePrice_OrdersKnownPricesYesUnavailableUnknown(
        string sortDir,
        string[] expectedDomains)
    {
        // Arrange
        _context.Sites.RemoveRange(_context.Sites);
        _context.Sites.AddRange(
            SiteWithServiceState("known-expensive.com", 200m, ServiceAvailabilityStatus.Available),
            SiteWithServiceState("known-cheap.com", 100m, ServiceAvailabilityStatus.Available),
            SiteWithServiceState("yes-price.com", null, ServiceAvailabilityStatus.AvailableWithUnknownPrice),
            SiteWithServiceState("no-price.com", null, ServiceAvailabilityStatus.NotAvailable),
            SiteWithServiceState("unknown-price.com", null, ServiceAvailabilityStatus.Unknown));
        await _context.SaveChangesAsync();

        // Act
        var domains = await GetSortedDomainsAsync(SortFields.PriceCasino, sortDir);

        // Assert
        Assert.Equal(expectedDomains, domains);
    }

    [Fact]
    public async Task GetSitesAsync_SortByNumberDFLinksAsc_ReturnsNullsLast()
    {
        SetNumberDFLinks();
        await _context.SaveChangesAsync();

        var domains = await GetSortedDomainsAsync(SortFields.NumberDFLinks, SortingDefaults.Ascending);

        Assert.Equal(new List<string>
        {
            "crypto.com",
            "example.com",
            "gambling.com",
            "lowdr.com",
            "test.com"
        }, domains);
    }

    [Fact]
    public async Task GetSitesAsync_SortByNumberDFLinksDesc_ReturnsNullsLast()
    {
        SetNumberDFLinks();
        await _context.SaveChangesAsync();

        var domains = await GetSortedDomainsAsync(SortFields.NumberDFLinks, SortingDefaults.Descending);

        Assert.Equal(new List<string>
        {
            "gambling.com",
            "example.com",
            "crypto.com",
            "lowdr.com",
            "test.com"
        }, domains);
    }

    [Fact]
    public async Task GetSitesAsync_SortByTermAsc_ReturnsFinitePermanentThenNull()
    {
        SetTerms();
        await _context.SaveChangesAsync();

        var domains = await GetSortedDomainsAsync(SortFields.Term, SortingDefaults.Ascending);

        Assert.Equal(new List<string>
        {
            "lowdr.com",
            "example.com",
            "crypto.com",
            "gambling.com",
            "test.com"
        }, domains);
    }

    [Fact]
    public async Task GetSitesAsync_SortByTermDesc_ReturnsPermanentFiniteThenNull()
    {
        SetTerms();
        await _context.SaveChangesAsync();

        var domains = await GetSortedDomainsAsync(SortFields.Term, SortingDefaults.Descending);

        Assert.Equal(new List<string>
        {
            "gambling.com",
            "crypto.com",
            "example.com",
            "lowdr.com",
            "test.com"
        }, domains);
    }

    #endregion

    #region LastPublishedDate Tests

    [Fact]
    public async Task GetSitesAsync_WithLastPublishedRange_ExcludesNullAndOutsideRange()
    {
        SetLastPublishedDatesForFiltering();
        await _context.SaveChangesAsync();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All,
            LastPublishedFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastPublishedToExclusive = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        Assert.Equal(["example.com", "gambling.com", "test.com"], result.Items.Select(s => s.Domain).ToList());
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_SortByLastPublishedDate_ReturnsNullLastAndUsesMonthOnlyTieBreaker()
    {
        SetLastPublishedDatesForSorting();
        await _context.SaveChangesAsync();

        var desc = await GetSortedDomainsAsync(SortFields.LastPublishedDate, SortingDefaults.Descending);
        var asc = await GetSortedDomainsAsync(SortFields.LastPublishedDate, SortingDefaults.Ascending);

        Assert.Equal(["example.com", "test.com", "gambling.com", "crypto.com", "lowdr.com"], desc);
        Assert.Equal(["crypto.com", "gambling.com", "test.com", "example.com", "lowdr.com"], asc);
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
            CryptoAvailability = [ServiceAvailabilityStatus.Available],
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
        Assert.Equal("United States", result.Items[0].Location);
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
        Assert.Equal(["United States", "United Kingdom", "Canada", "France", "Unknown"], result);
    }

    [Fact]
    public async Task GetLocationsAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        _context.Sites.RemoveRange(_context.Sites);
        _context.CanonicalLocations.RemoveRange(_context.CanonicalLocations);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetLocationsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLocationFilterOptionsAsync_ReturnsGroupsLocationsAndUnknownSpecialValue()
    {
        // Arrange

        // Act
        var result = await _service.GetLocationFilterOptionsAsync();

        // Assert
        Assert.Contains(result.Groups, group =>
            group.Key == "north-america"
            && group.DisplayName == "North America"
            && group.GroupType == "Region"
            && group.LocationCount == 2
            && group.Locations.Select(location => location.Key).SequenceEqual(new[] { "US", "CA" }));
        Assert.Contains(result.Locations, location =>
            location.Key == "US" && location.DisplayName == "United States");
        Assert.Equal(LocationConstants.UnknownLocationKey, result.Special.Unknown.Key);
        Assert.Equal("Unknown", result.Special.Unknown.DisplayName);
        Assert.Null(result.Special.Other);
        Assert.DoesNotContain(result.Locations, location => location.Key == LocationDisplayFormatter.OtherPseudoKey);
    }

    [Fact]
    public async Task GetLocationFilterOptionsAsync_WhenOtherLocationsExist_ReturnsOtherSpecialValue()
    {
        // Arrange
        _context.Sites.Add(CreateSite("other-filter-option.com", null));
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetLocationFilterOptionsAsync();

        // Assert
        Assert.NotNull(result.Special.Other);
        Assert.Equal(LocationDisplayFormatter.OtherPseudoKey, result.Special.Other.Key);
        Assert.Equal("Other", result.Special.Other.DisplayName);
        Assert.DoesNotContain(result.Locations, location => location.Key == LocationDisplayFormatter.OtherPseudoKey);
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
        Assert.Equal("UNKNOWN", site.Language);
        Assert.True(site.IsQuarantined);
        Assert.Equal("Under review", site.QuarantineReason);
    }

    [Fact]
    public async Task MultiSearchSitesAsync_FoundRowsIncludeNullLanguage()
    {
        var result = await _service.MultiSearchSitesAsync(
            new List<string> { "test.com", "missing.com" },
            []);

        var site = Assert.Single(result.Found);
        Assert.Equal("test.com", site.Domain);
        Assert.Null(site.Language);
        Assert.Equal(["missing.com"], result.NotFound);
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
            Language = site.Language,
            PriceUsd = site.PriceUsd,
            PriceCasino = site.PriceCasino,
            PriceCasinoStatus = site.PriceCasinoStatus,
            PriceCrypto = site.PriceCrypto,
            PriceCryptoStatus = site.PriceCryptoStatus,
            PriceLinkInsert = site.PriceLinkInsert,
            PriceLinkInsertStatus = site.PriceLinkInsertStatus,
            PriceLinkInsertCasino = site.PriceLinkInsertCasino,
            PriceLinkInsertCasinoStatus = site.PriceLinkInsertCasinoStatus,
            PriceDating = site.PriceDating,
            PriceDatingStatus = site.PriceDatingStatus,
            NumberDFLinks = site.NumberDFLinks,
            TermType = site.TermType,
            TermValue = site.TermValue,
            TermUnit = site.TermUnit,
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

        var updated = await _service.UpdateSiteAsync("example.com", request, TestAuditUserEmail, CancellationToken.None);

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

        var updated = await _service.UpdateSiteAsync("gambling.com", request, TestAuditUserEmail, CancellationToken.None);

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

        var updated = await _service.UpdateSiteAsync("HTTPS://www.Example.COM/", request, TestAuditUserEmail, CancellationToken.None);

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

        var updated = await _service.UpdateSiteAsync("nonexistent.org", request, TestAuditUserEmail, CancellationToken.None);

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

        var updated = await _service.UpdateSiteAsync("  ", request, TestAuditUserEmail, CancellationToken.None);

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
            Language = "EN",
            PriceUsd = 150m,
            PriceCasino = 200m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Available,
            PriceCrypto = null,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsert = 90m,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
            PriceLinkInsertCasino = 95m,
            PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Available,
            PriceDating = null,
            PriceDatingStatus = ServiceAvailabilityStatus.NotAvailable,
            NumberDFLinks = 4,
            TermType = TermType.Finite,
            TermValue = 1,
            TermUnit = TermUnit.Year,
            Niche = "Updated niche",
            Categories = "Updated categories",
            IsQuarantined = site.IsQuarantined,
            QuarantineReason = site.QuarantineReason
        };

        var updated = await _service.UpdateSiteAsync("example.com", request, TestAuditUserEmail, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(60, updated.DR);
        Assert.Equal(20000L, updated.Traffic);
        Assert.Equal("Canada", updated.Location);
        Assert.Equal("EN", updated.Language);
        Assert.Equal(150m, updated.PriceUsd);
        Assert.Equal(200m, updated.PriceCasino);
        Assert.Null(updated.PriceCrypto);
        Assert.Equal(90m, updated.PriceLinkInsert);
        Assert.Equal(95m, updated.PriceLinkInsertCasino);
        Assert.Null(updated.PriceDating);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, updated.PriceDatingStatus);
        Assert.Equal(4, updated.NumberDFLinks);
        Assert.Equal(TermType.Finite, updated.TermType);
        Assert.Equal(1, updated.TermValue);
        Assert.Equal(TermUnit.Year, updated.TermUnit);
        Assert.Equal("Updated niche", updated.Niche);
        Assert.Equal(["updated niche"], updated.NicheTokens);
        Assert.Equal("Updated categories", updated.Categories);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Equal(60, dbSite.DR);
        Assert.Equal("CA", dbSite.Location);
        Assert.Equal("CA", dbSite.LocationKey);
        Assert.Equal("EN", dbSite.Language);
        Assert.Equal(["updated niche"], dbSite.NicheTokens);
    }

    [Fact]
    public async Task UpdateSiteAsync_WithUserEmail_SetsUpdatedBy()
    {
        // Arrange
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site);

        // Act
        var updated = await _service.UpdateSiteAsync(
            "example.com",
            request,
            TestAuditUserEmail,
            CancellationToken.None);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(TestAuditUserEmail, updated.UpdatedBy);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Equal(TestAuditUserEmail, dbSite.UpdatedBy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    public async Task UpdateSiteAsync_EmptyOrUnknownLocation_SetsCanonicalUnknown(string? location)
    {
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site);
        request.Location = location ?? string.Empty;

        var updated = await _service.UpdateSiteAsync("example.com", request, TestAuditUserEmail, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Unknown", updated.Location);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Equal(LocationConstants.UnknownLocationKey, dbSite.LocationKey);
    }

    [Fact]
    public async Task UpdateSiteAsync_NullLanguage_ClearsLanguage()
    {
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site);
        request.Language = null;

        var updated = await _service.UpdateSiteAsync("example.com", request, TestAuditUserEmail, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Null(updated.Language);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Null(dbSite.Language);
    }

    [Fact]
    public async Task UpdateSiteAsync_ClearingNiche_ClearsNicheTokens()
    {
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site);
        request.Niche = "N/A";

        var updated = await _service.UpdateSiteAsync("example.com", request, TestAuditUserEmail, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Empty(updated.NicheTokens);

        var dbSite = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Empty(dbSite.NicheTokens);
    }

    [Fact]
    public async Task UpdateSiteAsync_WhenNicheChanges_NicheOptionsIncludeNewTokenAfterCacheWarmup()
    {
        var before = await _service.GetNicheOptionsAsync();
        Assert.DoesNotContain(before, option => option.Value == "updated cache niche");

        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site);
        request.Niche = "Updated Cache Niche";

        await _service.UpdateSiteAsync("example.com", request, TestAuditUserEmail, CancellationToken.None);

        var after = await _service.GetNicheOptionsAsync();
        Assert.Contains(after, option => option.Value == "updated cache niche");
    }

    [Fact]
    public async Task UpdateSiteAsync_InvalidatesNicheOptionsCache()
    {
        var nicheOptionsCacheMock = new Mock<INicheFilterOptionsCache>();
        var service = new SitesService(_context, new SitesQueryBuilder(_context), nicheOptionsCacheMock.Object, new LocationNormalizer());
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        var request = RequestFrom(site);
        request.Niche = "Updated Cache Niche";

        await service.UpdateSiteAsync("example.com", request, TestAuditUserEmail, CancellationToken.None);

        nicheOptionsCacheMock.Verify(cache => cache.Invalidate(), Times.Once);
    }

    #endregion

    #region Price filter and sort: PriceUsd nullable

    [Fact]
    public async Task GetSitesAsync_PriceMinFilter_ExcludesNullPriceUsd()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Page = 1, PageSize = 10, PriceMin = 50m,
            SortBy = SortFields.Domain, SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.DoesNotContain(result.Items, s => s.Domain == "null-price.com");
    }

    [Fact]
    public async Task GetSitesAsync_PriceMaxFilter_ExcludesNullPriceUsd()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Page = 1, PageSize = 10, PriceMax = 1000m,
            SortBy = SortFields.Domain, SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.DoesNotContain(result.Items, s => s.Domain == "null-price.com");
    }

    [Fact]
    public async Task GetSitesAsync_NoPriceFilter_IncludesNullPriceUsd()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Page = 1, PageSize = 10,
            SortBy = SortFields.Domain, SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Contains(result.Items, s => s.Domain == "null-price.com");
    }

    [Fact]
    public async Task GetSitesAsync_SortByPriceUsdAscending_NullsLast()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Page = 1, PageSize = 10,
            SortBy = SortFields.PriceUsd, SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Equal("null-price.com", result.Items.Last().Domain);
    }

    [Fact]
    public async Task GetSitesAsync_SortByPriceUsdDescending_NullsLast()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Page = 1, PageSize = 10,
            SortBy = SortFields.PriceUsd, SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        });

        Assert.Equal("null-price.com", result.Items.Last().Domain);
    }

    #endregion

    private async Task<List<string>> GetSortedDomainsAsync(string sortBy, string sortDir)
    {
        var result = await _service.GetSitesAsync(new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = sortBy,
            SortDir = sortDir,
        });

        return result.Items.Select(s => s.Domain).ToList();
    }

    private void SetNumberDFLinks()
    {
        _context.Sites.Single(s => s.Domain == "example.com").NumberDFLinks = 2;
        _context.Sites.Single(s => s.Domain == "test.com").NumberDFLinks = null;
        _context.Sites.Single(s => s.Domain == "gambling.com").NumberDFLinks = 10;
        _context.Sites.Single(s => s.Domain == "crypto.com").NumberDFLinks = 1;
        _context.Sites.Single(s => s.Domain == "lowdr.com").NumberDFLinks = null;
    }

    private void SetTerms()
    {
        SetFiniteTerm("lowdr.com", 1);
        SetFiniteTerm("example.com", 2);
        SetFiniteTerm("crypto.com", 10);

        var permanent = _context.Sites.Single(s => s.Domain == "gambling.com");
        permanent.TermType = TermType.Permanent;
        permanent.TermValue = null;
        permanent.TermUnit = null;

        var empty = _context.Sites.Single(s => s.Domain == "test.com");
        empty.TermType = null;
        empty.TermValue = null;
        empty.TermUnit = null;
    }

    private void SetAuditDatesForSorting()
    {
        SetAuditDates("lowdr.com", 1);
        SetAuditDates("example.com", 2);
        SetAuditDates("test.com", 3);
        SetAuditDates("crypto.com", 4);
        SetAuditDates("gambling.com", 5);
    }

    private void SetAuditDates(string domain, int day)
    {
        var site = _context.Sites.Single(s => s.Domain == domain);
        site.CreatedAtUtc = new DateTime(2025, 1, day, 0, 0, 0, DateTimeKind.Utc);
        site.UpdatedAtUtc = new DateTime(2025, 2, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private void SetFiniteTerm(string domain, int years)
    {
        var site = _context.Sites.Single(s => s.Domain == domain);
        site.TermType = TermType.Finite;
        site.TermValue = years;
        site.TermUnit = TermUnit.Year;
    }

    private void SetLastPublishedDatesForFiltering()
    {
        SetLastPublishedDate("example.com", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetLastPublishedDate("test.com", new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        SetLastPublishedDate("gambling.com", new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc));
        SetLastPublishedDate("crypto.com", new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        SetLastPublishedDate("lowdr.com", null);
    }

    private void SetLastPublishedDatesForSorting()
    {
        SetLastPublishedDate("example.com", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        SetLastPublishedDate("test.com", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), isMonthOnly: false);
        SetLastPublishedDate("gambling.com", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), isMonthOnly: true);
        SetLastPublishedDate("crypto.com", new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), isMonthOnly: true);
        SetLastPublishedDate("lowdr.com", null);
    }

    private void SetLastPublishedDate(string domain, DateTime? value, bool isMonthOnly = false)
    {
        var site = _context.Sites.Single(s => s.Domain == domain);
        site.LastPublishedDate = value;
        site.LastPublishedDateIsMonthOnly = value.HasValue && isMonthOnly;
    }

    private static Site SiteWithNullPrice(string domain) => new()
    {
        Domain = domain,
        DR = 50, Traffic = 10000, Location = "US", LocationKey = "US", ImportedLocationRaw = "US",
        PriceUsd = null,
        PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
        IsQuarantined = false,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static Site SiteWithServiceState(
        string domain,
        decimal? priceCasino,
        ServiceAvailabilityStatus priceCasinoStatus) => new()
    {
        Domain = domain,
        DR = 50,
        Traffic = 10000,
        Location = "US",
        LocationKey = "US",
        ImportedLocationRaw = "US",
        PriceUsd = 100m,
        PriceCasino = priceCasino,
        PriceCasinoStatus = priceCasinoStatus,
        PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
        IsQuarantined = false,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static Site SiteForCategorySearch(string domain, string? categories) => new()
    {
        Domain = domain,
        DR = 50,
        Traffic = 10000,
        Location = "US",
        LocationKey = "US",
        ImportedLocationRaw = "US",
        PriceUsd = 100m,
        PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
        Categories = categories,
        IsQuarantined = false,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static Site CreateSite(string domain, string? locationKey) => new()
    {
        Domain = domain,
        DR = 50,
        Traffic = 10000,
        Location = locationKey ?? "Unmapped location",
        LocationKey = locationKey,
        ImportedLocationRaw = locationKey is null ? "Unmapped location" : locationKey,
        PriceUsd = 100m,
        PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
        IsQuarantined = false,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}

