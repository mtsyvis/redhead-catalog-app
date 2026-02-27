using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests;

public class SitesImportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SitesImportService _service;

    public SitesImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var parser = new CsvSitesParser();
        var logger = NullLogger<SitesImportService>.Instance;

        _service = new SitesImportService(_context, new[] { parser }, logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private static Stream CsvStream(string csv)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(csv));
    }

    private static string BuildHeader()
    {
        return "Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,Niche,Categories";
    }

    [Fact]
    public async Task ImportAsync_ParsesDrWithComma_AsDecimal()
    {
        var csv =
            BuildHeader() + Environment.NewLine +
            @"example.com,""0,9"",1000,US,10,,,,Niche,Cat" + Environment.NewLine;

        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "sites.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.ErrorsCount);

        var site = await _context.Sites.FirstOrDefaultAsync(s => s.Domain == "example.com");
        Assert.NotNull(site);
        Assert.Equal(0.9, site!.DR);
    }

    [Fact]
    public async Task ImportAsync_ParsesDrWithDot_AsDecimal()
    {
        var csv =
            BuildHeader() + Environment.NewLine +
            @"example.com,0.9,1000,US,10,,,,Niche,Cat" + Environment.NewLine;

        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "sites.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.ErrorsCount);

        var site = await _context.Sites.FirstOrDefaultAsync(s => s.Domain == "example.com");
        Assert.NotNull(site);
        Assert.Equal(0.9, site!.DR);
    }

    [Fact]
    public async Task ImportAsync_InvalidDr_DoesNotCrashAndAddsError()
    {
        var csv =
            BuildHeader() + Environment.NewLine +
            @"example.com,abc,1000,US,10,,,,Niche,Cat" + Environment.NewLine +
            @"second.com,10,2000,UK,20,,,,Niche2,Cat2" + Environment.NewLine;

        await using var stream = CsvStream(csv);

        var result = await _service.ImportAsync(stream, "sites.csv", "text/csv", "user-1", "admin@test.com");

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);

        var error = result.Errors[0];
        Assert.Equal(2, error.RowNumber);
        Assert.Equal("example.com", error.Domain);
        Assert.Equal("DR", error.Field);
        Assert.Equal("abc", error.RawValue);
        Assert.Contains("Invalid numeric format for DR", error.Message, StringComparison.OrdinalIgnoreCase);

        var validSite = await _context.Sites.FirstOrDefaultAsync(s => s.Domain == "second.com");
        Assert.NotNull(validSite);
        Assert.Equal(10, validSite!.DR);
    }
}

