using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Redhead.SitesCatalog.Application.Audit;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Data.Migrations;

namespace Redhead.SitesCatalog.Tests.Integration;

// Each test creates and drops its own database containing synthetic records only.
public sealed class QuarantinedOffersMigrationTests : IAsyncLifetime
{
    private const string Previous = "20261109082829_BackfillCatalogPriceTerms";
    private const string Migration = "20261111051519_DeactivateQuarantinedWebmasterOffers";
    private const string Actor = "Migration:" + Migration;
    private static readonly DateTime OriginalTime = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private string? _connection;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")!) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        builder.Database = "redhead_quarantine_test_" + Guid.NewGuid().ToString("N");
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
    public async Task Up_DeactivatesEveryActiveOfferForQuarantinedSites_AndPreservesOtherData()
    {
        // Arrange
        await using var context = CreateContext();
        var unavailable = AddSite(context, "unavailable.example", quarantined: true);
        var available = AddSite(context, "unavailable.example.net", quarantined: false);
        AddSite(context, "empty-quarantine.example", quarantined: true);
        var first = AddOffer(context, unavailable);
        var second = AddOffer(context, unavailable);
        var inactive = AddOffer(context, unavailable, SiteWebmasterOfferStatus.Inactive);
        var activeElsewhere = AddOffer(context, available);
        var inactiveElsewhere = AddOffer(context, available, SiteWebmasterOfferStatus.Inactive);
        context.EntityChangeHistories.Add(new EntityChangeHistory
        {
            Id = Guid.NewGuid(), EntityType = "Site", EntityId = unavailable.Domain, Action = "Updated",
            Source = "Manual", ChangedBy = "original@example.com", ChangedAtUtc = OriginalTime, ChangesJson = "[]"
        });
        await context.SaveChangesAsync();
        var offerData = await SnapshotAsync("SiteWebmasterOffers", "Status", "UpdatedAtUtc", "UpdatedBy");
        var tables = new[] { "Sites", "SitePriceOptions", "Webmasters", "WebmasterOfferPrices",
            "SiteWebmasterOfferLinkbuilderMailboxes", "LinkbuilderMailboxes" };
        var before = new Dictionary<string, string>();
        foreach (var table in tables) before[table] = await SnapshotAsync(table);
        var originalVersions = await context.SiteWebmasterOffers.ToDictionaryAsync(offer => offer.Id, offer => offer.Version);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        context.ChangeTracker.Clear();

        // Assert
        var saved = await context.SiteWebmasterOffers.ToDictionaryAsync(offer => offer.Id);
        Assert.Equal(5, saved.Count);
        foreach (var target in new[] { first, second })
        {
            var offer = saved[target.Id];
            Assert.Equal(SiteWebmasterOfferStatus.Inactive, offer.Status);
            Assert.Equal(Actor, offer.UpdatedBy);
            Assert.True(offer.UpdatedAtUtc > OriginalTime);
            Assert.NotEqual(originalVersions[target.Id], offer.Version);
            var history = Assert.Single(await EntityChangeHistoryRecorder.GetHistoryAsync(
                context, "SiteWebmasterOffer", offer.Id.ToString(), default));
            Assert.Equal("Data migration", history.Source);
            Assert.Equal("Updated", history.Action);
            Assert.Equal(Actor, history.ChangedBy);
            Assert.Equal(offer.UpdatedAtUtc, history.ChangedAtUtc);
            var status = Assert.Single(history.Changes, change => change.Field == "Status");
            Assert.Equal("Active", status.OldValue);
            Assert.Equal("Inactive", status.NewValue);
            var reason = Assert.Single(history.Changes, change => change.Field == "Cleanup rule");
            Assert.StartsWith("Technical correction:", reason.NewValue);
        }
        Assert.Equal(saved[first.Id].UpdatedAtUtc, saved[second.Id].UpdatedAtUtc);
        foreach (var untouched in new[] { inactive, activeElsewhere, inactiveElsewhere })
        {
            var offer = saved[untouched.Id];
            Assert.Equal(untouched.Status, offer.Status);
            Assert.Equal(OriginalTime, offer.UpdatedAtUtc);
            Assert.Equal("original@example.com", offer.UpdatedBy);
            Assert.Equal(originalVersions[untouched.Id], offer.Version);
        }
        Assert.Equal(offerData, await SnapshotAsync("SiteWebmasterOffers", "Status", "UpdatedAtUtc", "UpdatedBy"));
        foreach (var table in tables) Assert.Equal(before[table], await SnapshotAsync(table));
        Assert.Equal(3, await context.EntityChangeHistories.CountAsync());
        Assert.Single(await context.EntityChangeHistories.Where(history => history.Source == "Manual").ToListAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Up_RepeatedMigrationAndSql_PreserveStatusAuditAndHistory()
    {
        // Arrange
        await using var context = CreateContext();
        var offer = AddOffer(context, AddSite(context, "replay.example", quarantined: true));
        await context.SaveChangesAsync();
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        var offers = await SnapshotAsync("SiteWebmasterOffers");
        var history = await SnapshotAsync("EntityChangeHistories");
        var version = await context.SiteWebmasterOffers.AsNoTracking().Select(item => item.Version).SingleAsync();
        var sql = string.Join("\n", new DeactivateQuarantinedWebmasterOffers().UpOperations
            .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(operation => operation.Sql));

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.Database.ExecuteSqlRawAsync(sql);
            await transaction.CommitAsync();
        }

        // Assert
        Assert.Equal(offers, await SnapshotAsync("SiteWebmasterOffers"));
        Assert.Equal(history, await SnapshotAsync("EntityChangeHistories"));
        Assert.Equal(version, await context.SiteWebmasterOffers.AsNoTracking().Where(item => item.Id == offer.Id)
            .Select(item => item.Version).SingleAsync());
        Assert.Single(await context.EntityChangeHistories.ToListAsync());
    }

    [PostgresFact]
    public async Task Up_HistoryInsertFailure_RollsBackOffersAuditAndMigrationRecord()
    {
        // Arrange
        await using var context = CreateContext();
        AddOffer(context, AddSite(context, "failure.example", quarantined: true));
        await context.SaveChangesAsync();
        var before = await SnapshotAsync("SiteWebmasterOffers");
        var version = await context.SiteWebmasterOffers.Select(offer => offer.Version).SingleAsync();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_quarantine_history() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Synthetic history failure'; END; $body$;
            CREATE TRIGGER reject_quarantine_history BEFORE INSERT ON "EntityChangeHistories"
                FOR EACH ROW EXECUTE FUNCTION reject_quarantine_history();
            """);

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Migration));

        // Assert
        Assert.Equal("Synthetic history failure", exception.MessageText);
        Assert.Equal(before, await SnapshotAsync("SiteWebmasterOffers"));
        Assert.Equal(version, await context.SiteWebmasterOffers.AsNoTracking().Select(offer => offer.Version).SingleAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        Assert.DoesNotContain(Migration, await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task Down_NonEmptyRepair_IsRejectedWithoutChangingDataOrMigrationRecord()
    {
        // Arrange
        await using var context = CreateContext();
        AddOffer(context, AddSite(context, "rollback.example", quarantined: true));
        await context.SaveChangesAsync();
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        var offers = await SnapshotAsync("SiteWebmasterOffers");
        var history = await SnapshotAsync("EntityChangeHistories");

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Previous));

        // Assert
        Assert.Contains("cannot be rolled back automatically", exception.MessageText);
        Assert.Equal(offers, await SnapshotAsync("SiteWebmasterOffers"));
        Assert.Equal(history, await SnapshotAsync("EntityChangeHistories"));
        Assert.Contains(Migration, await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task Down_EmptyRepair_CanBeRolledBackAndReappliedWithoutChangingData()
    {
        // Arrange
        await using var context = CreateContext();
        AddOffer(context, AddSite(context, "inactive.example", quarantined: true), SiteWebmasterOfferStatus.Inactive);
        AddOffer(context, AddSite(context, "available.example", quarantined: false));
        AddSite(context, "empty.example", quarantined: true);
        await context.SaveChangesAsync();
        var before = await SnapshotAsync("SiteWebmasterOffers");
        await context.GetService<IMigrator>().MigrateAsync(Migration);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        var appliedAfterRollback = await context.Database.GetAppliedMigrationsAsync();
        await context.GetService<IMigrator>().MigrateAsync(Migration);

        // Assert
        Assert.DoesNotContain(Migration, appliedAfterRollback);
        Assert.Contains(Migration, await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(before, await SnapshotAsync("SiteWebmasterOffers"));
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
    }

    private ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connection).Options);

    private static Site AddSite(ApplicationDbContext context, string domain, bool quarantined)
    {
        var site = new Site
        {
            Domain = domain, Location = "US", DR = 42, Traffic = 1234, IsQuarantined = quarantined,
            QuarantineReason = quarantined ? "Policy review" : null,
            QuarantineUpdatedAtUtc = quarantined ? OriginalTime : null,
            CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime, UpdatedBy = "original@example.com",
            LastPublishedDate = OriginalTime,
            PriceOptions = [new SitePriceOption
            {
                PriceType = PriceType.Main, TermKey = "unknown", AmountUsd = 150.25m,
                CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
            }]
        };
        context.Sites.Add(site);
        return site;
    }

    private static SiteWebmasterOffer AddOffer(ApplicationDbContext context, Site site,
        SiteWebmasterOfferStatus status = SiteWebmasterOfferStatus.Active)
    {
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), Site = site, ContactRawText = "Synthetic contact", Status = status,
            ImportFingerprint = Guid.NewGuid().ToString("N"), CommentText = "Preserve this note",
            TermType = TermType.Permanent, TermRawText = "permanent", UpdatedBy = "original@example.com",
            CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime,
            Webmaster = new Webmaster
            {
                Id = Guid.NewGuid(), ContactRawText = "Synthetic contact", NormalizedContactRawText = Guid.NewGuid().ToString("N"),
                PrimaryEmail = "synthetic@example.com", CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
            },
            Prices = [new WebmasterOfferPrice
            {
                Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main, WebmasterPriceUsd = 50.25m,
                AvailabilityStatus = ServiceAvailabilityStatus.Available, CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
            }],
            LinkbuilderMailboxes = [new SiteWebmasterOfferLinkbuilderMailbox
            {
                CreatedAtUtc = OriginalTime,
                LinkbuilderMailbox = new LinkbuilderMailbox
                {
                    Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@example.com", DisplayName = "Synthetic mailbox",
                    CreatedAtUtc = OriginalTime, UpdatedAtUtc = OriginalTime
                }
            }]
        };
        context.SiteWebmasterOffers.Add(offer);
        return offer;
    }

    private async Task<string> SnapshotAsync(string table, params string[] excludedColumns)
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync();
        // Table names come only from the fixed test call sites; values are parameterized.
        await using var command = new NpgsqlCommand($"""
            SELECT COALESCE(jsonb_agg(to_jsonb(t) - @excluded ORDER BY (to_jsonb(t) - @excluded)::text), '[]'::jsonb)::text
            FROM "{table}" t
            """, connection);
        command.Parameters.AddWithValue("excluded", excludedColumns);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")))
                Skip = "Set REDHEAD_TEST_POSTGRES to run isolated PostgreSQL tests.";
        }
    }
}
