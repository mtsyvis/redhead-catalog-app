using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public class LastPublishedImportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly LastPublishedImportService _service;

    public LastPublishedImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var logger = NullLogger<LastPublishedImportService>.Instance;
        _service = new LastPublishedImportService(_context, logger);

        SeedSites();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedSites()
    {
        var now = DateTime.UtcNow;
        _context.Sites.AddRange(
            new Site
            {
                Domain = "example.com",
                DR = 10,
                Traffic = 1000,
                Location = "US",
                PriceUsd = 10,
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
            new Site
            {
                Domain = "month-only.com",
                DR = 20,
                Traffic = 2000,
                Location = "US",
                PriceUsd = 20,
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });

        _context.SaveChanges();
    }

    private static Stream Csv(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData("31.01.2026")]          // dd.MM.yyyy
    [InlineData("2026-01-31")]          // yyyy-MM-dd
    [InlineData("31/01/2026")]          // dd/MM/yyyy
    [InlineData("01/31/2026")]          // MM/dd/yyyy
    [InlineData("31-01-2026")]          // dd-MM-yyyy
    public async Task ImportAsync_SupportsConfiguredDayFormats(string date)
    {
        var csv = $"Domain,LastPublishedDate\nexample.com,{date}\n";
        await using var stream = Csv(csv);

        var result = await _service.ImportAsync(stream, "lastpub.csv", "text/csv", "u1", "user@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Equal(new DateTime(2026, 1, 31), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);
    }

    [Theory]
    [InlineData("January 2026")]  // MMMM yyyy
    [InlineData("Jan 2026")]      // MMM yyyy
    [InlineData("01.2026")]       // MM.yyyy
    [InlineData("1.2026")]        // M.yyyy
    [InlineData("2026-01")]       // yyyy-MM
    [InlineData("2026.01")]       // yyyy.MM
    [InlineData("01/2026")]       // MM/yyyy
    [InlineData("1/2026")]        // M/yyyy
    public async Task ImportAsync_SupportsConfiguredMonthFormats(string monthValue)
    {
        var csv = $"Domain,LastPublishedDate\nmonth-only.com,{monthValue}\n";
        await using var stream = Csv(csv);

        var result = await _service.ImportAsync(stream, "lastpub.csv", "text/csv", "u1", "user@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await _context.Sites.FirstAsync(s => s.Domain == "month-only.com");
        Assert.Equal(new DateTime(2026, 1, 1), site.LastPublishedDate);
        Assert.True(site.LastPublishedDateIsMonthOnly);
    }

    [Fact]
    public async Task ImportAsync_FullDate_SetsDayPrecision()
    {
        var csv = "Domain,LastPublishedDate\nexample.com,31.01.2026\n";
        await using var stream = Csv(csv);

        var result = await _service.ImportAsync(stream, "lastpub.csv", "text/csv", "u1", "user@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Equal(new DateTime(2026, 1, 31), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);
    }

    [Fact]
    public async Task ImportAsync_MonthYear_SetsMonthOnlyPrecision()
    {
        var csv = "Domain,LastPublishedDate\nmonth-only.com,January 2026\n";
        await using var stream = Csv(csv);

        var result = await _service.ImportAsync(stream, "lastpub.csv", "text/csv", "u1", "user@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await _context.Sites.FirstAsync(s => s.Domain == "month-only.com");
        Assert.Equal(new DateTime(2026, 1, 1), site.LastPublishedDate);
        Assert.True(site.LastPublishedDateIsMonthOnly);
    }

    [Fact]
    public async Task ImportAsync_EmptyDate_ClearsDateWithoutError()
    {
        // Pre-set a date so we can verify it is cleared.
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        site.LastPublishedDate = new DateTime(2025, 12, 31);
        site.LastPublishedDateIsMonthOnly = false;
        await _context.SaveChangesAsync();

        var csv = "Domain,LastPublishedDate\nexample.com,\n";
        await using var stream = Csv(csv);

        var result = await _service.ImportAsync(stream, "lastpub.csv", "text/csv", "u1", "user@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);

        site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Null(site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomains_CountsDuplicatesAndKeepsLast()
    {
        var csv = "Domain,LastPublishedDate\nexample.com,31.01.2026\nexample.com,01.02.2026\n";
        await using var stream = Csv(csv);

        var result = await _service.ImportAsync(stream, "lastpub.csv", "text/csv", "u1", "user@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);

        var log = await _context.ImportLogs.FirstOrDefaultAsync(l => l.Type == ImportConstants.ImportTypeLastPublished);
        Assert.NotNull(log);
        Assert.Equal(1, log!.Duplicates);

        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.Equal(new DateTime(2026, 2, 1), site.LastPublishedDate);
    }
}

