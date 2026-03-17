using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests;

/// <summary>
/// Tests for server-side sorting by LastPublishedDate (null last, tie-breaker month-only vs exact).
/// </summary>
public class LastPublishedDateSortingTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SitesService _sitesService;
    private readonly ExportService _exportService;

    public LastPublishedDateSortingTests()
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
        _context.RoleSettings.AddRange(
            new RoleSettings { RoleName = AppRoles.Admin, ExportLimitRows = 100 },
            new RoleSettings { RoleName = AppRoles.SuperAdmin, ExportLimitRows = 100 });

        var utc = DateTimeKind.Utc;
        var sites = new List<Site>
        {
            new()
            {
                Domain = "a.com",
                DR = 1,
                Traffic = 1,
                Location = "US",
                PriceUsd = 1m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastPublishedDate = new DateTime(2026, 1, 15, 0, 0, 0, utc),
                LastPublishedDateIsMonthOnly = false
            },
            new()
            {
                Domain = "b.com",
                DR = 1,
                Traffic = 1,
                Location = "US",
                PriceUsd = 1m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastPublishedDate = new DateTime(2026, 1, 1, 0, 0, 0, utc),
                LastPublishedDateIsMonthOnly = false
            },
            new()
            {
                Domain = "c.com",
                DR = 1,
                Traffic = 1,
                Location = "US",
                PriceUsd = 1m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastPublishedDate = new DateTime(2026, 1, 1, 0, 0, 0, utc),
                LastPublishedDateIsMonthOnly = true
            },
            new()
            {
                Domain = "d.com",
                DR = 1,
                Traffic = 1,
                Location = "US",
                PriceUsd = 1m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastPublishedDate = null,
                LastPublishedDateIsMonthOnly = false
            },
            new()
            {
                Domain = "e.com",
                DR = 1,
                Traffic = 1,
                Location = "US",
                PriceUsd = 1m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastPublishedDate = new DateTime(2025, 12, 1, 0, 0, 0, utc),
                LastPublishedDateIsMonthOnly = true
            }
        };

        _context.Sites.AddRange(sites);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetSitesAsync_SortByLastPublishedDateAsc_ReturnsNullLast_MonthBeforeExactOnSameDate()
    {
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.LastPublishedDate,
            SortDir = SortingDefaults.Ascending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _sitesService.GetSitesAsync(query);

        var domains = result.Items.Select(s => s.Domain).ToList();
        Assert.Equal(5, domains.Count);
        Assert.Equal("e.com", domains[0]);
        Assert.Equal("c.com", domains[1]);
        Assert.Equal("b.com", domains[2]);
        Assert.Equal("a.com", domains[3]);
        Assert.Equal("d.com", domains[4]);
    }

    [Fact]
    public async Task GetSitesAsync_SortByLastPublishedDateDesc_ReturnsNullLast_ExactBeforeMonthOnlyOnSameDate()
    {
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 10,
            SortBy = SortFields.LastPublishedDate,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        };

        var result = await _sitesService.GetSitesAsync(query);

        var domains = result.Items.Select(s => s.Domain).ToList();
        Assert.Equal(5, domains.Count);
        Assert.Equal("a.com", domains[0]);
        Assert.Equal("b.com", domains[1]);
        Assert.Equal("c.com", domains[2]);
        Assert.Equal("e.com", domains[3]);
        Assert.Equal("d.com", domains[4]);
    }

    [Fact]
    public async Task ExportSitesAsCsvAsync_WithLastPublishedDateSort_UsesSameOrderAsList()
    {
        var query = new SitesQuery
        {
            Page = 1,
            PageSize = 100,
            SortBy = SortFields.LastPublishedDate,
            SortDir = SortingDefaults.Descending,
            Quarantine = QuarantineFilterValues.All
        };

        var stream = await _exportService.ExportSitesAsCsvAsync(
            query,
            "user-id",
            "user@example.com",
            AppRoles.Admin,
            CancellationToken.None);

        var sites = await ReadCsvFromStream(stream);
        var domains = sites.Select(s => s.Domain).ToList();
        Assert.Equal(5, domains.Count);
        Assert.Equal("a.com", domains[0]);
        Assert.Equal("b.com", domains[1]);
        Assert.Equal("c.com", domains[2]);
        Assert.Equal("e.com", domains[3]);
        Assert.Equal("d.com", domains[4]);
    }

    private static async Task<List<ExportedSiteRow>> ReadCsvFromStream(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        var list = new List<ExportedSiteRow>();
        await foreach (var record in csv.GetRecordsAsync<ExportedSiteRow>())
        {
            list.Add(record);
        }

        return list;
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
        public string? Niche { get; set; }
        public string? Categories { get; set; }
        public bool IsQuarantined { get; set; }
        public string? QuarantineReason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? LastPublishedDate { get; set; }
    }
}
