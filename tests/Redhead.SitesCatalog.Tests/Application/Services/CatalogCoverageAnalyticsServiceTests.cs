using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services.Analytics.CatalogCoverageAnalytics;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class CatalogCoverageAnalyticsServiceTests
{
    [Fact]
    public async Task Get_CountsUniqueCatalogDomainsAndAllOffers_IncludingInactiveAndQuarantined()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(
            new Site { Domain = "active.com" },
            new Site { Domain = "inactive.com" },
            new Site { Domain = "mixed.com" },
            new Site { Domain = "missing.com" },
            new Site { Domain = "quarantined-missing.com", IsQuarantined = true },
            new Site { Domain = "quarantined-covered.com", IsQuarantined = true });
        AddOffer(context, "active.com", SiteWebmasterOfferStatus.Active);
        AddOffer(context, "active.com", SiteWebmasterOfferStatus.Active);
        AddOffer(context, "inactive.com", SiteWebmasterOfferStatus.Inactive);
        AddOffer(context, "inactive.com", SiteWebmasterOfferStatus.Inactive);
        AddOffer(context, "mixed.com", SiteWebmasterOfferStatus.Active);
        AddOffer(context, "mixed.com", SiteWebmasterOfferStatus.Inactive);
        AddOffer(context, "quarantined-covered.com", SiteWebmasterOfferStatus.Active);
        await context.SaveChangesAsync();
        var service = new CatalogCoverageAnalyticsService(context);
        var before = DateTime.UtcNow;

        // Act
        var result = await service.GetAsync();

        // Assert
        Assert.Equal(6, result.CatalogSites);
        Assert.Equal(4, result.CoveredSites);
        Assert.Equal(2, result.SitesWithoutOffers);
        Assert.Equal(66.67m, result.CoveragePercent);
        Assert.Equal(7, result.TotalOffers);
        Assert.Equal(4, result.ActiveOffers);
        Assert.Equal(3, result.InactiveOffers);
        Assert.Equal(1, result.SitesWithOnlyInactiveOffers);
        Assert.Equal(2, result.QuarantinedSites);
        Assert.Equal(1, result.QuarantinedSitesWithoutOffers);
        Assert.InRange(result.GeneratedAtUtc, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, result.GeneratedAtUtc.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Get_NoOffers_ReturnsZeroCoverageWithoutDividingByZero(int catalogSites)
    {
        // Arrange
        await using var context = CreateContext();
        if (catalogSites > 0) context.Sites.Add(new Site { Domain = "uncovered.com" });
        await context.SaveChangesAsync();
        var service = new CatalogCoverageAnalyticsService(context);

        // Act
        var result = await service.GetAsync();

        // Assert
        Assert.Equal(catalogSites, result.CatalogSites);
        Assert.Equal(catalogSites, result.SitesWithoutOffers);
        Assert.Equal(0, result.CoveredSites);
        Assert.Equal(0, result.CoveragePercent);
        Assert.Equal(0, result.TotalOffers);
        Assert.Equal(0, result.SitesWithOnlyInactiveOffers);
    }

    [Fact]
    public async Task Export_ContainsOnlyUniqueUncoveredDomains_SortedAndIncludingQuarantine()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(
            new Site { Domain = "z-missing.com" },
            new Site { Domain = "a-quarantined.com", IsQuarantined = true },
            new Site { Domain = "active.com" },
            new Site { Domain = "inactive.com" },
            new Site { Domain = "www.exact-key.com" },
            new Site { Domain = "exact-key.com" });
        AddOffer(context, "active.com", SiteWebmasterOfferStatus.Active);
        AddOffer(context, "active.com", SiteWebmasterOfferStatus.Active);
        AddOffer(context, "inactive.com", SiteWebmasterOfferStatus.Inactive);
        AddOffer(context, "exact-key.com", SiteWebmasterOfferStatus.Active);
        await context.SaveChangesAsync();
        var service = new CatalogCoverageAnalyticsService(context);

        // Act
        using var stream = await service.ExportDomainsWithoutOffersAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var worksheet = ReadXml(archive, "xl/worksheets/sheet1.xml");

        // Assert
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal("Domains without offers", Assert.Single(workbook.Descendants(ns + "sheet")).Attribute("name")!.Value);
        var rows = worksheet.Descendants(ns + "row").ToArray();
        Assert.Equal(["Domain", "a-quarantined.com", "www.exact-key.com", "z-missing.com"],
            rows.Select(row => Assert.Single(row.Descendants(ns + "t")).Value).ToArray());
        Assert.All(rows, row => Assert.Single(row.Elements(ns + "c")));
        Assert.Equal("1", Assert.Single(worksheet.Descendants(ns + "pane")).Attribute("ySplit")!.Value);
        Assert.Equal("A1:A4", Assert.Single(worksheet.Descendants(ns + "autoFilter")).Attribute("ref")!.Value);
        Assert.Empty(context.ExportLogs);
        Assert.Empty(context.ExportAnalyticsSnapshots);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5_001)]
    public async Task Export_AlwaysHasHeaderAndDoesNotApplyOrdinaryExportLimit(int domainCount)
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(Enumerable.Range(0, domainCount)
            .Select(index => new Site { Domain = $"missing-{index:D5}.com" }));
        await context.SaveChangesAsync();
        var service = new CatalogCoverageAnalyticsService(context);

        // Act
        using var stream = await service.ExportDomainsWithoutOffersAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var worksheet = ReadXml(archive, "xl/worksheets/sheet1.xml");

        // Assert
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = worksheet.Descendants(ns + "row").ToArray();
        Assert.Equal(domainCount + 1, rows.Length);
        Assert.Equal("Domain", Assert.Single(rows[0].Descendants(ns + "t")).Value);
        if (domainCount > 0)
        {
            Assert.Equal("missing-05000.com", Assert.Single(rows[^1].Descendants(ns + "t")).Value);
        }
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void AddOffer(ApplicationDbContext context, string domain, SiteWebmasterOfferStatus status)
    {
        var id = Guid.NewGuid();
        context.SiteWebmasterOffers.Add(new SiteWebmasterOffer
        {
            Id = id, SiteDomain = domain, Status = status, ContactRawText = "Contact instructions",
            ImportFingerprint = id.ToString("N").PadRight(64, '0'),
            Webmaster = new Webmaster
            {
                Id = Guid.NewGuid(), ContactRawText = "Contact instructions",
                NormalizedContactRawText = id.ToString("N"), PrimaryEmail = "Contact instructions"
            }
        });
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        using var stream = archive.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }
}
