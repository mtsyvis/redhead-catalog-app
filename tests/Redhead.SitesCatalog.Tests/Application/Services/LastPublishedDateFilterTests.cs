using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests;

/// <summary>
/// Tests for filtering Sites by LastPublishedDate using month/year range (FromMonth / ToMonth).
/// </summary>
public class LastPublishedDateFilterTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SitesService _sitesService;
    private readonly ExportService _exportService;

    // Domains used throughout the tests
    private const string SiteNullDate = "null-date.com";
    private const string SiteJanFirst = "jan-first.com";
    private const string SiteJanMid = "jan-mid.com";
    private const string SiteJanLast = "jan-last.com";
    private const string SiteFebFirst = "feb-first.com";
    private const string SiteMar = "mar.com";

    public LastPublishedDateFilterTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var queryBuilder = new SitesQueryBuilder();
        _sitesService = new SitesService(_context, queryBuilder);
        _exportService = new ExportService(_context, queryBuilder);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        _context.RoleSettings.Add(
            new RoleSettings { RoleName = AppRoles.Admin, ExportLimitMode = ExportLimitMode.Unlimited, ExportLimitRows = null });

        var utc = DateTimeKind.Utc;
        _context.Sites.AddRange(
            MakeSite(SiteNullDate, lastPublishedDate: null),
            MakeSite(SiteJanFirst, new DateTime(2025, 1, 1, 0, 0, 0, utc)),
            MakeSite(SiteJanMid, new DateTime(2025, 1, 15, 12, 0, 0, utc)),
            MakeSite(SiteJanLast, new DateTime(2025, 1, 31, 23, 59, 59, utc)),
            MakeSite(SiteFebFirst, new DateTime(2025, 2, 1, 0, 0, 0, utc)),
            MakeSite(SiteMar, new DateTime(2025, 3, 15, 0, 0, 0, utc)));

        _context.SaveChanges();
    }

    private static Site MakeSite(string domain, DateTime? lastPublishedDate) => new()
    {
        Domain = domain,
        DR = 50,
        Traffic = 1000,
        Location = "US",
        PriceUsd = 100m,
        PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
        IsQuarantined = false,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
        LastPublishedDate = lastPublishedDate
    };

    private static SitesQuery BaseQuery(
        DateTime? lastPublishedFrom = null,
        DateTime? lastPublishedToExclusive = null) => new()
    {
        Page = 1,
        PageSize = 100,
        SortBy = SortFields.Domain,
        SortDir = SortingDefaults.Ascending,
        Quarantine = QuarantineFilterValues.All,
        LastPublishedFrom = lastPublishedFrom,
        LastPublishedToExclusive = lastPublishedToExclusive
    };

    #region List filter tests

    [Fact]
    public async Task GetSitesAsync_WithNoLastPublishedFilter_IncludesNullDates()
    {
        var result = await _sitesService.GetSitesAsync(BaseQuery(), CancellationToken.None);

        var domains = result.Items.Select(s => s.Domain).ToList();
        Assert.Contains(SiteNullDate, domains);
        Assert.Equal(6, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_WithFromMonthOnly_ExcludesNullAndBeforeRange()
    {
        // From 2025-02 → include 2025-02-01 and later, exclude null and 2025-01-*
        var from = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _sitesService.GetSitesAsync(BaseQuery(lastPublishedFrom: from), CancellationToken.None);

        var domains = result.Items.Select(s => s.Domain).ToList();
        Assert.DoesNotContain(SiteNullDate, domains);
        Assert.DoesNotContain(SiteJanFirst, domains);
        Assert.DoesNotContain(SiteJanMid, domains);
        Assert.DoesNotContain(SiteJanLast, domains);
        Assert.Contains(SiteFebFirst, domains);
        Assert.Contains(SiteMar, domains);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_WithToMonthOnly_ExcludesNullAndAfterRange()
    {
        // ToMonth 2025-01 → exclusive upper bound = 2025-02-01, include up to 2025-01-31, exclude null
        var toExclusive = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _sitesService.GetSitesAsync(BaseQuery(lastPublishedToExclusive: toExclusive), CancellationToken.None);

        var domains = result.Items.Select(s => s.Domain).ToList();
        Assert.DoesNotContain(SiteNullDate, domains);
        Assert.Contains(SiteJanFirst, domains);
        Assert.Contains(SiteJanMid, domains);
        Assert.Contains(SiteJanLast, domains);
        Assert.DoesNotContain(SiteFebFirst, domains);
        Assert.DoesNotContain(SiteMar, domains);
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_WithBothBounds_ReturnsOnlyWithinRange()
    {
        // From 2025-01, ToMonth 2025-02 → exclusive upper = 2025-03-01
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toExclusive = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _sitesService.GetSitesAsync(
            BaseQuery(lastPublishedFrom: from, lastPublishedToExclusive: toExclusive),
            CancellationToken.None);

        var domains = result.Items.Select(s => s.Domain).ToList();
        Assert.DoesNotContain(SiteNullDate, domains);
        Assert.Contains(SiteJanFirst, domains);
        Assert.Contains(SiteJanMid, domains);
        Assert.Contains(SiteJanLast, domains);
        Assert.Contains(SiteFebFirst, domains);
        Assert.DoesNotContain(SiteMar, domains);
        Assert.Equal(4, result.Total);
    }

    [Fact]
    public async Task GetSitesAsync_NullLastPublishedDate_ExcludedWhenAnyBoundSet()
    {
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _sitesService.GetSitesAsync(BaseQuery(lastPublishedFrom: from), CancellationToken.None);

        Assert.DoesNotContain(result.Items, s => s.Domain == SiteNullDate);
    }

    #endregion

    #region Boundary correctness tests

    [Fact]
    public async Task GetSitesAsync_FromMonth_IncludesFirstDayOfMonth()
    {
        // From = 2025-01-01: the site with exactly 2025-01-01 must be included
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _sitesService.GetSitesAsync(BaseQuery(lastPublishedFrom: from), CancellationToken.None);

        Assert.Contains(result.Items, s => s.Domain == SiteJanFirst);
    }

    [Fact]
    public async Task GetSitesAsync_ToMonthExclusive_IncludesLastDayOfMonth_ExcludesFirstDayOfNextMonth()
    {
        // ToMonth = 2025-01 → toExclusive = 2025-02-01
        // 2025-01-31 must be included; 2025-02-01 must be excluded
        var toExclusive = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _sitesService.GetSitesAsync(
            BaseQuery(lastPublishedToExclusive: toExclusive),
            CancellationToken.None);

        var domains = result.Items.Select(s => s.Domain).ToList();
        Assert.Contains(SiteJanLast, domains);
        Assert.DoesNotContain(SiteFebFirst, domains);
    }

    #endregion

    #region Export uses same filter logic

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithLastPublishedFilter_UsesIdenticalFilterLogic()
    {
        // ToMonth = 2025-01 → exclusive upper = 2025-02-01
        var toExclusive = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var query = BaseQuery(lastPublishedToExclusive: toExclusive);

        var exportResult = await _exportService.ExportSitesAsCsvAsync(
            query,
            "user-id",
            "user@example.com",
            AppRoles.Admin,
            CancellationToken.None);

        Assert.NotNull(exportResult);
        exportResult.CsvStream.Position = 0;
        using var reader = new System.IO.StreamReader(exportResult.CsvStream, leaveOpen: true);
        var csv = await reader.ReadToEndAsync();

        Assert.Contains(SiteJanFirst, csv);
        Assert.Contains(SiteJanMid, csv);
        Assert.Contains(SiteJanLast, csv);
        Assert.DoesNotContain(SiteFebFirst, csv);
        Assert.DoesNotContain(SiteMar, csv);
        Assert.DoesNotContain(SiteNullDate, csv);
    }

    #endregion
}
