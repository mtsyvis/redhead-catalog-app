using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.Pricing;
using Redhead.SitesCatalog.Application.Models.TableViews;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Services.Pricing;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Integration;

// Synthetic data in a separate database. Never writes to the configured catalog database.
public sealed class PricingPostgresTests : IAsyncLifetime
{
    private string? _connection;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")!) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var database = "redhead_pricing_test_" + Guid.NewGuid().ToString("N");
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
        await command.ExecuteNonQueryAsync();
        builder.Database = database;
        _connection = builder.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [PostgresTheory]
    [InlineData(PriceType.Main)]
    [InlineData(PriceType.Casino)]
    [InlineData(PriceType.Crypto)]
    [InlineData(PriceType.Dating)]
    [InlineData(PriceType.LinkInsertion)]
    [InlineData(PriceType.LinkInsertionCasino)]
    public async Task Search_MarginUsesLowestActiveExactTermOfferForEachService(PriceType type)
    {
        // Arrange
        await using var context = CreateContext();
        var term = PricingTerm.FiniteYears(2);
        var site = Site("match.example", (type, 100m, term));
        context.Sites.Add(site);
        AddOffer(context, site, type, 70, term);
        var best = AddOffer(context, site, type, 60, term);
        AddOffer(context, site, type, 1, PricingTerm.Permanent);
        AddOffer(context, site, type, 2, PricingTerm.Unknown);
        AddOffer(context, site, type, 3, term, SiteWebmasterOfferStatus.Inactive);
        await context.SaveChangesAsync();

        // Act
        var result = await CreateService(context).SearchAsync(new() { PriceType = type, SortBy = "margin", MinMargin = 40, MaxMargin = 40 }, default);

        // Assert
        var row = Assert.Single(result.Items);
        var price = Assert.Single(row.Prices.Where(price => price.PriceType == type));
        Assert.Equal(40, price.Margin);
        Assert.Equal(60, price.OfferPrice);
        Assert.Equal(best.Id, price.OfferId);
        Assert.Equal(1, result.TotalCount);
    }

    [PostgresFact]
    public async Task Search_IncludesQuarantinedEmptyAndMultipleTermSites_AndFiltersBeforePaging()
    {
        // Arrange
        await using var context = CreateContext();
        var empty = Site("empty.example");
        empty.IsQuarantined = true;
        context.Sites.AddRange(empty, Site("multi.example", (PriceType.Main, 100m, PricingTerm.Permanent), (PriceType.Main, 50m, PricingTerm.FiniteYears(3))),
            Site("single.example", (PriceType.Main, 80m, PricingTerm.Unknown)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var all = await service.SearchAsync(new() { PageSize = 1 }, default);
        var multipleTerms = await service.SearchAsync(new() { MultipleTerms = true, TermKey = "finite:3:year", Margin = "missing", SortBy = "margin" }, default);
        var quarantine = await service.SearchAsync(new() { IsQuarantined = true }, default);

        // Assert
        Assert.Equal(4, all.TotalCount);
        Assert.Equal(3, all.DomainCount);
        Assert.Equal("empty.example", Assert.Single(all.Items).Domain);
        var multi = Assert.Single(multipleTerms.Items);
        Assert.Equal("multi.example", multi.Domain);
        Assert.Equal("finite:3:year", Assert.Single(multi.Terms).TermKey);
        Assert.Empty(Assert.Single(quarantine.Items).Terms);
        Assert.Null(Assert.Single(quarantine.Items).Market!.CheckedAtUtc);
        Assert.False(Assert.Single(quarantine.Items).Market!.CanRefresh);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Search_MultipleTermsKeepExactMarginsAndOtherServiceSortBeforePaging(bool descending)
    {
        // Arrange
        await using var context = CreateContext();
        var site = Site("multi.example",
            (PriceType.Main, 100m, PricingTerm.Unknown),
            (PriceType.Main, 80m, PricingTerm.Permanent),
            (PriceType.Main, 90m, PricingTerm.FiniteYears(2)),
            (PriceType.Casino, 300m, PricingTerm.Unknown),
            (PriceType.Casino, 110m, PricingTerm.Permanent));
        context.Sites.Add(site);
        AddOffer(context, site, PriceType.Main, 120, PricingTerm.Unknown);
        AddOffer(context, site, PriceType.Main, 100, PricingTerm.Permanent);
        AddOffer(context, site, PriceType.Main, 100, PricingTerm.FiniteYears(2));
        AddOffer(context, site, PriceType.Casino, 100, PricingTerm.Unknown);
        AddOffer(context, site, PriceType.Casino, 100, PricingTerm.Permanent);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var query = new PricingQuery
        {
            MultipleTerms = true, Margin = "nonPositive", SortBy = "margin",
            SortPriceType = PriceType.Casino, Descending = descending, PageSize = 1
        };

        // Act
        var first = await service.SearchAsync(query, default);
        query.Page = 2;
        var second = await service.SearchAsync(query, default);
        query.Page = 3;
        var third = await service.SearchAsync(query, default);
        var exact = await service.SearchAsync(new() { TermKey = "permanent", MinMargin = -20, MaxMargin = -20 }, default);

        // Assert
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(1, first.DomainCount);
        Assert.Equal(descending ? "unknown" : "permanent", Assert.Single(Assert.Single(first.Items).Terms).TermKey);
        Assert.Equal(descending ? "permanent" : "unknown", Assert.Single(Assert.Single(second.Items).Terms).TermKey);
        Assert.Equal("finite:2:year", Assert.Single(Assert.Single(third.Items).Terms).TermKey);
        Assert.Equal(-10, Assert.Single(third.Items).Prices.Single(price => price.PriceType == PriceType.Main).Margin);
        var exactRow = Assert.Single(exact.Items);
        Assert.Equal(1, exact.TotalCount);
        Assert.Equal(exact.TotalCount, exact.DomainCount);
        Assert.Equal("permanent", Assert.Single(exactRow.Terms).TermKey);
        Assert.Equal(80, exactRow.Prices.Single(price => price.PriceType == PriceType.Main).CatalogPrice);
        Assert.Equal(100, exactRow.Prices.Single(price => price.PriceType == PriceType.Main).OfferPrice);
        Assert.All(exactRow.Prices, price => Assert.Equal("permanent", price.TermKey));
    }

    [PostgresFact]
    public async Task CatalogPrices_RejectDuplicateServiceWithinSameDomainAndTerm()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(Site("duplicate.example",
            (PriceType.Main, 100m, PricingTerm.Unknown),
            (PriceType.Main, 200m, PricingTerm.Unknown)));

        // Act
        var exception = await Record.ExceptionAsync(() => context.SaveChangesAsync());

        // Assert
        var updateException = Assert.IsType<DbUpdateException>(exception);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(updateException.InnerException).SqlState);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Search_MarginsSortWithUnknownLast_AndNonPositiveFilterIncludesZero(bool descending)
    {
        // Arrange
        await using var context = CreateContext();
        var negative = Site("negative.example", (PriceType.Main, 90m, PricingTerm.Unknown));
        var zero = Site("zero.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        var positive = Site("positive.example", (PriceType.Main, 110m, PricingTerm.Unknown));
        var unknown = Site("unknown.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        context.Sites.AddRange(negative, zero, positive, unknown);
        AddOffer(context, negative, PriceType.Main, 100, PricingTerm.Unknown);
        AddOffer(context, zero, PriceType.Main, 100, PricingTerm.Unknown);
        AddOffer(context, positive, PriceType.Main, 100, PricingTerm.Unknown);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.SearchAsync(new() { SortBy = "margin", Descending = descending }, default);
        var firstPage = await service.SearchAsync(new() { Margin = "nonPositive", PageSize = 1 }, default);
        var secondPage = await service.SearchAsync(new() { Margin = "nonPositive", PageSize = 1, Page = 2 }, default);
        var range = await service.SearchAsync(new() { Margin = "nonPositive", MinMargin = -5 }, default);

        // Assert
        Assert.Equal(descending ? ["positive.example", "zero.example", "negative.example", "unknown.example"] :
            new[] { "negative.example", "zero.example", "positive.example", "unknown.example" }, result.Items.Select(site => site.Domain));
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal("negative.example", Assert.Single(firstPage.Items).Domain);
        Assert.Equal("zero.example", Assert.Single(secondPage.Items).Domain);
        Assert.Equal("zero.example", Assert.Single(range.Items).Domain);
        Assert.Equal(0, Assert.Single(Assert.Single(range.Items).Prices.Where(price => price.PriceType == PriceType.Main)).Margin);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Search_SortingByAnotherServicePreservesMarginFilterAndPaging(bool descending)
    {
        // Arrange
        await using var context = CreateContext();
        var highCasino = Site("a.example", (PriceType.Main, 90m, PricingTerm.Unknown), (PriceType.Casino, 200m, PricingTerm.Unknown));
        var lowCasino = Site("b.example", (PriceType.Main, 80m, PricingTerm.Unknown), (PriceType.Casino, 120m, PricingTerm.Unknown));
        var missingCasino = Site("c.example", (PriceType.Main, 70m, PricingTerm.Unknown));
        var excluded = Site("excluded.example", (PriceType.Main, 110m, PricingTerm.Unknown), (PriceType.Casino, 101m, PricingTerm.Unknown));
        context.Sites.AddRange(highCasino, lowCasino, missingCasino, excluded);
        foreach (var site in new[] { highCasino, lowCasino, missingCasino, excluded })
            AddOffer(context, site, PriceType.Main, 100, PricingTerm.Unknown);
        foreach (var site in new[] { highCasino, lowCasino, excluded })
            AddOffer(context, site, PriceType.Casino, 100, PricingTerm.Unknown);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var query = new PricingQuery
        {
            PriceType = PriceType.Main, Margin = "nonPositive", SortBy = "margin",
            SortPriceType = PriceType.Casino, Descending = descending
        };

        // Act
        var all = await service.SearchAsync(query, default);
        query.PageSize = 1;
        query.Page = 2;
        var secondPage = await service.SearchAsync(query, default);

        // Assert
        Assert.Equal(descending ? ["a.example", "b.example", "c.example"] :
            new[] { "b.example", "a.example", "c.example" }, all.Items.Select(site => site.Domain));
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal(descending ? "b.example" : "a.example", Assert.Single(secondPage.Items).Domain);
    }

    [PostgresFact]
    public async Task Save_RoundTripsTwoDecimalPricesThroughPostgres()
    {
        // Arrange
        await using var context = CreateContext();
        var site = Site("decimal-price.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var version = SitePricingUpdater.Version(SitePricingUpdater.Snapshot(site));
        context.ChangeTracker.Clear();

        // Act
        await CreateEditor(context).SaveAsync(site.Domain,
            new() { Version = version, Pricing = PriceRequest(100.12m) }, null, default);
        context.ChangeTracker.Clear();
        var price = await context.SitePriceOptions.AsNoTracking().SingleAsync();
        var legacyPrice = await context.Sites.AsNoTracking().Select(row => row.PriceUsd).SingleAsync();

        // Assert
        Assert.Equal(100.12m, price.AmountUsd);
        Assert.Equal(100.12m, legacyPrice);
    }

    [PostgresFact]
    public async Task Save_SimultaneousEditsAllowOneWriterAndReportConflictForTheOther()
    {
        // Arrange
        await using var seed = CreateContext();
        var site = Site("race.example", (PriceType.Main, 100m, PricingTerm.Unknown));
        seed.Sites.Add(site);
        await seed.SaveChangesAsync();
        var version = SitePricingUpdater.Version(SitePricingUpdater.Snapshot(site));
        var barrier = new SaveBarrier();
        await using var first = CreateContext(barrier);
        await using var second = CreateContext(barrier);

        // Act
        var attempts = await Task.WhenAll(
            Record.ExceptionAsync(() => CreateEditor(first).SaveAsync(site.Domain, new() { Version = version, Pricing = PriceRequest(110) }, null, default)),
            Record.ExceptionAsync(() => CreateEditor(second).SaveAsync(site.Domain, new() { Version = version, Pricing = PriceRequest(120) }, null, default)));

        // Assert
        Assert.Single(attempts, exception => exception is null);
        Assert.Single(attempts, exception => exception is PricingConflictException);
        seed.ChangeTracker.Clear();
        Assert.Contains((await seed.SitePriceOptions.SingleAsync()).AmountUsd, new[] { 110m, 120m });
        Assert.Single(await seed.EntityChangeHistories.ToListAsync());
    }

    [PostgresFact]
    public async Task Migration_AllowsPricingPreferencesAndRollbackPreservesSitesViewsAndCatalogData()
    {
        // Arrange
        await using var context = CreateContext();
        context.Users.Add(new ApplicationUser { Id = "pricing-admin", UserName = "pricing-admin" });
        context.Sites.Add(Site("preserve.example", (PriceType.Main, 100m, PricingTerm.Unknown)));
        context.LinkDetectivePriceCaches.Add(new() { SiteDomain = "preserve.example", MinimumUsd = 10 });
        await context.SaveChangesAsync();
        var views = new UserTableViewsService(context);
        var settings = new TableViewSettingsDto { SchemaVersion = 1, VisibleColumnIds = ["domain", "price0"], Density = "standard" };
        var sitesView = await views.CreateCustomViewAsync("pricing-admin", "sites", "Existing Sites view", settings, default);
        await views.SetActiveViewAsync("pricing-admin", "sites", "custom", sitesView.Id.ToString(), default);

        // Act
        var pricingView = await views.CreateCustomViewAsync("pricing-admin", "pricing", "Pricing", settings, default);
        await views.SetActiveViewAsync("pricing-admin", "pricing", "custom", pricingView.Id.ToString(), default);
        var savedPricing = await views.GetTableViewsAsync("pricing-admin", "pricing", default);
        await context.GetService<IMigrator>().MigrateAsync("20261103080717_AddLinkDetectiveBackgroundSync");
        context.ChangeTracker.Clear();

        // Assert
        Assert.Equal(pricingView.Id.ToString(), savedPricing.ActiveViewKey);
        Assert.Equal(sitesView.Id, (await context.UserTableCustomViews.SingleAsync()).Id);
        Assert.Equal("sites", (await context.UserTablePreferences.SingleAsync()).TableKey);
        Assert.Equal(100, (await context.SitePriceOptions.SingleAsync()).AmountUsd);
        Assert.Equal(10, (await context.LinkDetectivePriceCaches.SingleAsync()).MinimumUsd);
    }

    [PostgresTheory]
    [InlineData("permanent", null, null, null)]
    [InlineData("finite:2:year", null, 2, TermUnit.Year)]
    [InlineData("finite:2:year", TermType.Finite, 2, null)]
    [InlineData("finite:2:year", null, 2, null)]
    [InlineData("unknown", TermType.Permanent, null, null)]
    [InlineData("finite:3:year", TermType.Finite, 2, TermUnit.Year)]
    public async Task PriceConstraint_RejectsMalformedTerms(
        string termKey, TermType? termType, int? termValue, TermUnit? termUnit)
    {
        // Arrange
        await using var context = CreateContext();
        var site = Site("invalid-term.example");
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        context.SitePriceOptions.Add(new()
        {
            Site = site, SiteDomain = site.Domain, PriceType = PriceType.Main, AmountUsd = 100,
            TermKey = termKey, TermType = termType, TermValue = termValue, TermUnit = termUnit
        });

        // Act
        var exception = await Record.ExceptionAsync(() => context.SaveChangesAsync());

        // Assert
        var updateError = Assert.IsType<DbUpdateException>(exception);
        var databaseError = Assert.IsType<PostgresException>(updateError.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, databaseError.SqlState);
        Assert.Equal("CK_SitePriceOptions_Term_Consistency", databaseError.ConstraintName);
    }

    [PostgresTheory]
    [InlineData("unknown", null, null, null)]
    [InlineData("permanent", TermType.Permanent, null, null)]
    [InlineData("finite:2:year", TermType.Finite, 2, TermUnit.Year)]
    public async Task PriceConstraint_AcceptsSupportedTerms(
        string termKey, TermType? termType, int? termValue, TermUnit? termUnit)
    {
        // Arrange
        await using var context = CreateContext();
        var term = new PricingTerm(termKey, termType, termValue, termUnit);
        var site = Site("valid-term.example", (PriceType.Main, 100m, term));
        context.Sites.Add(site);

        // Act
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var terms = await CreateService(context).GetTermsAsync(default);

        // Assert
        Assert.Equal(termKey, Assert.Single(terms).TermKey);
        Assert.Equal(100, (await context.SitePriceOptions.AsNoTracking().SingleAsync()).AmountUsd);
    }

    [PostgresTheory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task PriceConstraint_RejectsNonPositiveAmounts(int amount)
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(Site("invalid-amount.example", (PriceType.Main, amount, PricingTerm.Unknown)));

        // Act
        var exception = await Record.ExceptionAsync(() => context.SaveChangesAsync());

        // Assert
        var updateError = Assert.IsType<DbUpdateException>(exception);
        var databaseError = Assert.IsType<PostgresException>(updateError.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, databaseError.SqlState);
        Assert.Equal("CK_SitePriceOptions_AmountUsd_Positive", databaseError.ConstraintName);
    }

    [PostgresFact]
    public async Task Migration_StrictTermsPreservePricesOnRollbackAndReapply()
    {
        // Arrange
        await using var context = CreateContext();
        var site = Site("preserve-terms.example",
            (PriceType.Main, 100m, PricingTerm.Unknown),
            (PriceType.Main, 200m, PricingTerm.Permanent),
            (PriceType.Main, 300m, PricingTerm.FiniteYears(2)));
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var version = SitePricingUpdater.Version(SitePricingUpdater.Snapshot(site));
        var migrator = context.GetService<IMigrator>();

        // Act
        await migrator.MigrateAsync("20261104075352_AddPricingTableViews");
        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();
        var saved = await context.Sites.Include(row => row.PriceOptions)
            .Include(row => row.ServiceAvailabilities).SingleAsync();

        // Assert
        Assert.Equal(version, SitePricingUpdater.Version(SitePricingUpdater.Snapshot(saved)));
        Assert.Equal(new[] { 100m, 200m, 300m }, saved.PriceOptions.Select(price => price.AmountUsd).OrderBy(amount => amount));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Migration_ExistingMalformedTermBlocksUpgradeWithoutDeletingPrices()
    {
        // Arrange
        await using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261104075352_AddPricingTableViews");
        var invalidTerm = new PricingTerm("permanent", null, null, null);
        context.Sites.Add(Site("blocked-upgrade.example",
            (PriceType.Main, 100m, PricingTerm.Unknown), (PriceType.Main, 200m, invalidTerm)));
        await context.SaveChangesAsync();

        // Act
        var exception = await Record.ExceptionAsync(() => migrator.MigrateAsync());

        // Assert
        var databaseError = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.CheckViolation, databaseError.SqlState);
        Assert.Equal("CK_SitePriceOptions_Term_Consistency", databaseError.ConstraintName);
        Assert.Equal(2, await context.SitePriceOptions.CountAsync());
        Assert.Equal(300, await context.SitePriceOptions.SumAsync(price => price.AmountUsd));
        Assert.DoesNotContain("20261105084327_EnforceStrictCatalogPriceTerms", await context.Database.GetAppliedMigrationsAsync());
    }

    private ApplicationDbContext CreateContext(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(_connection).AddInterceptors(interceptors).Options);
    private static PricingService CreateService(ApplicationDbContext context) => new(context, Options.Create(new LinkDetectiveOptions()), TimeProvider.System);
    private static PricingEditor CreateEditor(ApplicationDbContext context) => new(context, Mock.Of<ISitesCatalogCache>(), TimeProvider.System);
    private static UpdateSitePricingRequest PriceRequest(decimal amount) => new()
    {
        Prices = [new() { PriceType = PriceType.Main, TermKey = PricingTerm.UnknownKey, AmountUsd = amount }]
    };
    private static Site Site(string domain, params (PriceType Type, decimal Amount, PricingTerm Term)[] prices) => new()
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
    private static SiteWebmasterOffer AddOffer(ApplicationDbContext context, Site site, PriceType type, decimal amount, PricingTerm term,
        SiteWebmasterOfferStatus status = SiteWebmasterOfferStatus.Active)
    {
        var id = Guid.NewGuid();
        var offer = new SiteWebmasterOffer
        {
            Id = id,
            Site = site,
            SiteDomain = site.Domain,
            Webmaster = new Webmaster { Id = Guid.NewGuid(), PrimaryEmail = $"{id:N}@example.invalid", NormalizedContactRawText = id.ToString("N") },
            ImportFingerprint = id.ToString("N"),
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Prices = [new() { Id = Guid.NewGuid(), PriceType = CatalogPriceMapping.ToOfferType(type), WebmasterPriceUsd = amount,
                AvailabilityStatus = ServiceAvailabilityStatus.Available, TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }]
        };
        context.SiteWebmasterOffers.Add(offer);
        return offer;
    }
    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }
    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES"))) Skip = "Set REDHEAD_TEST_POSTGRES to run isolated PostgreSQL tests."; }
    }
    private sealed class PostgresTheoryAttribute : TheoryAttribute
    {
        public PostgresTheoryAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES"))) Skip = "Set REDHEAD_TEST_POSTGRES to run isolated PostgreSQL tests."; }
    }
}
