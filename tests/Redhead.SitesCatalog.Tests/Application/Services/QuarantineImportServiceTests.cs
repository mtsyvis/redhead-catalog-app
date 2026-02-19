using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests;

public class QuarantineImportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly QuarantineImportService _service;

    public QuarantineImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var logger = NullLogger<QuarantineImportService>.Instance;
        _service = new QuarantineImportService(_context, logger);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        _context.Sites.AddRange(
            new Site
            {
                Domain = "example.com",
                DR = 50,
                Traffic = 10000,
                Location = "US",
                PriceUsd = 100m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Site
            {
                Domain = "test.com",
                DR = 70,
                Traffic = 50000,
                Location = "UK",
                PriceUsd = 200m,
                IsQuarantined = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        _context.SaveChanges();
    }

    private static Stream CsvStream(string csv)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(csv));
    }

    [Fact]
    public async Task ImportAsync_ValidCsv_MatchesSitesAndUpdatesQuarantine()
    {
        var csv = "Domain,Reason\nexample.com,Policy review\ntest.com,\n";
        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "q.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(2, result.Matched);
        Assert.Empty(result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);

        var example = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.True(example.IsQuarantined);
        Assert.Equal("Policy review", example.QuarantineReason);
        Assert.NotNull(example.QuarantineUpdatedAtUtc);

        var test = await _context.Sites.FirstAsync(s => s.Domain == "test.com");
        Assert.True(test.IsQuarantined);
        Assert.Null(test.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedDomains_AddedToUnmatchedList()
    {
        var csv = "Domain,Reason\nexample.com,OK\nnotfound.org,Why\n";
        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "q.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Single(result.Unmatched);
        Assert.Contains("notfound.org", result.Unmatched);
        Assert.Equal(0, result.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_NormalizesDomain_MatchesByNormalizedValue()
    {
        var csv = "Domain,Reason\nhttps://www.Example.COM/,Normalized\n";
        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "q.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);
        var site = await _context.Sites.FirstAsync(s => s.Domain == "example.com");
        Assert.True(site.IsQuarantined);
    }

    [Fact]
    public async Task ImportAsync_EmptyDomain_AddsErrorAndSkipsRow()
    {
        var csv = "Domain,Reason\n,Some reason\ntest.com,OK\n";
        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "q.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(1, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Contains("Domain is required", result.Errors[0].Message);
    }

    [Fact]
    public async Task ImportAsync_NonCsvFile_ReturnsUnsupportedResult()
    {
        await using var stream = CsvStream("Domain,Reason\nx.com,Y\n");

        var result = await _service.ImportAsync(stream, "file.xlsx", "application/vnd.ms-excel", "user-1", "admin@test.com");

        Assert.Equal(0, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal("Unsupported file type. Use CSV.", result.Errors[0].Message);
    }

    [Fact]
    public async Task ImportAsync_CsvExtension_Accepted()
    {
        var csv = "Domain,Reason\nexample.com,OK\n";
        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "q.CSV", null, "user-1", "admin@test.com");

        Assert.Equal(1, result.Matched);
    }

    [Fact]
    public async Task ImportAsync_MissingDomainHeader_AddsErrorAndReturnsNoMatches()
    {
        var csv = "Url,Reason\nexample.com,OK\n";
        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "q.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(0, result.Matched);
        Assert.True(result.ErrorsCount > 0);
        Assert.True(
            result.Errors.Any(e => e.Message.Contains("Domain", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("header", StringComparison.OrdinalIgnoreCase)),
            "Expected an error about Domain or header. Errors: " + string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task ImportAsync_WritesImportLog()
    {
        var csv = "Domain,Reason\nexample.com,OK\n";
        await using var stream = CsvStream(csv);

        await _service.ImportAsync(stream, "q.csv", "text/csv", "user-1", "admin@test.com");

        var log = await _context.ImportLogs.FirstOrDefaultAsync(l => l.Type == ImportConstants.ImportTypeQuarantine);
        Assert.NotNull(log);
        Assert.Equal("user-1", log.UserId);
        Assert.Equal("admin@test.com", log.UserEmail);
        Assert.Equal(1, log.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
    }
}
