using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
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
        var queryBuilder = new SitesQueryBuilder();
        _service = new ExportService(_context, queryBuilder);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
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
        var result = await _service.ExportMultiSearchAsExcelAsync(
            "example.com missing.com test.com",
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        var siteRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        var notFoundRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Not found");

        Assert.Equal(["example.com", "test.com"], siteRows.Select(row => row["Domain"]).ToList());
        Assert.Single(notFoundRows);
        Assert.Equal("missing.com", notFoundRows[0]["Domain"]);
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
            CasinoAvailability = ServiceAvailabilityFilter.NotAvailable,
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
    public async Task ExportSitesAsExcelAsync_WithLinkInsertCasinoAvailabilityNotAvailable_FiltersByStatus()
    {
        var query = new SitesQuery
        {
            LinkInsertCasinoAvailability = ServiceAvailabilityFilter.NotAvailable,
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
            DatingAvailability = ServiceAvailabilityFilter.Unknown,
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
                "PriceUsd",
                "PriceCasino",
                "PriceCrypto",
                "PriceLinkInsert",
                "PriceLinkInsertCasino",
                "PriceDating",
                "Niche",
                "Categories",
                "NumberDFLinks",
                "SponsoredTag",
                "Term"
            ],
            headers);
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
                "PriceUsd",
                "PriceCasino",
                "PriceCrypto",
                "PriceLinkInsert",
                "PriceLinkInsertCasino",
                "PriceDating",
                "Niche",
                "Categories",
                "NumberDFLinks",
                "SponsoredTag",
                "Term",
                "IsQuarantined",
                "QuarantineReason",
                "LastPublishedDate",
                "CreatedAtUtc",
                "UpdatedAtUtc"
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
        Assert.Equal("NO", rows[0]["PriceCasino"]);
        Assert.Equal(string.Empty, rows[0]["PriceCrypto"]);
        Assert.Equal("12", rows[0]["PriceLinkInsert"]);
        Assert.Equal("NO", rows[0]["PriceLinkInsertCasino"]);
        Assert.Equal(string.Empty, rows[0]["PriceDating"]);
        Assert.Equal("4", rows[0]["NumberDFLinks"]);
        Assert.Equal("1 year", rows[0]["Term"]);
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
            PriceUsd = string.IsNullOrEmpty(row["PriceUsd"]) ? 0 : decimal.Parse(row["PriceUsd"], CultureInfo.InvariantCulture),
            PriceCasino = row["PriceCasino"],
            PriceCrypto = row["PriceCrypto"],
            PriceLinkInsert = row["PriceLinkInsert"],
            PriceLinkInsertCasino = row["PriceLinkInsertCasino"],
            PriceDating = row["PriceDating"],
            NumberDFLinks = string.IsNullOrEmpty(row["NumberDFLinks"]) ? null : int.Parse(row["NumberDFLinks"], CultureInfo.InvariantCulture),
            Term = row["Term"],
            Niche = row["Niche"],
            Categories = row["Categories"],
            SponsoredTag = row["SponsoredTag"],
            IsQuarantined = string.Equals(GetOptionalValue(row, "IsQuarantined"), "TRUE", StringComparison.OrdinalIgnoreCase),
            QuarantineReason = GetOptionalValue(row, "QuarantineReason"),
        };

    private static string? GetOptionalValue(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private void SetLastPublishedDates()
    {
        SetLastPublishedDate("example.com", new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        SetLastPublishedDate("test.com", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetLastPublishedDate("gambling.com", null);
    }

    private void SetLastPublishedDate(string domain, DateTime? value)
    {
        var site = _context.Sites.Single(s => s.Domain == domain);
        site.LastPublishedDate = value;
        site.LastPublishedDateIsMonthOnly = false;
    }

    private sealed class ExportedSiteRow
    {
        public string Domain { get; set; } = string.Empty;
        public double DR { get; set; }
        public long Traffic { get; set; }
        public string Location { get; set; } = string.Empty;
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
        Assert.Equal(string.Empty, row["PriceUsd"]);
    }

    [Fact]
    public async Task ExportSitesAsExcelAsync_NumericPriceUsd_WritesNumericValue()
    {
        var result = await _service.ExportSitesAsExcelAsync(
            DefaultQuery(), TestUserId, TestUserEmail, AppRoles.Admin, CancellationToken.None);

        var rows = await ReadSitesSheetRowsFromStream(result.FileStream);
        var row = rows.Single(r => r["Domain"] == "example.com");
        Assert.Equal("100", row["PriceUsd"]);
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
        DR = 50, Traffic = 10000, Location = "US",
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
}
