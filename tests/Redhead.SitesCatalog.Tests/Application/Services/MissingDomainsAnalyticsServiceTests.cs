using Redhead.SitesCatalog.Application.Services.Analytics.MissingDomainsAnalytics;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class MissingDomainsAnalyticsServiceTests
{
    [Theory]
    [InlineData(AppRoles.Client, true)]
    [InlineData(AppRoles.Lite, true)]
    [InlineData(AppRoles.Admin, false)]
    [InlineData(AppRoles.SuperAdmin, false)]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Editor, false)]
    [InlineData(AppRoles.Linkbuilder, false)]
    public async Task Record_OnlyExternalRoles_RecordsUniqueValidNormalizedDomains(string role, bool recorded)
    {
        // Arrange
        await using var db = CreateContext();
        var service = new MissingDomainsAnalyticsService(db);
        var domains = MultiSearchParser.Parse("https://www.Example.com/path example.com news.example.com hello 127.0.0.1")
            .UniqueDomains.Concat(["example.com"]).ToArray();

        // Act
        await service.RecordAsync("user", role, Guid.NewGuid(), domains);

        // Assert
        Assert.Equal(recorded ? 1 : 0, await db.MultiSearchAnalyticsRequests.CountAsync());
        Assert.Equal(recorded ? 2 : 0, await db.MissingDomainSearches.CountAsync());
        if (recorded)
        {
            Assert.Equal(["example.com", "news.example.com"], await db.MissingDomainSearches.OrderBy(x => x.Domain).Select(x => x.Domain).ToArrayAsync());
            Assert.Equal(role, (await db.MultiSearchAnalyticsRequests.SingleAsync()).Role);
        }
    }

    [Fact]
    public async Task Record_PreservesExactNormalizedSearchKeys_WithoutStrippingAnotherWww()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new MissingDomainsAnalyticsService(db);
        var domains = MultiSearchParser.Parse("https://www.www.example.com/path example.com").UniqueDomains;

        // Act
        await service.RecordAsync("client", AppRoles.Client, Guid.NewGuid(), domains);

        // Assert
        Assert.Equal(["example.com", "www.example.com"], await db.MissingDomainSearches
            .OrderBy(x => x.Domain).Select(x => x.Domain).ToArrayAsync());
    }

    [Fact]
    public async Task Record_RetryDoesNotCountAgain_NewSearchAndDifferentUserDo()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new MissingDomainsAnalyticsService(db);
        var id = Guid.NewGuid();

        // Act
        await service.RecordAsync("client", AppRoles.Client, id, ["missing.com"]);
        await service.RecordAsync("client", AppRoles.Client, id, ["missing.com", "later.com"]);
        await service.RecordAsync("client", AppRoles.Client, Guid.NewGuid(), ["missing.com"]);
        await service.RecordAsync("lite", AppRoles.Lite, id, ["missing.com"]);
        var result = await service.GetAsync(new());

        // Assert
        var row = Assert.Single(result.Items);
        Assert.Equal("missing.com", row.Domain);
        Assert.Equal(3, row.Searches);
        Assert.Equal(2, row.UniqueUsers);
        Assert.Equal(3, result.Searches);
        Assert.Equal(2, result.UniqueUsers);
        Assert.Equal(3, await db.MultiSearchAnalyticsRequests.CountAsync());
    }

    [Fact]
    public async Task Record_RetryOfInitiallyAllFound_DoesNotIntroduceMissingDomains()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new MissingDomainsAnalyticsService(db);
        var id = Guid.NewGuid();

        // Act
        await service.RecordAsync("client", AppRoles.Client, id, []);
        await service.RecordAsync("client", AppRoles.Client, id, ["removed.com"]);

        // Assert
        Assert.Empty(await db.MissingDomainSearches.ToListAsync());
        Assert.Single(await db.MultiSearchAnalyticsRequests.ToListAsync());
    }

    [Fact]
    public async Task Get_FiltersByUtcPeriodAndHistoricalRole_UsesUniqueUsersAcrossDomains()
    {
        // Arrange
        await using var db = CreateContext();
        AddSearch(db, "a", AppRoles.Client, At(1), "one.com", "two.com");
        AddSearch(db, "a", AppRoles.Lite, At(2), "one.com");
        AddSearch(db, "b", AppRoles.Client, At(2), "one.com");
        AddSearch(db, "c", AppRoles.Client, At(3), "three.com");
        await db.SaveChangesAsync();
        var service = new MissingDomainsAnalyticsService(db);

        // Act
        var result = await service.GetAsync(new() { FromUtc = At(1), ToUtc = At(3), Role = AppRoles.Client });

        // Assert
        Assert.Equal(2, result.UniqueDomains);
        Assert.Equal(3, result.Searches);
        Assert.Equal(2, result.UniqueUsers);
        Assert.Equal(["one.com", "two.com"], result.Items.Select(x => x.Domain));
        Assert.Equal(At(1), result.Items[0].FirstSearchedAtUtc);
        Assert.Equal(At(2), result.Items[0].LastSearchedAtUtc);
        Assert.Equal(2, result.Items[0].UniqueUsers);
    }

    [Fact]
    public async Task Get_DomainAddedToCatalog_PreservesHistoryAndIncludesQuarantineAsPresent()
    {
        // Arrange
        await using var db = CreateContext();
        AddSearch(db, "a", AppRoles.Client, At(1), "added.com", "missing.com");
        db.Sites.Add(new Site { Domain = "added.com", IsQuarantined = true });
        await db.SaveChangesAsync();
        var service = new MissingDomainsAnalyticsService(db);

        // Act
        var added = await service.GetAsync(new() { IsInCatalog = true });
        var missing = await service.GetAsync(new() { IsInCatalog = false });
        var searched = await service.GetAsync(new() { Domain = "added" });

        // Assert
        Assert.Equal("added.com", Assert.Single(added.Items).Domain);
        Assert.True(added.Items[0].IsInCatalog);
        Assert.Equal("missing.com", Assert.Single(missing.Items).Domain);
        Assert.False(missing.Items[0].IsInCatalog);
        Assert.Equal(1, searched.Searches);
        Assert.Equal(2, await db.MissingDomainSearches.CountAsync());
    }

    [Fact]
    public async Task Get_PaginatesStableDemandOrder_WhileTotalsCoverAllMatchingRows()
    {
        // Arrange
        await using var db = CreateContext();
        var domains = Enumerable.Range(0, 12).Select(i => $"domain{i:00}.com").ToArray();
        AddSearch(db, "a", AppRoles.Client, At(1), domains);
        AddSearch(db, "b", AppRoles.Lite, At(2), "domain11.com");
        await db.SaveChangesAsync();
        var service = new MissingDomainsAnalyticsService(db);

        // Act
        var first = await service.GetAsync(new() { Page = 1, PageSize = 10 });
        var second = await service.GetAsync(new() { Page = 2, PageSize = 10 });

        // Assert
        Assert.Equal("domain11.com", first.Items[0].Domain);
        Assert.Equal(["domain09.com", "domain10.com"], second.Items.Select(x => x.Domain));
        Assert.Equal(12, second.UniqueDomains);
        Assert.Equal(13, second.Searches);
        Assert.Equal(2, second.UniqueUsers);
    }

    [Fact]
    public async Task Get_CatalogAdditionRemovesLastPage_ReturnsLastAvailablePage()
    {
        // Arrange
        await using var db = CreateContext();
        var domains = Enumerable.Range(0, 12).Select(i => $"domain{i:00}.com").ToArray();
        AddSearch(db, "a", AppRoles.Client, At(1), domains);
        await db.SaveChangesAsync();
        var service = new MissingDomainsAnalyticsService(db);
        var query = new MissingDomainsAnalyticsQuery { IsInCatalog = false, Page = 2, PageSize = 10 };
        var before = await service.GetAsync(query);

        // Act
        db.Sites.AddRange(domains.Take(2).Select(domain => new Site { Domain = domain }));
        await db.SaveChangesAsync();
        var after = await service.GetAsync(query);

        // Assert
        Assert.Equal(2, before.Page);
        Assert.Equal(2, before.Items.Count);
        Assert.Equal(1, after.Page);
        Assert.Equal(10, after.UniqueDomains);
        Assert.Equal(domains.Skip(2), after.Items.Select(row => row.Domain));
    }

    [Fact]
    public async Task Get_EmptySelection_ReturnsZeroTotals()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new MissingDomainsAnalyticsService(db);

        // Act
        var result = await service.GetAsync(new() { Page = 7 });

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.UniqueUsers);
        Assert.Equal(0, result.Searches);
        Assert.Equal(0, result.UniqueDomains);
        Assert.Equal(1, result.Page);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DateTime At(int day) => new(2026, 9, day, 0, 0, 0, DateTimeKind.Utc);

    private static void AddSearch(ApplicationDbContext db, string user, string role, DateTime date, params string[] domains)
        => db.MultiSearchAnalyticsRequests.Add(new MultiSearchAnalyticsRequest
        {
            Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), UserId = user, Role = role, SearchedAtUtc = date,
            MissingDomains = domains.Select(domain => new MissingDomainSearch { Domain = domain }).ToList()
        });
}
