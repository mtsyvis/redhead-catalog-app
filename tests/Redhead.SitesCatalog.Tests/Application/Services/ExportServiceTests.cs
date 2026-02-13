using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
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
            new() { RoleName = AppRoles.SuperAdmin, ExportLimitRows = 1000000 },
            new() { RoleName = AppRoles.Admin, ExportLimitRows = 50000 },
            new() { RoleName = AppRoles.Internal, ExportLimitRows = 10000 },
            new() { RoleName = AppRoles.Client, ExportLimitRows = 5000 },
            new() { RoleName = "DisabledRole", ExportLimitRows = 0 }
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
            }
        };

        _context.Sites.AddRange(sites);
        _context.SaveChanges();
    }

    #region Export Success Tests

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithValidRole_ReturnsStreamWithCsvData()
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
        var stream = await _service.ExportSitesAsCsvAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
        Assert.Equal(0, stream.Position); // Stream should be at start

        // Read CSV content
        var sites = await ReadCsvFromStream(stream);
        Assert.Equal(3, sites.Count);
    }

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithFilters_ExportsOnlyMatchingSites()
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
        var stream = await _service.ExportSitesAsCsvAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var sites = await ReadCsvFromStream(stream);
        Assert.Equal(2, sites.Count); // test.com(70) and gambling.com(90)
        Assert.All(sites, site => Assert.True(site.DR >= 60));
    }

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithQuarantineExclude_ExcludesQuarantinedSites()
    {
        // Arrange
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.Exclude
        };

        // Act
        var stream = await _service.ExportSitesAsCsvAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var sites = await ReadCsvFromStream(stream);
        Assert.Equal(2, sites.Count); // example.com and test.com
        Assert.All(sites, site => Assert.False(site.IsQuarantined));
    }

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithSorting_ReturnsSortedData()
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
        var stream = await _service.ExportSitesAsCsvAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var sites = await ReadCsvFromStream(stream);
        Assert.Equal(3, sites.Count);
        Assert.Equal(90, sites[0].DR); // gambling.com
        Assert.Equal(70, sites[1].DR); // test.com
        Assert.Equal(50, sites[2].DR); // example.com
    }

    #endregion

    #region Role Limit Tests

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithLimitSmallerThanResults_EnforcesLimit()
    {
        // Arrange - Add more sites to exceed limit
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

        // Create a role with small limit
        var roleSettings = new RoleSettings { RoleName = "LimitedRole", ExportLimitRows = 5 };
        _context.RoleSettings.Add(roleSettings);
        await _context.SaveChangesAsync();

        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 100,
            SortBy = SortFields.Domain,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        // Act
        var stream = await _service.ExportSitesAsCsvAsync(
            query,
            TestUserId,
            TestUserEmail,
            "LimitedRole",
            CancellationToken.None);

        // Assert
        var sites = await ReadCsvFromStream(stream);
        Assert.Equal(5, sites.Count); // Should be limited to 5
    }

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithDisabledRole_ThrowsExportDisabledException()
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

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ExportDisabledException>(
            () => _service.ExportSitesAsCsvAsync(
                query,
                TestUserId,
                TestUserEmail,
                "DisabledRole",
                CancellationToken.None));

        Assert.Equal(ExportConstants.ExportDisabledMessage, exception.Message);
        Assert.Equal("DisabledRole", exception.RoleName);
    }

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithNonExistentRole_ThrowsRoleSettingsNotFoundException()
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

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RoleSettingsNotFoundException>(
            () => _service.ExportSitesAsCsvAsync(
                query,
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
    public async Task ExportSitesAsCsvAsync_CreatesExportLog()
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
        await _service.ExportSitesAsCsvAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
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
    public async Task ExportSitesAsCsvAsync_LogsCorrectRowCount()
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
        await _service.ExportSitesAsCsvAsync(
            query,
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        // Assert
        var exportLog = await _context.ExportLogs.FirstOrDefaultAsync();
        Assert.NotNull(exportLog);
        Assert.Equal(2, exportLog.RowsReturned); // 2 non-quarantined sites
    }

    #endregion

    #region Helper Methods

    private static async Task<List<Site>> ReadCsvFromStream(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        var sites = new List<Site>();
        await foreach (var record in csv.GetRecordsAsync<Site>())
        {
            sites.Add(record);
        }

        return sites;
    }

    #endregion
}
