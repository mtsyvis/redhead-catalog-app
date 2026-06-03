using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests;

public class ExportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ExportService _service;
    private const string TestUserId = "test-user-id";
    private const string TestUserEmail = "test@example.com";

    public ExportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var queryBuilder = new SitesQueryBuilder(_context);
        _service = new ExportService(
            _context,
            queryBuilder,
            new EffectiveExportPolicyService(_context),
            new SitesExcelExportGenerator());

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        _context.CanonicalLocations.AddRange(
            new CanonicalLocation { Key = "US", DisplayName = "United States", SortOrder = 1, IsActive = true },
            new CanonicalLocation { Key = "GB", DisplayName = "United Kingdom", SortOrder = 2, IsActive = true },
            new CanonicalLocation { Key = LocationConstants.UnknownLocationKey, DisplayName = "Unknown", SortOrder = 999, IsActive = true });
        _context.LocationGroups.Add(new LocationGroup { Key = "western", DisplayName = "Western", Kind = "Business", SortOrder = 1 });
        _context.LocationGroupItems.AddRange(
            new LocationGroupItem { GroupKey = "western", LocationKey = "US" },
            new LocationGroupItem { GroupKey = "western", LocationKey = "GB" });

        // Seed role settings
        var roleSettings = new List<RoleSettings>
        {
            new() { RoleName = AppRoles.SuperAdmin, ExportLimitMode = ExportLimitMode.Unlimited, ExportLimitRows = null },
            new() { RoleName = AppRoles.Admin, ExportLimitMode = ExportLimitMode.Unlimited, ExportLimitRows = null },
            new() { RoleName = AppRoles.Internal, ExportLimitMode = ExportLimitMode.Limited, ExportLimitRows = 10000 },
            new() { RoleName = AppRoles.Client, ExportLimitMode = ExportLimitMode.Limited, ExportLimitRows = 5000 },
            new() { RoleName = "DisabledRole", ExportLimitMode = ExportLimitMode.Disabled, ExportLimitRows = null }
        };
        _context.RoleSettings.AddRange(roleSettings);

        // Seed sites
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
                PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
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
                PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
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
            }
        };

        foreach (var site in sites)
        {
            site.NicheTokens = NicheNormalizer.NormalizeTokens(site.Niche);
        }

        _context.Sites.AddRange(sites);
        _context.SaveChanges();
    }

    private static SitesQuery DefaultQuery() => new()
    {
        Page = 1,
        PageSize = 10,
        SortBy = SortFields.Domain,
        SortDir = SortingDefaults.Ascending,
        Quarantine = QuarantineFilterValues.All
    };

    #region Export Success Tests

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithValidRole_ReturnsWorkbookWithSiteData()
    {
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.FileStream.Length > 0);
        Assert.Equal(0, result.FileStream.Position);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(3, sites.Count);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithoutNotFoundDomains_OmitsNotFoundSheet()
    {
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sheetNames = XlsxTestWorkbook.GetSheetNames(result.FileStream);

        Assert.Equal(["Sites", "Export info"], sheetNames);
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_NoActiveFilters_WritesNotFoundDomainsToSeparateSheet()
    {
        // Arrange
        var query = DefaultQuery();

        // Act
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "test.com missing-b.com example.com missing-a.com",
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var siteRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        var notFoundRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Not found");

        Assert.Equal(["test.com", "example.com"], siteRows.Select(row => row["Domain"]).ToList());
        Assert.Equal(["missing-b.com", "missing-a.com"], notFoundRows.Select(row => row["Domain"]).ToList());
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_WithNonDomainSort_SortsFoundSitesAndKeepsNotFoundInputOrder()
    {
        // Arrange
        var query = DefaultQuery();
        query.SortBy = SortFields.Traffic;
        query.SortDir = SortingDefaults.Descending;

        // Act
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "example.com missing-b.com test.com missing-a.com gambling.com",
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var siteRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        var notFoundRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Not found");

        Assert.Equal(
            ["gambling.com", "test.com", "example.com"],
            siteRows.Select(row => row["Domain"]).ToArray());
        Assert.Equal(["missing-b.com", "missing-a.com"], notFoundRows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_WithInputOrderSort_AppliesLimitAfterInputOrdering()
    {
        // Arrange
        var internalSettings = await _context.RoleSettings.SingleAsync(rs => rs.RoleName == AppRoles.Internal);
        internalSettings.ExportLimitRows = 1;
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "test.com example.com",
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Internal,
            CancellationToken.None);

        // Assert
        var siteRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        Assert.Equal(["test.com"], siteRows.Select(row => row["Domain"]).ToArray());
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_AllDomainsFound_OmitsNotFoundSheetAndMetadata()
    {
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "example.com test.com",
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sheetNames = XlsxTestWorkbook.GetSheetNames(result.FileStream);
        var infoRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Export info")
            .ToDictionary(row => row["Property"], row => row["Value"]);

        Assert.Equal(["Sites", "Export info"], sheetNames);
        Assert.False(infoRows.ContainsKey("Not found sheet rows"));
        Assert.False(infoRows.ContainsKey("Not found included"));
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WhenTruncated_WritesLimitDetailsToExportInfoSheet()
    {
        var internalSettings = await _context.RoleSettings.SingleAsync(rs => rs.RoleName == AppRoles.Internal);
        internalSettings.ExportLimitRows = 1;
        await _context.SaveChangesAsync();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Internal,
            CancellationToken.None);

        var infoRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Export info")
            .ToDictionary(row => row["Property"], row => row["Value"]);

        Assert.True(result.Truncated);
        Assert.Equal("1", infoRows["Export limit rows"]);
        Assert.Equal("TRUE", infoRows["Export truncated by limit"]);
        Assert.Equal("3", infoRows["Rows matching export request"]);
        Assert.Equal("1", infoRows["Rows in Sites sheet"]);
        Assert.False(infoRows.ContainsKey("Limit note"));
        Assert.False(infoRows.ContainsKey("Filters"));
        Assert.False(infoRows.ContainsKey("Sort"));
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithFilters_ExportsOnlyMatchingSites()
    {
        var query = new SitesQuery
        {
            DrMin = 60,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(2, sites.Count); // test.com(70) and gambling.com(90)
        Assert.All(sites, site => Assert.True(site.DR >= 60));
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_DisplaysCanonicalUnknownAndOtherLocations()
    {
        // Arrange
        _context.Sites.AddRange(
            SiteWithLocation("unknown-location.com", LocationConstants.UnknownLocationKey, "UNKNOWN"),
            SiteWithLocation("other-location.com", null, "US/CA"));
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Equal("United States", rows.Single(row => row["Domain"] == "example.com")["Location"]);
        Assert.Equal("United Kingdom", rows.Single(row => row["Domain"] == "test.com")["Location"]);
        Assert.Equal("Unknown", rows.Single(row => row["Domain"] == "unknown-location.com")["Location"]);
        Assert.Equal("Other", rows.Single(row => row["Domain"] == "other-location.com")["Location"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithLocationFilters_ExportsMatchingCanonicalLocations()
    {
        // Arrange
        _context.Sites.Add(SiteWithLocation("other-location.com", null, "US/CA"));
        await _context.SaveChangesAsync();

        var query = DefaultQuery();
        query.LocationKeys = ["GB"];
        query.IncludeOtherLocation = true;

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Equal(["other-location.com", "test.com"], rows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithLocationGroupFilter_ExportsGroupMembers()
    {
        // Arrange
        var query = DefaultQuery();
        query.LocationGroupKeys = ["western"];

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Equal(["example.com", "gambling.com", "test.com"], rows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithStopList_ExcludesMatchingDomains()
    {
        var query = new SitesQuery
        {
            StopListDomains = new List<string> { "test.com" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(2, result.RequestedRows);
        Assert.Equal(["example.com", "gambling.com"], sites.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_WithStopList_ThrowsRequestValidationException()
    {
        var query = DefaultQuery();
        query.StopListDomains = new List<string> { "example.com" };

        var ex = await Assert.ThrowsAsync<RequestValidationException>(
            () => _service.ExportMultiSearchAsExcelAsync(
                "example.com",
                query,
                TestUserId,
                TestUserEmail,
                AppRoles.Admin,
                CancellationToken.None));

        Assert.Equal(StopListConstants.MultiSearchNotSupportedMessage, ex.Message);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithNicheFilter_ExportsOnlyMatchingSites()
    {
        var query = new SitesQuery
        {
            Niches = new List<string> { "casino" },
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Single(sites);
        Assert.Equal("gambling.com", sites[0].Domain);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithCategorySearchFilter_ExportsOnlyMatchingSites()
    {
        var query = new SitesQuery
        {
            CategorySearchTerms = ["gambling"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Single(sites);
        Assert.Equal("gambling.com", sites[0].Domain);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithTopicFitExpandMode_ExportsNicheOrCategoryMatches()
    {
        // Arrange
        var query = new SitesQuery
        {
            Niches = ["tech"],
            CategorySearchTerms = ["news"],
            TopicFitMode = TopicFitModeValues.Expand,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(["example.com", "test.com"], sites.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithExcludedNicheFilter_ExportsOnlyNonMatchingSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            ExcludedNiches = ["casino"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(["example.com", "test.com"], sites.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithExcludedCategorySearchFilter_ExportsOnlyNonMatchingSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            ExcludedCategorySearchTerms = ["news"],
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(["example.com", "gambling.com"], sites.Select(site => site.Domain).ToArray());
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_WithCategorySearchFilter_ExportsFilteredFoundSitesAndNotFoundInputOrder()
    {
        // Arrange
        var query = DefaultQuery();
        query.CategorySearchTerms = ["news"];

        // Act
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "missing-a.com example.com test.com missing-b.com",
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var siteRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        var notFoundRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Not found");

        Assert.Equal(["test.com"], siteRows.Select(row => row["Domain"]).ToArray());
        Assert.Equal(["missing-a.com", "missing-b.com"], notFoundRows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_WithExcludedNicheFilter_ExportsFilteredFoundSitesAndNotFoundInputOrder()
    {
        // Arrange
        var query = DefaultQuery();
        query.ExcludedNiches = ["casino"];

        // Act
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "missing-b.com example.com gambling.com missing-a.com",
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var siteRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        var notFoundRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Not found");

        Assert.Equal(["example.com"], siteRows.Select(row => row["Domain"]).ToArray());
        Assert.Equal(["missing-b.com", "missing-a.com"], notFoundRows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithCasinoAvailabilityNotAvailable_FiltersByStatus()
    {
        _context.Sites.Add(new Site
        {
            Domain = "no-casino.com",
            DR = 20,
            Traffic = 1000,
            Location = "US",
            PriceUsd = 25m,
            PriceCasino = null,
            PriceCasinoStatus = ServiceAvailabilityStatus.NotAvailable,
            PriceCrypto = null,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsert = null,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            IsQuarantined = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.NotAvailable],
            Page = 1,
            PageSize = 100,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Single(sites);
        Assert.Equal("no-casino.com", sites[0].Domain);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_AvailableWithUnknownPrice_WritesYes()
    {
        // Arrange
        _context.Sites.Add(new Site
        {
            Domain = "yes-casino.com",
            DR = 20,
            Traffic = 1000,
            Location = "US",
            PriceUsd = 25m,
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

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        var row = rows.Single(r => r["Domain"] == "yes-casino.com");
        Assert.Equal("YES", row["Casino"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithCasinoAvailabilityAvailable_ExportsOnlyAvailableSites()
    {
        // Arrange
        _context.Sites.Add(new Site
        {
            Domain = "yes-casino.com",
            DR = 20,
            Traffic = 1000,
            Location = "US",
            PriceUsd = 25m,
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

        var query = DefaultQuery();
        query.CasinoAvailability = [ServiceAvailabilityStatus.Available];

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Equal(["example.com", "gambling.com"], rows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithCasinoAvailabilityAvailableWithUnknownPrice_ExportsOnlyMatchingSites()
    {
        // Arrange
        _context.Sites.Add(new Site
        {
            Domain = "yes-casino.com",
            DR = 20,
            Traffic = 1000,
            Location = "US",
            PriceUsd = 25m,
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

        var query = DefaultQuery();
        query.CasinoAvailability = [ServiceAvailabilityStatus.AvailableWithUnknownPrice];

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Equal(["yes-casino.com"], rows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithMultipleCasinoAvailabilityValues_ExportsOnlyMatchingSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            CasinoAvailability = [ServiceAvailabilityStatus.NotAvailable, ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 100,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Equal(["test.com"], rows.Select(row => row["Domain"]).ToArray());
        Assert.Equal(1, result.RequestedRows);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithLinkInsertCasinoAvailabilityNotAvailable_FiltersByStatus()
    {
        var query = new SitesQuery
        {
            LinkInsertCasinoAvailability = [ServiceAvailabilityStatus.NotAvailable],
            Page = 1,
            PageSize = 100,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(["gambling.com", "test.com"], sites.Select(s => s.Domain).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithDatingAvailabilityUnknown_FiltersByStatus()
    {
        var query = new SitesQuery
        {
            DatingAvailability = [ServiceAvailabilityStatus.Unknown],
            Page = 1,
            PageSize = 100,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Single(sites);
        Assert.Equal("test.com", sites[0].Domain);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithQuarantineExclude_ExcludesQuarantinedSites()
    {
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.Exclude
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(2, sites.Count); // example.com and test.com
        Assert.All(sites, site => Assert.False(site.IsQuarantined));
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithSorting_ReturnsSortedData()
    {
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.DR,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(3, sites.Count);
        Assert.Equal(90, sites[0].DR); // gambling.com
        Assert.Equal(70, sites[1].DR); // test.com
        Assert.Equal(50, sites[2].DR); // example.com
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithLastPublishedDateFilterAndSort_UsesSitesQueryLogic()
    {
        SetLastPublishedDates();
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.LastPublishedDate,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All,
            LastPublishedFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastPublishedToExclusive = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);

        Assert.Equal(["example.com", "test.com"], rows.Select(row => row["Domain"]).ToList());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_LastPublished_UsesUiDisplayValues()
    {
        SetLastPublishedDates();
        await _context.SaveChangesAsync();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);

        Assert.Equal("15.01.2025", rows.Single(row => row["Domain"] == "example.com")["Last Published"]);
        Assert.Equal("January 2025", rows.Single(row => row["Domain"] == "test.com")["Last Published"]);
        Assert.Equal("Before January 2026", rows.Single(row => row["Domain"] == "gambling.com")["Last Published"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_AuditColumns_UseDateFormatAndSystemFallbacks()
    {
        // Arrange
        var site = await _context.Sites.SingleAsync(s => s.Domain == "example.com");
        site.CreatedAtUtc = new DateTime(2025, 4, 5, 13, 45, 0, DateTimeKind.Utc);
        site.UpdatedAtUtc = new DateTime(2026, 6, 7, 8, 30, 0, DateTimeKind.Utc);
        site.CreatedBy = null;
        site.UpdatedBy = "   ";
        await _context.SaveChangesAsync();

        var visibleColumnKeys = new[] { "domain", "createdAt", "updatedAt", "createdBy", "updatedBy" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        var row = rows.Single(row => row["Domain"] == "example.com");
        Assert.Equal("05.04.2025", row["Created Date"]);
        Assert.Equal("07.06.2026", row["Updated Date"]);
        Assert.Equal("system", row["Created By"]);
        Assert.Equal("system", row["Updated By"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithClientRole_UsesExpectedHeaderOrder()
    {
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(
            [
                "Domain",
                "DR",
                "Traffic",
                "Location",
                "Price USD",
                "Casino",
                "Crypto",
                "Link Insert",
                "Link Insert Casino",
                "Dating",
                "Niche",
                "Categories",
                "DF Links",
                "Sponsored Tag",
                "Term",
                "Language",
                "Status",
                "Last Published",
                "Created Date"
            ],
            headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_ExportsOnlyRequestedClientAllowedVisibleColumns()
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", "priceUsd", "isQuarantined" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(["Domain", "Price USD", "Status"], headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_WhenInternalOnlyColumnRequested_ThrowsValidationError()
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", "quarantineReason", "updatedAt", "createdBy", "updatedBy" };

        // Act
        var act = () => _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<RequestValidationException>(act);
        Assert.Contains("quarantineReason", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("updatedAt")]
    [InlineData("createdBy")]
    [InlineData("updatedBy")]
    public async Task ExportSitesAsExcelAsync_ClientRole_WhenInternalAuditColumnRequested_ThrowsValidationError(string columnKey)
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", columnKey };

        // Act
        var act = () => _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<RequestValidationException>(act);
        Assert.Contains(columnKey, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task ExportSitesAsExcelAsync_AdminRoles_CanExportInternalAuditColumns(string role)
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", "updatedAt", "createdBy", "updatedBy" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            role,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(["Domain", "Updated Date", "Created By", "Updated By"], headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_InternalRole_CanExportQuarantineRestrictedAuditColumns()
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", "quarantineReason", "updatedAt", "createdBy", "updatedBy" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Internal,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(["Domain", "Quarantine reason", "Updated Date", "Created By", "Updated By"], headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WhenUnknownColumnKeyRequested_ThrowsValidationError()
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", "unknownColumn" };

        // Act
        var act = () => _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<RequestValidationException>(act);
        Assert.Contains("Unknown export column key", ex.Message, StringComparison.Ordinal);
        Assert.Contains("unknownColumn", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WhenDuplicateColumnKeyRequested_ExportsDistinctColumnsInFirstSeenOrder()
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", "traffic", "traffic", "dr" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(["Domain", "Traffic", "DR"], headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WhenVisibleColumnKeysEmpty_ThrowsValidationError()
    {
        // Arrange
        var visibleColumnKeys = Array.Empty<string>();

        // Act
        var act = () => _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<RequestValidationException>(act);
        Assert.Contains("At least one visible column key is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_HeadersMatchRequestedVisibleColumnOrder()
    {
        // Arrange
        var visibleColumnKeys = new[] { "traffic", "createdAt", "domain", "updatedAt", "dr" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(["Traffic", "Created Date", "Domain", "Updated Date", "DR"], headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_HiddenColumnsAreNotExported()
    {
        // Arrange
        var visibleColumnKeys = new[] { "domain", "traffic" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(["Domain", "Traffic"], headers);
        Assert.DoesNotContain("Price USD", headers);
        Assert.DoesNotContain("DR", headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithVisibleColumns_PreservesSearchFilterAndSort()
    {
        // Arrange
        var query = new SitesQuery
        {
            Search = "com",
            DrMin = 60,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.DR,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        };
        var visibleColumnKeys = new[] { "domain", "dr" };

        // Act
        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            visibleColumnKeys,
            CancellationToken.None);

        // Assert
        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Equal(["gambling.com", "test.com"], rows.Select(row => row["Domain"]).ToArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithAdminRole_UsesExpectedHeaderOrder()
    {
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var headers = await ReadSitesSheetHeaderFromStream(result.FileStream);
        Assert.Equal(
            [
                "Domain",
                "DR",
                "Traffic",
                "Location",
                "Price USD",
                "Casino",
                "Crypto",
                "Link Insert",
                "Link Insert Casino",
                "Dating",
                "Niche",
                "Categories",
                "DF Links",
                "Sponsored Tag",
                "Term",
                "Language",
                "Status",
                "Last Published",
                "Created Date",
                "Quarantine reason",
                "Updated Date",
                "Created By",
                "Updated By"
            ],
            headers);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_NotAvailableAndUnknown_AreExportedAsNoAndEmpty()
    {
        _context.Sites.Add(new Site
        {
            Domain = "status-export.com",
            DR = 10,
            Traffic = 100,
            Location = "US",
            PriceUsd = 5m,
            PriceCasino = null,
            PriceCasinoStatus = ServiceAvailabilityStatus.NotAvailable,
            PriceCrypto = null,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsert = 12m,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Available,
            PriceLinkInsertCasino = null,
            PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.NotAvailable,
            PriceDating = null,
            PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
            NumberDFLinks = 4,
            TermType = TermType.Finite,
            TermValue = 1,
            TermUnit = TermUnit.Year,
            IsQuarantined = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            Search = "status-export.com",
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Single(rows);
        Assert.Equal("NO", rows[0]["Casino"]);
        Assert.Equal(string.Empty, rows[0]["Crypto"]);
        Assert.Equal("12", rows[0]["Link Insert"]);
        Assert.Equal("NO", rows[0]["Link Insert Casino"]);
        Assert.Equal(string.Empty, rows[0]["Dating"]);
        Assert.Equal("4", rows[0]["DF Links"]);
        Assert.Equal("1 year", rows[0]["Term"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_Status_UsesUiAvailabilityLabels()
    {
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);

        Assert.Equal("Available", rows.Single(row => row["Domain"] == "example.com")["Status"]);
        Assert.Equal("Unavailable", rows.Single(row => row["Domain"] == "gambling.com")["Status"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_Language_WritesStoredValueAndUnknownForNull()
    {
        _context.Sites.Add(new Site
        {
            Domain = "multi-language.com",
            DR = 10,
            Traffic = 100,
            Location = "US",
            Language = "MULTI",
            PriceUsd = 10m,
            PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
            IsQuarantined = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);

        Assert.Equal("EN", rows.Single(row => row["Domain"] == "example.com")["Language"]);
        Assert.Equal("UNKNOWN", rows.Single(row => row["Domain"] == "test.com")["Language"]);
        Assert.Equal("UNKNOWN", rows.Single(row => row["Domain"] == "gambling.com")["Language"]);
        Assert.Equal("MULTI", rows.Single(row => row["Domain"] == "multi-language.com")["Language"]);
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_Language_IncludesColumnForFoundRowsAndKeepsNotFoundSheetDomainOnly()
    {
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "example.com test.com missing.com",
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var siteHeaders = await ReadSitesSheetHeaderFromStream(result.FileStream);
        var siteRows = await ReadSitesSheetRowsFromStream(result.FileStream);
        var notFoundHeaders = XlsxTestWorkbook.ReadHeaders(result.FileStream, "Not found");
        var notFoundRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Not found");

        Assert.Equal("Language", siteHeaders[siteHeaders.IndexOf("Term") + 1]);
        Assert.Equal("EN", siteRows.Single(row => row["Domain"] == "example.com")["Language"]);
        Assert.Equal("UNKNOWN", siteRows.Single(row => row["Domain"] == "test.com")["Language"]);
        Assert.Equal(["Domain"], notFoundHeaders);
        Assert.Single(notFoundRows);
        Assert.Equal("missing.com", notFoundRows[0]["Domain"]);
    }

    #endregion

    #region Export Policy Tests

    [Fact]
    public async Task ExportSitesAsExcelAsync_Unlimited_AllRowsExported_TruncatedFalse()
    {
        // Admin role is Unlimited — all 3 seeded sites should be exported
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        Assert.Equal(3, result.RequestedRows);
        Assert.Equal(3, result.ExportedRows);
        Assert.False(result.Truncated);
        Assert.Null(result.LimitRows);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_Limited_UnderLimit_AllRowsExported_TruncatedFalse()
    {
        // Client role has limit 5000; only 3 sites seeded — no truncation
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        Assert.Equal(3, result.RequestedRows);
        Assert.Equal(3, result.ExportedRows);
        Assert.False(result.Truncated);
        Assert.Equal(5000, result.LimitRows);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_Limited_OverLimit_TruncatedTrue_HeadersCorrect()
    {
        // Create a role with limit 2 — 3 sites seeded, so 1 is truncated
        _context.RoleSettings.Add(new RoleSettings
        {
            RoleName = "TinyLimitRole",
            ExportLimitMode = ExportLimitMode.Limited,
            ExportLimitRows = 2
        });
        await _context.SaveChangesAsync();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            "TinyLimitRole",
            CancellationToken.None);

        Assert.Equal(3, result.RequestedRows);
        Assert.Equal(2, result.ExportedRows);
        Assert.True(result.Truncated);
        Assert.Equal(2, result.LimitRows);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(2, sites.Count);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithDisabledRole_ThrowsExportDisabledException()
    {
        var exception = await Assert.ThrowsAsync<ExportDisabledException>(
            () => _service.ExportSitesAsExcelAsync(
                DefaultQuery(),
                TestUserId,
                TestUserEmail,
                "DisabledRole",
                CancellationToken.None));

        Assert.Equal(ExportConstants.ExportDisabledMessage, exception.Message);
        Assert.Equal("DisabledRole", exception.RoleName);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_UserOverride_Disabled_ThrowsExportDisabledException()
    {
        // User has a Disabled override even though role (Admin) is Unlimited
        var user = new ApplicationUser
        {
            Id = TestUserId,
            UserName = TestUserEmail,
            Email = TestUserEmail,
            ExportLimitOverrideMode = ExportLimitMode.Disabled
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<ExportDisabledException>(
            () => _service.ExportSitesAsExcelAsync(
                DefaultQuery(),
                TestUserId,
                TestUserEmail,
                AppRoles.Admin,
                CancellationToken.None));
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_UserOverride_Limited_OverridesRolePolicy()
    {
        // User has override limit of 1; role (Admin) is Unlimited — override wins
        var user = new ApplicationUser
        {
            Id = TestUserId,
            UserName = TestUserEmail,
            Email = TestUserEmail,
            ExportLimitOverrideMode = ExportLimitMode.Limited,
            ExportLimitRowsOverride = 1
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        Assert.Equal(3, result.RequestedRows);
        Assert.Equal(1, result.ExportedRows);
        Assert.True(result.Truncated);
        Assert.Equal(1, result.LimitRows);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithLimitSmallerThanResults_EnforcesLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            _context.Sites.Add(new Site
            {
                Domain = $"site{i}.com",
                DR = 40,
                Traffic = 5000,
                Location = "US",
                PriceUsd = 50m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        _context.RoleSettings.Add(new RoleSettings
        {
            RoleName = "LimitedRole",
            ExportLimitMode = ExportLimitMode.Limited,
            ExportLimitRows = 5
        });
        await _context.SaveChangesAsync();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            "LimitedRole",
            CancellationToken.None);

        var sites = await ReadSitesSheetFromStream(result.FileStream);
        Assert.Equal(5, sites.Count);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_WithNonExistentRole_ThrowsRoleSettingsNotFoundException()
    {
        var exception = await Assert.ThrowsAsync<RoleSettingsNotFoundException>(
            () => _service.ExportSitesAsExcelAsync(
                DefaultQuery(),
                TestUserId,
                TestUserEmail,
                "NonExistentRole",
                CancellationToken.None));

        Assert.Contains("Role settings not found", exception.Message);
        Assert.Equal("NonExistentRole", exception.RoleName);
    }

    #endregion

    #region Export Log Tests

    [Fact]
    public async Task ExportSitesAsExcelAsync_CreatesExportLog()
    {
        var query = new SitesQuery
        {
            DrMin = 60,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var exportLog = await _context.ExportLogs.FirstOrDefaultAsync();
        Assert.NotNull(exportLog);
        Assert.Equal(TestUserId, exportLog.UserId);
        Assert.Equal(TestUserEmail, exportLog.UserEmail);
        Assert.Equal(AppRoles.Admin, exportLog.Role);
        Assert.Equal(2, exportLog.RowsReturned); // 2 sites match DR >= 60
        Assert.NotNull(exportLog.FilterSummaryJson);
        Assert.Contains("\"DrMin\":60", exportLog.FilterSummaryJson);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_LogsCorrectRowCount()
    {
        var query = new SitesQuery
        {
            Quarantine = QuarantineFilterValues.Exclude,
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending
        };

        await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var exportLog = await _context.ExportLogs.FirstOrDefaultAsync();
        Assert.NotNull(exportLog);
        Assert.Equal(2, exportLog.RowsReturned); // 2 non-quarantined sites
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_CreatesExportLogAndAnalyticsSnapshot()
    {
        await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var exportLog = await _context.ExportLogs.SingleAsync();
        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();

        Assert.Equal(exportLog.Id, snapshot.ExportLogId);
        Assert.Equal(ExportAnalyticsSnapshotBuilder.CurrentSnapshotVersion, snapshot.SnapshotVersion);
        Assert.Equal(exportLog.TimestampUtc, snapshot.CreatedAtUtc);
        Assert.Equal(3, exportLog.RowsReturned);
        Assert.NotNull(exportLog.FilterSummaryJson);
        Assert.NotNull(snapshot.FiltersSnapshotJson);
        Assert.NotNull(snapshot.SortSnapshotJson);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_StoresActiveFiltersSnapshot()
    {
        var query = new SitesQuery
        {
            DrMin = 30,
            DrMax = 80,
            TrafficMin = 1000,
            PriceMax = 250m,
            Locations = ["US"],
            Languages = ["EN", "UNKNOWN"],
            Niches = ["Casino", " crypto ", "casino"],
            CategorySearchTerms = [" Sports Betting ", "crypto", "SPORTS BETTING"],
            TopicFitMode = TopicFitModeValues.Expand,
            ExcludedNiches = ["News"],
            ExcludedCategorySearchTerms = ["adult"],
            CasinoAvailability = [ServiceAvailabilityStatus.Available],
            LinkInsertAvailability = [ServiceAvailabilityStatus.Available],
            StopListDomains = ["test.com"],
            Quarantine = QuarantineFilterValues.Exclude,
            LastPublishedToExclusive = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending
        };

        await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();
        using var document = JsonDocument.Parse(snapshot.FiltersSnapshotJson);
        var root = document.RootElement;
        var filters = root.GetProperty("filters").EnumerateArray().ToArray();

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "dr" &&
            filter.GetProperty("kind").GetString() == "numberRange" &&
            filter.GetProperty("operator").GetString() == "between" &&
            filter.GetProperty("value").GetProperty("min").GetDecimal() == 30m &&
            filter.GetProperty("value").GetProperty("max").GetDecimal() == 80m);
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "traffic" &&
            filter.GetProperty("operator").GetString() == "gte" &&
            filter.GetProperty("value").GetProperty("min").GetDecimal() == 1000m);
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "priceUsd" &&
            filter.GetProperty("operator").GetString() == "lte" &&
            filter.GetProperty("value").GetProperty("max").GetDecimal() == 250m);
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "niche" &&
            filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["casino", "crypto"]));
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "categories" &&
            filter.GetProperty("kind").GetString() == "textSearch" &&
            filter.GetProperty("operator").GetString() == "containsAny" &&
            filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["Sports Betting", "crypto"]));
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "topicFitMode" &&
            filter.GetProperty("kind").GetString() == "enum" &&
            filter.GetProperty("operator").GetString() == "eq" &&
            filter.GetProperty("value").GetString() == TopicFitModeValues.Expand);
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "excludedNiche" &&
            filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["news"]));
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "excludedCategories" &&
            filter.GetProperty("kind").GetString() == "textSearch" &&
            filter.GetProperty("operator").GetString() == "notContainsAny" &&
            filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["adult"]));
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "language" &&
            filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["EN", "UNKNOWN"]));
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "stopList" &&
            filter.GetProperty("kind").GetString() == "boolean" &&
            filter.GetProperty("value").GetBoolean());
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "priceCasinoAvailability" &&
            filter.GetProperty("operator").GetString() == "in" &&
            filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["available"]));
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "priceLinkInsertAvailability" &&
            filter.GetProperty("operator").GetString() == "in" &&
            filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["available"]));
        Assert.Contains(filters, filter =>
            filter.GetProperty("field").GetString() == "lastPublishedDate" &&
            filter.GetProperty("kind").GetString() == "monthRange" &&
            filter.GetProperty("operator").GetString() == "before" &&
            filter.GetProperty("value").GetProperty("month").GetString() == "2026-01");
        Assert.DoesNotContain("test.com", snapshot.FiltersSnapshotJson);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_StoresEmptyFiltersArrayWhenNoFiltersActive()
    {
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = string.Empty,
            SortDir = string.Empty,
            Quarantine = QuarantineFilterValues.All
        };

        await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();
        using var document = JsonDocument.Parse(snapshot.FiltersSnapshotJson);

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("filters").EnumerateArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_StoresActiveSortSnapshot()
    {
        var query = DefaultQuery();
        query.SortBy = SortFields.Traffic;
        query.SortDir = SortingDefaults.Descending;

        await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();
        using var document = JsonDocument.Parse(snapshot.SortSnapshotJson);
        var sorts = document.RootElement.GetProperty("sorts").EnumerateArray().ToArray();

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Single(sorts);
        Assert.Equal("traffic", sorts[0].GetProperty("field").GetString());
        Assert.Equal("desc", sorts[0].GetProperty("direction").GetString());
        Assert.Equal(1, sorts[0].GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_StoresEmptySortsArrayWhenNoSortActive()
    {
        var query = DefaultQuery();
        query.SortBy = string.Empty;
        query.SortDir = string.Empty;

        await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();
        using var document = JsonDocument.Parse(snapshot.SortSnapshotJson);

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("sorts").EnumerateArray());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_StoresCatalogSearchSnapshotWhenSearchIsAvailable()
    {
        var query = DefaultQuery();
        query.Search = " Casino USA ";

        await _service.ExportSitesAsExcelAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();
        Assert.NotNull(snapshot.SearchSnapshotJson);
        using var document = JsonDocument.Parse(snapshot.SearchSnapshotJson);

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("catalogSearch", document.RootElement.GetProperty("mode").GetString());
        Assert.Equal("Casino USA", document.RootElement.GetProperty("query").GetString());
        Assert.Equal("casino usa", document.RootElement.GetProperty("normalizedQuery").GetString());
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_ClientRole_StoresNullSearchSnapshotWhenSearchIsNotAvailable()
    {
        await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();

        Assert.Null(snapshot.SearchSnapshotJson);
    }

    [Fact]
    public async Task ExportMultiSearchAsExcelAsync_ClientRole_StoresMultiSearchSnapshotWithoutDomains()
    {
        await _service.ExportMultiSearchAsExcelAsync(
            "example.com missing.com example.com",
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        var snapshot = await _context.ExportAnalyticsSnapshots.SingleAsync();
        Assert.NotNull(snapshot.SearchSnapshotJson);
        using var document = JsonDocument.Parse(snapshot.SearchSnapshotJson);

        Assert.Equal("multiSearch", document.RootElement.GetProperty("mode").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("inputCount").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("uniqueInputCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("foundCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("notFoundCount").GetInt32());
        Assert.DoesNotContain("example.com", snapshot.SearchSnapshotJson);
        Assert.DoesNotContain("missing.com", snapshot.SearchSnapshotJson);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_NonClientRole_DoesNotCreateAnalyticsSnapshot()
    {
        await _service.ExportSitesAsExcelAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        Assert.Single(await _context.ExportLogs.ToListAsync());
        Assert.Empty(await _context.ExportAnalyticsSnapshots.ToListAsync());
    }

    [Fact]
    public void ExportAnalyticsSnapshot_ExportLogId_HasUniqueIndex()
    {
        var entityType = _context.Model.FindEntityType(typeof(ExportAnalyticsSnapshot));
        Assert.NotNull(entityType);

        var index = entityType.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(ExportAnalyticsSnapshot.ExportLogId));

        Assert.True(index.IsUnique);
    }

    #endregion

    #region Helper Methods

    private static async Task<List<ExportedSiteRow>> ReadSitesSheetFromStream(Stream stream)
    {
        await Task.CompletedTask;
        return XlsxTestWorkbook.ReadRows(stream, "Sites")
            .Select(MapExportedSiteRow)
            .ToList();
    }

    private static async Task<List<Dictionary<string, string>>> ReadSitesSheetRowsFromStream(Stream stream)
    {
        await Task.CompletedTask;
        return XlsxTestWorkbook.ReadRows(stream, "Sites");
    }

    private static async Task<List<string>> ReadSitesSheetHeaderFromStream(Stream stream)
    {
        await Task.CompletedTask;
        return XlsxTestWorkbook.ReadHeaders(stream, "Sites");
    }

    private static ExportedSiteRow MapExportedSiteRow(IReadOnlyDictionary<string, string> row)
        => new()
        {
            Domain = row["Domain"],
            DR = double.Parse(row["DR"], CultureInfo.InvariantCulture),
            Traffic = long.Parse(row["Traffic"], CultureInfo.InvariantCulture),
            Location = row["Location"],
            Language = row["Language"],
            PriceUsd = string.IsNullOrEmpty(row["Price USD"]) ? 0 : decimal.Parse(row["Price USD"], CultureInfo.InvariantCulture),
            PriceCasino = row["Casino"],
            PriceCrypto = row["Crypto"],
            PriceLinkInsert = row["Link Insert"],
            PriceLinkInsertCasino = row["Link Insert Casino"],
            PriceDating = row["Dating"],
            NumberDFLinks = string.IsNullOrEmpty(row["DF Links"]) ? null : int.Parse(row["DF Links"], CultureInfo.InvariantCulture),
            Term = row["Term"],
            Niche = row["Niche"],
            Categories = row["Categories"],
            SponsoredTag = row["Sponsored Tag"],
            IsQuarantined = string.Equals(GetOptionalValue(row, "Status"), "Unavailable", StringComparison.OrdinalIgnoreCase),
            QuarantineReason = GetOptionalValue(row, "Quarantine reason"),
        };

    private static string? GetOptionalValue(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private void SetLastPublishedDates()
    {
        SetLastPublishedDate("example.com", new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        SetLastPublishedDate("test.com", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), isMonthOnly: true);
        SetLastPublishedDate("gambling.com", null);
    }

    private void SetLastPublishedDate(string domain, DateTime? value, bool isMonthOnly = false)
    {
        var site = _context.Sites.Single(s => s.Domain == domain);
        site.LastPublishedDate = value;
        site.LastPublishedDateIsMonthOnly = value.HasValue && isMonthOnly;
    }

    private sealed class ExportedSiteRow
    {
        public string Domain { get; set; } = string.Empty;
        public double DR { get; set; }
        public long Traffic { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public decimal PriceUsd { get; set; }
        public string PriceCasino { get; set; } = string.Empty;
        public string PriceCrypto { get; set; } = string.Empty;
        public string PriceLinkInsert { get; set; } = string.Empty;
        public string PriceLinkInsertCasino { get; set; } = string.Empty;
        public string PriceDating { get; set; } = string.Empty;
        public int? NumberDFLinks { get; set; }
        public string Term { get; set; } = string.Empty;
        public string? Niche { get; set; }
        public string? Categories { get; set; }
        public string? SponsoredTag { get; set; }
        public bool IsQuarantined { get; set; }
        public string? QuarantineReason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? LastPublishedDate { get; set; }
    }

    #endregion

    #region PriceUsd nullable

    [Fact]
    public async Task ExportSitesAsExcelAsync_NullPriceUsd_WritesEmptyCell()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(), TestUserId, TestUserEmail, AppRoles.Admin, CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        var row = rows.Single(r => r["Domain"] == "null-price.com");
        Assert.Equal(string.Empty, row["Price USD"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_NumericPriceUsd_WritesNumericValue()
    {
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(), TestUserId, TestUserEmail, AppRoles.Admin, CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        var row = rows.Single(r => r["Domain"] == "example.com");
        Assert.Equal("100", row["Price USD"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_PriceMinFilter_ExcludesNullPriceUsd()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var query = new SitesQuery
        {
            Page = 1, PageSize = 10, PriceMin = 50m,
            SortBy = SortFields.Domain, SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _service.ExportSitesAsExcelAsync(
            query, TestUserId, TestUserEmail, AppRoles.Admin, CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.DoesNotContain(rows, r => r["Domain"] == "null-price.com");
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_NoPriceFilter_IncludesNullPriceUsd()
    {
        _context.Sites.Add(SiteWithNullPrice("null-price.com"));
        _context.SaveChanges();

        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(), TestUserId, TestUserEmail, AppRoles.Admin, CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        Assert.Contains(rows, r => r["Domain"] == "null-price.com");
    }

    #endregion

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

    private static Site SiteWithLocation(string domain, string? locationKey, string rawLocation) => new()
    {
        Domain = domain,
        DR = 50,
        Traffic = 10000,
        Location = rawLocation,
        LocationKey = locationKey,
        ImportedLocationRaw = locationKey == LocationConstants.UnknownLocationKey ? null : rawLocation,
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

