using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Redhead.SitesCatalog.Application.Audit;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Data.Migrations;

namespace Redhead.SitesCatalog.Tests.Integration;

// Every test owns a disposable database; never changes the local catalog or production snapshots.
public sealed class CatalogPriceTermMigrationTests : IAsyncLifetime
{
    private const string Previous = "20261105084327_EnforceStrictCatalogPriceTerms";
    private const string Migration = "20261109082829_BackfillCatalogPriceTerms";
    private const string Actor = "Migration:" + Migration;
    private static readonly DateTime OriginalTime = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private string? _connection;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")!) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        builder.Database = "redhead_term_test_" + Guid.NewGuid().ToString("N");
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{builder.Database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _connection = builder.ConnectionString;
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(Previous);
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [PostgresFact]
    public async Task Up_MapsAllServicesAndSupportedTerms_PreservesAmountsAndUnrelatedData()
    {
        // Arrange
        await using var context = CreateContext();
        var cases = (from type in Enum.GetValues<PriceType>()
                     from term in new[] { PricingTerm.Permanent, PricingTerm.FiniteYears(2) }
                     select (Type: type, Term: term)).ToArray();
        foreach (var (type, term) in cases)
        {
            var site = AddSite(context, $"{type}-{term.TermType}.example");
            AddPrice(site, type, PricingTerm.Unknown, 100.25m);
            AddOffer(context, site, term, type);
        }
        await context.SaveChangesAsync();
        var originalIds = await context.SitePriceOptions.Select(p => p.Id).OrderBy(id => id).ToArrayAsync();
        var offerJson = JsonSerializer.Serialize(await context.WebmasterOfferPrices.AsNoTracking().OrderBy(p => p.Id).ToListAsync());

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        context.ChangeTracker.Clear();
        var sites = await context.Sites.Include(s => s.PriceOptions).OrderBy(s => s.Domain).ToListAsync();

        // Assert
        Assert.Equal(originalIds, await context.SitePriceOptions.Select(p => p.Id).OrderBy(id => id).ToArrayAsync());
        foreach (var site in sites)
        {
            var price = Assert.Single(site.PriceOptions);
            Assert.Equal(100.25m, price.AmountUsd);
            Assert.Equal(OriginalTime, price.CreatedAtUtc);
            Assert.NotEqual("unknown", price.TermKey);
            Assert.True(PricingTerm.TryCreate(price.TermKey, price.TermType, price.TermValue, price.TermUnit, out _));
            Assert.Equal(Actor, site.UpdatedBy);
            Assert.Equal(price.UpdatedAtUtc, site.UpdatedAtUtc);
            Assert.Equal("Original", site.CreatedBy);
            Assert.Equal(OriginalTime, site.LastPublishedDate);
            Assert.Null(site.TermType); // Legacy site-level term is not the pricing source.
            Assert.Equal(777m, site.PriceUsd);
        }
        Assert.Equal(cases.Length, await context.EntityChangeHistories.CountAsync());
        Assert.Equal(offerJson, JsonSerializer.Serialize(await context.WebmasterOfferPrices.AsNoTracking().OrderBy(p => p.Id).ToListAsync()));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Up_RequiresUnanimousKnownActiveTermsAndNumericMatchingService()
    {
        // Arrange
        await using var context = CreateContext();
        var domains = new[] { "consensus", "inactive-ignored", "unknown", "conflicting", "mixed-unknown", "inactive-only", "no-offers", "details-only", "wrong-service", "price-term-differs", "malformed-offer" };
        var sites = domains.ToDictionary(name => name, name => AddSite(context, name + ".example"));
        foreach (var site in sites.Values) AddPrice(site, PriceType.Main, PricingTerm.Unknown);
        AddOffer(context, sites["consensus"], PricingTerm.Permanent);
        AddOffer(context, sites["consensus"], PricingTerm.Permanent);
        AddOffer(context, sites["inactive-ignored"], PricingTerm.Permanent);
        AddOffer(context, sites["inactive-ignored"], PricingTerm.Unknown, status: SiteWebmasterOfferStatus.Inactive);
        AddOffer(context, sites["unknown"], PricingTerm.Unknown);
        AddOffer(context, sites["conflicting"], PricingTerm.Permanent);
        AddOffer(context, sites["conflicting"], PricingTerm.FiniteYears(1));
        AddOffer(context, sites["mixed-unknown"], PricingTerm.Permanent);
        AddOffer(context, sites["mixed-unknown"], PricingTerm.Unknown);
        AddOffer(context, sites["inactive-only"], PricingTerm.Permanent, status: SiteWebmasterOfferStatus.Inactive);
        var details = AddOffer(context, sites["details-only"], PricingTerm.Permanent).Prices.Single();
        details.WebmasterPriceUsd = null;
        details.AvailabilityStatus = ServiceAvailabilityStatus.Unknown;
        details.WebmasterPriceDetails = "Ask for price";
        AddOffer(context, sites["wrong-service"], PricingTerm.Permanent, PriceType.Casino);
        var wrongTerm = AddOffer(context, sites["price-term-differs"], PricingTerm.Permanent).Prices.Single();
        wrongTerm.TermType = null;
        var malformed = AddOffer(context, sites["malformed-offer"], PricingTerm.Permanent);
        malformed.TermType = null;
        malformed.TermValue = 2;
        malformed.TermUnit = TermUnit.Year;
        await context.SaveChangesAsync();

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        context.ChangeTracker.Clear();

        // Assert
        foreach (var (name, site) in sites)
        {
            var expected = name is "consensus" or "inactive-ignored" ? "permanent" : "unknown";
            Assert.Equal(expected, (await context.SitePriceOptions.SingleAsync(p => p.SiteDomain == site.Domain)).TermKey);
            Assert.Equal(expected == "permanent" ? 1 : 0, await context.EntityChangeHistories.CountAsync(h => h.EntityId == site.Domain));
            if (expected == "unknown") Assert.Equal("Original", (await context.Sites.SingleAsync(s => s.Domain == site.Domain)).UpdatedBy);
        }
    }

    [PostgresFact]
    public async Task Up_SkipsNewMultipleTerms_AndRepairsOnlyCoveredServicesOnExistingMultipleTerms()
    {
        // Arrange
        await using var context = CreateContext();
        var partial = AddSite(context, "new-multiple.example");
        AddPrice(partial, PriceType.Main, PricingTerm.Unknown);
        AddPrice(partial, PriceType.Casino, PricingTerm.Unknown);
        AddOffer(context, partial, PricingTerm.Permanent);
        var existing = AddSite(context, "existing-multiple.example");
        AddPrice(existing, PriceType.Main, PricingTerm.Permanent);
        AddPrice(existing, PriceType.Casino, PricingTerm.Unknown);
        AddPrice(existing, PriceType.Crypto, PricingTerm.Unknown);
        AddOffer(context, existing, PricingTerm.Permanent, PriceType.Casino);
        var different = AddSite(context, "other-known.example");
        AddPrice(different, PriceType.Main, PricingTerm.FiniteYears(1));
        AddPrice(different, PriceType.Casino, PricingTerm.Unknown);
        AddOffer(context, different, PricingTerm.Permanent, PriceType.Casino);
        await context.SaveChangesAsync();

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        context.ChangeTracker.Clear();

        // Assert
        Assert.All(await context.SitePriceOptions.Where(p => p.SiteDomain == partial.Domain).ToListAsync(), p => Assert.Equal("unknown", p.TermKey));
        Assert.Equal("permanent", (await context.SitePriceOptions.SingleAsync(p => p.SiteDomain == existing.Domain && p.PriceType == PriceType.Casino)).TermKey);
        Assert.Equal("unknown", (await context.SitePriceOptions.SingleAsync(p => p.SiteDomain == existing.Domain && p.PriceType == PriceType.Crypto)).TermKey);
        Assert.Equal("unknown", (await context.SitePriceOptions.SingleAsync(p => p.SiteDomain == different.Domain && p.PriceType == PriceType.Casino)).TermKey);
        Assert.Equal(existing.Domain, (await context.EntityChangeHistories.SingleAsync()).EntityId);
    }

    [PostgresFact]
    public async Task Up_MergesOnlyIdenticalDuplicates_AndSkipsEntireSiteWithAnyDifferentAmount()
    {
        // Arrange
        await using var context = CreateContext();
        var identical = AddSite(context, "identical.example");
        AddPrice(identical, PriceType.Main, PricingTerm.Unknown);
        var target = AddPrice(identical, PriceType.Main, PricingTerm.Permanent);
        AddOffer(context, identical, PricingTerm.Permanent);
        var conflict = AddSite(context, "conflict.example");
        AddPrice(conflict, PriceType.Main, PricingTerm.Unknown);
        AddPrice(conflict, PriceType.Main, PricingTerm.Permanent, 200);
        AddPrice(conflict, PriceType.Casino, PricingTerm.Unknown);
        AddPrice(conflict, PriceType.Casino, PricingTerm.Permanent); // Equal pair must also remain untouched.
        AddOffer(context, conflict, PricingTerm.Permanent);
        AddOffer(context, conflict, PricingTerm.Permanent, PriceType.Casino);
        await context.SaveChangesAsync();
        var targetId = target.Id;

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        context.ChangeTracker.Clear();

        // Assert
        var remaining = await context.SitePriceOptions.SingleAsync(p => p.SiteDomain == identical.Domain);
        Assert.Equal(targetId, remaining.Id);
        Assert.Equal(100, remaining.AmountUsd);
        Assert.Equal(OriginalTime, remaining.UpdatedAtUtc);
        Assert.Equal(4, await context.SitePriceOptions.CountAsync(p => p.SiteDomain == conflict.Domain));
        Assert.Equal(identical.Domain, (await context.EntityChangeHistories.SingleAsync()).EntityId);
    }

    [PostgresFact]
    public async Task Up_CreatesVisibleSiteHistoryWithExactBeforeAfterAndTechnicalReason_AndIsIdempotent()
    {
        // Arrange
        await using var context = CreateContext();
        var site = AddSite(context, "history.example");
        AddPrice(site, PriceType.Main, PricingTerm.Unknown);
        AddPrice(site, PriceType.Casino, PricingTerm.Unknown, 125.50m);
        AddOffer(context, site, PricingTerm.FiniteYears(1));
        AddOffer(context, site, PricingTerm.FiniteYears(1), PriceType.Casino);
        await context.SaveChangesAsync();
        var before = EntityChangeHistoryRecorder.CaptureSite(site);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        context.ChangeTracker.Clear();
        var saved = await context.Sites.Include(s => s.PriceOptions).SingleAsync();
        var after = EntityChangeHistoryRecorder.CaptureSite(saved);
        var history = Assert.Single(await EntityChangeHistoryRecorder.GetHistoryAsync(context, "Site", site.Domain, default));
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.Database.ExecuteSqlRawAsync(ReadSql());
            await transaction.CommitAsync();
        }

        // Assert
        Assert.Equal("Data migration", history.Source);
        Assert.Equal(Actor, history.ChangedBy);
        Assert.Equal("Updated", history.Action);
        Assert.Equal(saved.UpdatedAtUtc, history.ChangedAtUtc);
        Assert.Contains(history.Changes, c => c.Field == "Cleanup rule" && c.NewValue!.StartsWith("Technical correction:"));
        var changes = history.Changes.Where(c => c.Field != "Cleanup rule").ToDictionary(c => c.Field);
        var expectedFields = before.Keys.Union(after.Keys).Where(field => before.GetValueOrDefault(field) != after.GetValueOrDefault(field)).ToArray();
        Assert.Equal(expectedFields.Order(), changes.Keys.Order());
        foreach (var field in expectedFields)
        {
            Assert.Equal(before.GetValueOrDefault(field), changes[field].OldValue);
            Assert.Equal(after.GetValueOrDefault(field), changes[field].NewValue);
        }
        Assert.Single(await context.EntityChangeHistories.ToListAsync());
        Assert.Equal(saved.UpdatedAtUtc, await context.Sites.Select(s => s.UpdatedAtUtc).SingleAsync());
    }

    [PostgresFact]
    public async Task Up_FailureAfterHistoryInsert_RollsBackPricesSiteAuditAndMigrationRecord()
    {
        // Arrange
        await using var context = CreateContext();
        var site = AddSite(context, "failure.example");
        AddPrice(site, PriceType.Main, PricingTerm.Unknown);
        AddOffer(context, site, PricingTerm.Permanent);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_term_repair() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Synthetic failure'; END; $body$;
            CREATE TRIGGER reject_term_repair BEFORE UPDATE ON "Sites"
                FOR EACH ROW EXECUTE FUNCTION reject_term_repair();
            """);

        // Act
        await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Migration));
        context.ChangeTracker.Clear();

        // Assert
        Assert.Equal("unknown", (await context.SitePriceOptions.SingleAsync()).TermKey);
        Assert.Equal("Original", (await context.Sites.SingleAsync()).UpdatedBy);
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        Assert.DoesNotContain(Migration, await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task Down_RejectsChangedDataAndPreservesHistory()
    {
        // Arrange
        await using var context = CreateContext();
        var site = AddSite(context, "rollback.example");
        AddPrice(site, PriceType.Main, PricingTerm.Unknown);
        AddOffer(context, site, PricingTerm.Permanent);
        await context.SaveChangesAsync();
        await context.GetService<IMigrator>().MigrateAsync(Migration);

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Previous));

        // Assert
        Assert.Contains("cannot be rolled back automatically", exception.MessageText);
        Assert.Equal("permanent", (await context.SitePriceOptions.AsNoTracking().SingleAsync()).TermKey);
        Assert.Single(await context.EntityChangeHistories.ToListAsync());
        Assert.Contains(Migration, await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task Down_EmptyRepairCanBeRolledBackAndReapplied()
    {
        // Arrange
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(Migration);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await context.GetService<IMigrator>().MigrateAsync(Migration);

        // Assert
        Assert.Contains(Migration, await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
    }

    private ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connection).Options);

    private static Site AddSite(ApplicationDbContext context, string domain)
    {
        var site = new Site
        {
            Domain = domain.ToLowerInvariant(), Location = "US", DR = 42, Traffic = 1234,
            PriceUsd = 777, CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime,
            CreatedBy = "Original", UpdatedBy = "Original", LastPublishedDate = OriginalTime
        };
        context.Sites.Add(site);
        return site;
    }

    private static SitePriceOption AddPrice(Site site, PriceType type, PricingTerm term, decimal amount = 100)
    {
        var price = new SitePriceOption
        {
            Site = site, SiteDomain = site.Domain, PriceType = type, AmountUsd = amount,
            TermKey = term.TermKey, TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit,
            CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
        };
        site.PriceOptions.Add(price);
        return price;
    }

    private static SiteWebmasterOffer AddOffer(ApplicationDbContext context, Site site, PricingTerm term,
        PriceType type = PriceType.Main, SiteWebmasterOfferStatus status = SiteWebmasterOfferStatus.Active)
    {
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "Synthetic", NormalizedContactRawText = Guid.NewGuid().ToString("N"),
            PrimaryEmail = "synthetic@example.org", CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
        };
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), Site = site, SiteDomain = site.Domain, Webmaster = webmaster,
            ImportFingerprint = Guid.NewGuid().ToString("N"), ContactRawText = "Synthetic", Status = status,
            TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit,
            CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime,
            Prices = [new WebmasterOfferPrice
            {
                Id = Guid.NewGuid(), PriceType = CatalogPriceMapping.ToOfferType(type),
                AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = 60,
                TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit,
                CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
            }]
        };
        context.SiteWebmasterOffers.Add(offer);
        return offer;
    }

    private static string ReadSql() => string.Join("\n", new BackfillCatalogPriceTerms().UpOperations
        .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(op => op.Sql));

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")))
                Skip = "Set REDHEAD_TEST_POSTGRES to run isolated PostgreSQL tests.";
        }
    }
}
