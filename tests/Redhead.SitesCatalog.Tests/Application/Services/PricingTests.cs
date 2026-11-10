using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.Pricing;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Services.Pricing;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class PricingTests
{
    [Theory]
    [InlineData("", 5, 3)]
    [InlineData(" ", 5, 3)]
    [InlineData("unknown", 2, 2)]
    [InlineData("permanent", 1, 1)]
    [InlineData("finite:2:year", 1, 1)]
    [InlineData("finite:3:year", 0, 0)]
    public async Task Search_CountsMatchingDomainsAndTermRowsBeforePaging(string termKey, int rowCount, int domainCount)
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(
            CreateSite("a.example", (PriceType.Main, 100m, PricingTerm.Unknown),
                (PriceType.Main, 200m, PricingTerm.Permanent), (PriceType.Casino, 300m, PricingTerm.Permanent)),
            CreateSite("b.example", (PriceType.Main, 150m, PricingTerm.Unknown),
                (PriceType.Main, 250m, PricingTerm.FiniteYears(2))),
            CreateSite("empty.example"));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var query = new PricingQuery { TermKey = termKey, PageSize = 1 };

        // Act
        var first = await service.SearchAsync(query, default);
        query.Page = 2;
        var second = await service.SearchAsync(query, default);

        // Assert
        Assert.Equal(rowCount, first.TotalCount);
        Assert.Equal(rowCount, second.TotalCount);
        Assert.Equal(domainCount, first.DomainCount);
        Assert.Equal(domainCount, second.DomainCount);
        Assert.Equal(Math.Min(1, rowCount), first.Items.Count);
        Assert.Equal(Math.Clamp(rowCount - 1, 0, 1), second.Items.Count);
        if (!string.IsNullOrWhiteSpace(termKey))
        {
            Assert.All(first.Items.Concat(second.Items), row => Assert.Equal(termKey, Assert.Single(row.Terms).TermKey));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_ExposesCachedMarketRefreshAvailabilityAndCooldownWithoutChangingCatalog(bool configured)
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("cached.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        var checkedAt = DateTime.UtcNow.AddDays(-1);
        var retryAfter = DateTime.UtcNow.AddMinutes(1);
        context.Sites.Add(site);
        context.LinkDetectivePriceCaches.Add(new()
        {
            SiteDomain = site.Domain, MinimumUsd = 10, AverageUsd = 20, MedianUsd = 15,
            CheckedAtUtc = checkedAt, RetryAfterUtc = retryAfter, LastError = "Provider unavailable"
        });
        await context.SaveChangesAsync();
        var service = new PricingService(context,
            Options.Create(new LinkDetectiveOptions { ApiToken = configured ? "test-token" : null }), TimeProvider.System);

        // Act
        var result = await service.GetAsync(site.Domain, default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(configured, result.Market!.CanRefresh);
        Assert.Equal(retryAfter, result.Market.RetryAfterUtc);
        Assert.Equal(checkedAt, result.Market.CheckedAtUtc);
        Assert.Equal("Provider unavailable", result.Market.LastError);
        Assert.Equal(100, Assert.Single(result.Pricing.Prices).AmountUsd);
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
    }

    [Fact]
    public async Task Search_AnyTermReturnsOneRowPerCatalogTermAndKeepsSitesWithoutPrices()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(CreateSite("empty.example"), CreateSite("multi.example",
            (PriceType.Main, 100m, PricingTerm.Unknown),
            (PriceType.Casino, 150m, PricingTerm.Unknown),
            (PriceType.Main, 200m, PricingTerm.Permanent)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.SearchAsync(new PricingQuery(), default);
        var permanent = await service.SearchAsync(new PricingQuery { TermKey = "permanent", MultipleTerms = true }, default);
        var page = await service.SearchAsync(new PricingQuery { PageSize = 1, Page = 3 }, default);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.DomainCount);
        Assert.Empty(result.Items[0].Terms);
        Assert.Empty(result.Items[0].Prices);
        Assert.Equal("unknown", Assert.Single(result.Items[1].Terms).TermKey);
        Assert.Equal("permanent", Assert.Single(result.Items[2].Terms).TermKey);
        Assert.Equal("permanent", Assert.Single(Assert.Single(page.Items).Terms).TermKey);
        var selected = Assert.Single(permanent.Items);
        Assert.Equal(1, permanent.DomainCount);
        Assert.Equal("permanent", Assert.Single(selected.Terms).TermKey);
        Assert.Equal(200, selected.Prices.Single(price => price.PriceType == PriceType.Main).CatalogPrice);
    }

    [Theory]
    [InlineData("unknown", 100, 120, -20)]
    [InlineData("permanent", 200, 50, 150)]
    public async Task Search_MultiTermMarginsUseLowestActiveOfferForExactServiceAndTerm(
        string termKey, int catalog, int offer, int margin)
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("multi.example",
            (PriceType.Main, 100m, PricingTerm.Unknown),
            (PriceType.Main, 200m, PricingTerm.Permanent));
        context.Sites.Add(site);
        AddOffer(context, site, PriceType.Main, 120, PricingTerm.Unknown);
        AddOffer(context, site, PriceType.Main, 140, PricingTerm.Unknown);
        AddOffer(context, site, PriceType.Main, 50, PricingTerm.Permanent);
        AddOffer(context, site, PriceType.Main, 1, PricingTerm.FiniteYears(1));
        AddOffer(context, site, PriceType.Casino, 2, PricingTerm.Unknown);
        AddOffer(context, site, PriceType.Main, 3, PricingTerm.Unknown, SiteWebmasterOfferStatus.Inactive);
        await context.SaveChangesAsync();

        // Act
        var result = await CreateService(context).SearchAsync(new PricingQuery
        {
            TermKey = termKey, MultipleTerms = true, MinMargin = margin, MaxMargin = margin
        }, default);

        // Assert
        var row = Assert.Single(result.Items);
        var price = row.Prices.Single(price => price.PriceType == PriceType.Main);
        Assert.Equal(termKey, Assert.Single(row.Terms).TermKey);
        Assert.Equal(catalog, price.CatalogPrice);
        Assert.Equal(offer, price.OfferPrice);
        Assert.Equal(margin, price.Margin);
    }

    [Fact]
    public async Task Search_MissingMarginsArePerTermAndNeverUseAnOfferFromAnotherTerm()
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("multi.example",
            (PriceType.Main, 100m, PricingTerm.Unknown),
            (PriceType.Main, 200m, PricingTerm.Permanent));
        context.Sites.Add(site);
        AddOffer(context, site, PriceType.Main, 50, PricingTerm.Permanent);
        await context.SaveChangesAsync();

        // Act
        var result = await CreateService(context).SearchAsync(new PricingQuery { Margin = "missing" }, default);

        // Assert
        var row = Assert.Single(result.Items);
        Assert.Equal("unknown", Assert.Single(row.Terms).TermKey);
        Assert.Null(row.Prices.Single(price => price.PriceType == PriceType.Main).Margin);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.DomainCount);
    }

    [Theory]
    [InlineData("https://WWW.MATCH.EXAMPLE/path?query=1#fragment")]
    [InlineData("  www.match.example  ")]
    [InlineData("MATCH")]
    public async Task Search_NormalizesDomainAndUrlLikeTheCatalog(string search)
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(new Site { Domain = "match.example" }, new Site { Domain = "other.example" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.SearchAsync(new PricingQuery { Domain = search }, default);
        var longUrl = await service.SearchAsync(new PricingQuery { Domain = "https://match.example/" + new string('x', 300) }, default);

        // Assert
        Assert.Equal("match.example", Assert.Single(result.Items).Domain);
        Assert.Equal("match.example", Assert.Single(longUrl.Items).Domain);
    }

    [Fact]
    public async Task Search_DefaultPageSizeIsAppliedBeforeReturningCatalogRows()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(Enumerable.Range(1, 30).Select(index => new Site { Domain = $"site-{index:D2}.example" }));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var first = await service.SearchAsync(new PricingQuery(), default);
        var second = await service.SearchAsync(new PricingQuery { Page = 2 }, default);

        // Assert
        Assert.Equal(30, first.TotalCount);
        Assert.Equal(PaginationDefaults.DefaultPageSize, first.PageSize);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        Assert.Equal("site-26.example", second.Items[0].Domain);
    }

    [Fact]
    public async Task Get_InvalidCatalogTermCannotProduceMarginAgainstNoTermOffer()
    {
        // Arrange
        await using var context = CreateContext();
        var site = new Site { Domain = "invalid-term.example" };
        site.PriceOptions.Add(new() { PriceType = PriceType.Main, TermKey = PricingTerm.UnknownKey, TermType = TermType.Permanent, AmountUsd = 100 });
        site.PriceOptions.Add(new() { PriceType = PriceType.Casino, TermKey = PricingTerm.UnknownKey, AmountUsd = 150 });
        context.Sites.Add(site);
        context.SiteWebmasterOffers.Add(new()
        {
            Id = Guid.NewGuid(),
            Site = site,
            SiteDomain = site.Domain,
            Status = SiteWebmasterOfferStatus.Active,
            Prices = [new() { Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main, AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = 50 }]
        });
        await context.SaveChangesAsync();

        // Act
        var result = await CreateService(context).GetAsync(site.Domain, default);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasInvalidTerms);
        var price = result.Prices.Single(price => price.PriceType == PriceType.Main);
        Assert.Null(price.CatalogPrice);
        Assert.Null(price.Margin);
        Assert.Equal("No term", Assert.Single(result.Terms).Label);
    }

    [Fact]
    public async Task Save_CannotChangePlacementTerms()
    {
        // Arrange
        await using var context = CreateContext();
        var site = new Site { Domain = "fixed-term.example" };
        site.PriceOptions.Add(new() { PriceType = PriceType.Main, TermKey = PricingTerm.UnknownKey, AmountUsd = 100 });
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var request = new SavePricingRequest
        {
            Version = SitePricingUpdater.Version(SitePricingUpdater.Snapshot(site)),
            Pricing = new() { Prices = [new() { PriceType = PriceType.Main, TermKey = "permanent", TermType = TermType.Permanent, AmountUsd = 100 }] }
        };

        // Act
        var exception = await Record.ExceptionAsync(() => CreateEditor(context)
            .SaveAsync(site.Domain, request, null, default));

        // Assert
        Assert.IsType<RequestValidationException>(exception);
        Assert.Equal(PricingTerm.UnknownKey, Assert.Single(site.PriceOptions).TermKey);
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
    }

    [Fact]
    public async Task Save_YesStatusRemovesOptionalPricesAcrossAllTermsAndKeepsOtherServices()
    {
        // Arrange
        await using var context = CreateContext();
        var site = new Site { Domain = "availability.example" };
        site.PriceOptions.Add(new() { PriceType = PriceType.Main, TermKey = PricingTerm.UnknownKey, AmountUsd = 100 });
        site.PriceOptions.Add(new() { PriceType = PriceType.Casino, TermKey = PricingTerm.UnknownKey, AmountUsd = 150 });
        site.PriceOptions.Add(new() { PriceType = PriceType.Casino, TermKey = PricingTerm.PermanentKey, TermType = TermType.Permanent, AmountUsd = 200 });
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var snapshot = SitePricingUpdater.Snapshot(site);
        var request = new SavePricingRequest
        {
            Version = SitePricingUpdater.Version(snapshot),
            Pricing = new()
            {
                Prices = snapshot.Prices.Where(price => price.PriceType != PriceType.Casino).ToList(),
                ServiceAvailabilities = [new() { ServiceType = PriceType.Casino, Status = ServiceAvailabilityStatus.AvailableWithUnknownPrice }]
            }
        };

        // Act
        await CreateEditor(context).SaveAsync(site.Domain, request, null, default);

        // Assert
        Assert.Equal(PriceType.Main, Assert.Single(await context.SitePriceOptions.ToListAsync()).PriceType);
        Assert.Equal(ServiceAvailabilityStatus.AvailableWithUnknownPrice, site.PriceCasinoStatus);
        Assert.Null(site.PriceCasino);
        Assert.Single(await context.EntityChangeHistories.ToListAsync());
    }

    [Fact]
    public async Task Save_ContradictoryAvailabilityIsRejectedBeforeChangingData()
    {
        // Arrange
        await using var context = CreateContext();
        var request = new SavePricingRequest
        {
            Pricing = new()
            {
                Prices = [new() { PriceType = PriceType.Casino, TermKey = PricingTerm.UnknownKey, AmountUsd = 100 }],
                ServiceAvailabilities = [new() { ServiceType = PriceType.Casino, Status = ServiceAvailabilityStatus.NotAvailable }]
            }
        };

        // Act
        var exception = await Record.ExceptionAsync(() => CreateEditor(context)
            .SaveAsync("test.example", request, null, default));

        // Assert
        Assert.IsType<RequestValidationException>(exception);
        Assert.Empty(await context.SitePriceOptions.ToListAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
    }

    [Fact]
    public async Task Get_ReusesCachedMarketDataWithFreshnessAndFailure_AndExcludesInactiveQuotes()
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("market.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        context.Sites.Add(site);
        context.SiteWebmasterOffers.Add(new()
        {
            Id = Guid.NewGuid(),
            Site = site,
            SiteDomain = site.Domain,
            Status = SiteWebmasterOfferStatus.Inactive,
            Prices = [new() { Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main,
                AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = 5 }]
        });
        var checkedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        context.LinkDetectivePriceCaches.Add(new() { SiteDomain = site.Domain, MinimumUsd = 10, AverageUsd = 20, MedianUsd = 15, CheckedAtUtc = checkedAt, LastError = "Provider unavailable" });
        await context.SaveChangesAsync();

        // Act
        var result = await CreateService(context).GetAsync("https://market.example/", default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Market!.MinimumUsd);
        Assert.True(result.Market.IsStale);
        Assert.Equal(checkedAt, result.Market.CheckedAtUtc);
        Assert.Equal("Provider unavailable", result.Market.LastError);
        Assert.Null(result.Prices.Single(price => price.PriceType == PriceType.Main).Margin);
    }

    [Fact]
    public async Task Save_ChangesOnlyPricingAndAudit_WritesManualHistory_AndRejectsStaleVersion()
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("edit.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        site.DR = 42; site.Niche = "Original niche"; site.IsQuarantined = true; site.QuarantineReason = "Keep this";
        site.LastPublishedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var original = SitePricingUpdater.Snapshot(site);
        var version = SitePricingUpdater.Version(original);
        var request = new SavePricingRequest { Version = version, Pricing = CreatePriceRequest(125) };
        context.ChangeTracker.Clear();
        var editor = CreateEditor(context);

        // Act
        await editor.SaveAsync(site.Domain, request, "admin@example.invalid", default);
        context.ChangeTracker.Clear();
        var stale = await Record.ExceptionAsync(() => editor.SaveAsync(site.Domain, request, "admin@example.invalid", default));
        var updated = await context.Sites.Include(row => row.PriceOptions).SingleAsync();

        // Assert
        Assert.IsType<PricingConflictException>(stale);
        Assert.Equal(125, Assert.Single(updated.PriceOptions).AmountUsd);
        Assert.Equal(125, updated.PriceUsd);
        Assert.Equal(42, updated.DR);
        Assert.Equal("Original niche", updated.Niche);
        Assert.True(updated.IsQuarantined);
        Assert.Equal("Keep this", updated.QuarantineReason);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), updated.LastPublishedDate);
        var history = Assert.Single(await context.EntityChangeHistories.ToListAsync());
        Assert.Equal("Manual", history.Source);
        Assert.Contains("Price", history.ChangesJson);
        Assert.DoesNotContain("Quarantine", history.ChangesJson);
    }

    [Fact]
    public async Task Save_NoOpKeepsAuditFieldsAndCreatesNoHistory()
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("noop.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var snapshot = SitePricingUpdater.Snapshot(site);
        var originalTime = await context.Sites.AsNoTracking().Select(row => row.UpdatedAtUtc).SingleAsync();
        context.ChangeTracker.Clear();

        // Act
        await CreateEditor(context).SaveAsync(site.Domain, new() { Version = SitePricingUpdater.Version(snapshot), Pricing = snapshot }, "admin@example.invalid", default);

        // Assert
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        Assert.Equal(originalTime, (await context.Sites.SingleAsync()).UpdatedAtUtc);
        Assert.Null((await context.Sites.SingleAsync()).UpdatedBy);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("0.001")]
    [InlineData("100.001")]
    [InlineData("10000000000000000")]
    public async Task Save_InvalidPriceDoesNotChangeCatalog(string text)
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("invalid.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var version = SitePricingUpdater.Version(SitePricingUpdater.Snapshot(site));
        var originalAuditTime = await context.Sites.AsNoTracking().Select(row => row.UpdatedAtUtc).SingleAsync();
        var amount = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        context.ChangeTracker.Clear();

        // Act
        var exception = await Record.ExceptionAsync(() => CreateEditor(context).SaveAsync(site.Domain, new() { Version = version, Pricing = CreatePriceRequest(amount) }, null, default));

        // Assert
        Assert.IsType<RequestValidationException>(exception);
        Assert.Equal(100, (await context.SitePriceOptions.SingleAsync()).AmountUsd);
        Assert.Equal(originalAuditTime, (await context.Sites.SingleAsync()).UpdatedAtUtc);
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
    }

    [Fact]
    public async Task Save_TwoDecimalPricePersistsExactly_AndTrailingZerosAreANoOp()
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("decimal-price.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var version = SitePricingUpdater.Version(SitePricingUpdater.Snapshot(site));
        context.ChangeTracker.Clear();
        var editor = CreateEditor(context);

        // Act
        await editor.SaveAsync(site.Domain, new() { Version = version, Pricing = CreatePriceRequest(100.12m) }, null, default);
        context.ChangeTracker.Clear();
        var updated = await context.Sites.AsNoTracking().Include(row => row.PriceOptions).Include(row => row.ServiceAvailabilities).SingleAsync();
        var savedVersion = SitePricingUpdater.Version(SitePricingUpdater.Snapshot(updated));
        await editor.SaveAsync(site.Domain, new() { Version = savedVersion, Pricing = CreatePriceRequest(100.1200m) }, null, default);
        context.ChangeTracker.Clear();

        // Assert
        var saved = await context.Sites.Include(row => row.PriceOptions).SingleAsync();
        Assert.Equal(100.12m, Assert.Single(saved.PriceOptions).AmountUsd);
        Assert.Equal(100.12m, saved.PriceUsd);
        Assert.Equal(updated.UpdatedAtUtc, saved.UpdatedAtUtc);
        Assert.Contains("100.12", Assert.Single(await context.EntityChangeHistories.ToListAsync()).ChangesJson);
    }

    private static PricingService CreateService(ApplicationDbContext context) =>
        new(context, Options.Create(new LinkDetectiveOptions()), TimeProvider.System);

    private static PricingEditor CreateEditor(ApplicationDbContext context) =>
        new(context, Mock.Of<ISitesCatalogCache>(), TimeProvider.System);

    private static UpdateSitePricingRequest CreatePriceRequest(decimal amount) => new()
    {
        Prices = [new() { PriceType = PriceType.Main, TermKey = PricingTerm.UnknownKey, AmountUsd = amount }]
    };

    private static Site CreateSite(string domain, params (PriceType Type, decimal Amount, PricingTerm Term)[] prices) => new()
    {
        Domain = domain,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
        PriceOptions = prices.Select(price => new SitePriceOption
        {
            SiteDomain = domain,
            PriceType = price.Type,
            AmountUsd = price.Amount,
            TermKey = price.Term.TermKey,
            TermType = price.Term.TermType,
            TermValue = price.Term.TermValue,
            TermUnit = price.Term.TermUnit,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }).ToList()
    };

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void AddOffer(ApplicationDbContext context, Site site, PriceType type, decimal amount, PricingTerm term,
        SiteWebmasterOfferStatus status = SiteWebmasterOfferStatus.Active) => context.SiteWebmasterOffers.Add(new()
        {
            Id = Guid.NewGuid(), Site = site, SiteDomain = site.Domain, Status = status,
            Prices = [new()
            {
                Id = Guid.NewGuid(), PriceType = CatalogPriceMapping.ToOfferType(type),
                AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = amount,
                TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit
            }]
        });
}
