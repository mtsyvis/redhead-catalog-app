using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Exports;

public sealed class EmergencySitesExportServiceTests
{
    [Fact]
    public async Task GenerateAsync_ExportsAllSitesWithoutUserExportLimits()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.Sites.AddRange(
            new Site
            {
                Domain = "second.example",
                DR = 20,
                Traffic = 200,
                Location = "US",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Site
            {
                Domain = "first.example",
                DR = 10,
                Traffic = 100,
                Location = "US",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sut = new EmergencySitesExportService(db, new SitesExcelExportGenerator());

        // Act
        var result = await sut.GenerateAsync(CancellationToken.None);

        // Assert
        Assert.StartsWith("redhead-sites-full-", result.FileName, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", result.FileName, StringComparison.Ordinal);
        Assert.Equal(ExportConstants.ExcelContentType, result.ContentType);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(result.FileStream.Length, result.FileSizeBytes);
        Assert.True(result.FileSizeBytes > 0);

        var rows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        var infoRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Export info")
            .ToDictionary(row => row["Property"], row => row["Value"]);
        Assert.Equal(2, rows.Count);
        Assert.Equal("first.example", rows[0]["Domain"]);
        Assert.Equal("second.example", rows[1]["Domain"]);
        Assert.Equal(
            "All term-specific prices are included in each price column.",
            infoRows["Term pricing"]);
        Assert.Empty(db.ExportLogs);
    }

    [Fact]
    public async Task GenerateAsync_PriceColumnsWriteAllTermAwarePrices()
    {
        // Arrange
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var site = new Site
        {
            Domain = "prices.example",
            DR = 30,
            Traffic = 300,
            Location = "US",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Sites.Add(site);
        db.SitePriceOptions.AddRange(
            CreatePriceOption(site, PriceType.Main, PricingTerm.Permanent, 300m),
            CreatePriceOption(site, PriceType.Main, PricingTerm.FiniteYears(2), 150m),
            CreatePriceOption(site, PriceType.Main, PricingTerm.Unknown, 100m),
            CreatePriceOption(site, PriceType.Casino, PricingTerm.FiniteYears(1), 400m),
            CreatePriceOption(site, PriceType.Casino, PricingTerm.Permanent, 250m),
            CreatePriceOption(site, PriceType.LinkInsertion, PricingTerm.Unknown, 80m),
            CreatePriceOption(site, PriceType.LinkInsertionCasino, PricingTerm.FiniteYears(3), 120m),
            CreatePriceOption(site, PriceType.Dating, PricingTerm.Permanent, 500m));
        await db.SaveChangesAsync();

        var sut = new EmergencySitesExportService(db, new SitesExcelExportGenerator());

        // Act
        var result = await sut.GenerateAsync(CancellationToken.None);

        // Assert
        var row = Assert.Single(XlsxTestWorkbook.ReadRows(result.FileStream, "Sites"));
        var infoRows = XlsxTestWorkbook.ReadRows(result.FileStream, "Export info")
            .ToDictionary(infoRow => infoRow["Property"], infoRow => infoRow["Value"]);
        Assert.Equal("prices.example", row["Domain"]);
        Assert.Equal("No term: 100; 2 years: 150; Permanent: 300", row["Price USD"]);
        Assert.Equal("1 year: 400; Permanent: 250", row["Casino"]);
        Assert.Equal(string.Empty, row["Crypto"]);
        Assert.Equal("No term: 80", row["Link Insert"]);
        Assert.Equal("3 years: 120", row["Link Insert Casino"]);
        Assert.Equal("Permanent: 500", row["Dating"]);
        Assert.DoesNotContain("999", row["Price USD"], StringComparison.Ordinal);
        Assert.DoesNotContain("888", row["Casino"], StringComparison.Ordinal);
        Assert.DoesNotContain("777", row["Crypto"], StringComparison.Ordinal);
        Assert.Equal(
            "All term-specific prices are included in each price column.",
            infoRows["Term pricing"]);
        Assert.Empty(db.ExportLogs);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static SitePriceOption CreatePriceOption(
        Site site,
        PriceType priceType,
        PricingTerm term,
        decimal amountUsd)
        => new()
        {
            SiteDomain = site.Domain,
            PriceType = priceType,
            TermKey = term.TermKey,
            TermType = term.TermType,
            TermValue = term.TermValue,
            TermUnit = term.TermUnit,
            AmountUsd = amountUsd,
            CreatedAtUtc = site.CreatedAtUtc,
            UpdatedAtUtc = site.UpdatedAtUtc
        };
}
