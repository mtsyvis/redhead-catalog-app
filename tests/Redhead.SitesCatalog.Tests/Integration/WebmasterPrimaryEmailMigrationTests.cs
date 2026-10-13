using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Redhead.SitesCatalog.Application.Audit;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Integration;

// Each opt-in test owns a fresh database; no restored or development database is modified.
public sealed class WebmasterPrimaryEmailMigrationTests : IAsyncLifetime
{
    private const string ConnectionVariable = "REDHEAD_TEST_POSTGRES";
    private const string PreviousMigration = "20261007112728_UsePostgresXminForWebmasterOfferConcurrency";
    private const string RepairMigration = "20261012153852_NormalizeWebmasterPrimaryEmails";
    private const string Actor = "Migration:" + RepairMigration;
    private readonly string _databaseName = "redhead_email_repair_test_" + Guid.NewGuid().ToString("N");
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
        await command.ExecuteNonQueryAsync();
        builder.Database = _databaseName;
        _connectionString = builder.ConnectionString;
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_connectionString)) return;
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [PostgresFact]
    public async Task Up_RepairsOnlyUnambiguousLegacyValues_AndAuditsEveryAffectedOffer()
    {
        // Arrange
        var cases = new[]
        {
            new RepairCase("repeat@example.org\nREPEAT@example.org", null, "repeat@example.org"),
            new RepairCase("first1@example.org\nsecond1@example.org (ОТВЕЧАЮТ)", null, "second1@example.org"),
            new RepairCase("first2@example.org second2@example.org=ответ здесь", "first2@example.org", "second2@example.org", Sibling: true),
            new RepairCase("first3@example.org\nосновной контакт: second3@example.org", null, "second3@example.org"),
            new RepairCase("first4@example.org - ответ\nsecond4@example.org - ответ", "first4@example.org", "first4@example.org"),
            new RepairCase("first5@example.org reply: second5@example.org", "first5@example.org", "first5@example.org"),
            new RepairCase("first6@example.org\nsecond6@example.org", null, null),
            new RepairCase("first7@example.org second7@example.org (reply)", "chosen@example.org", "chosen@example.org"),
            new RepairCase("first8@example.org second8@example.org (reply)", "first8@example.org", "first8@example.org", ManualField: "Primary email"),
            new RepairCase("first9@example.org second9@example.org (reply)", "first9@example.org", "first9@example.org", ManualField: "Contact"),
            new RepairCase("cleared@example.org", null, null),
            new RepairCase("https://example.org/contact/", null, null),
            new RepairCase("", null, null),
            new RepairCase("first10@example.org second10@example.org (reply)\nНЕ ПИШЕМ", "first10@example.org", "first10@example.org"),
            new RepairCase("first11@example.org reply@another.example.org", "first11@example.org", "first11@example.org"),
            new RepairCase("first12@example.org; second12@example.org (писать сюда)", "first12@example.org", "second12@example.org"),
            new RepairCase("first13@example.org second13@example.org (reply)", "first13@example.org", "first13@example.org", ManualField: "malformed")
        };
        await using var setup = CreateContext();
        var offers = new List<SiteWebmasterOffer>();
        foreach (var item in cases) offers.Add(await SeedOfferAsync(setup, item));
        var preserved = await OfferSnapshotAsync(setup);
        var versions = offers.ToDictionary(offer => offer.WebmasterId, offer => offer.Webmaster.Version);
        var expectedChanged = offers.Where((offer, index) => cases[index].OldEmail != cases[index].ExpectedEmail).ToArray();

        // Act
        await setup.Database.MigrateAsync();

        // Assert
        await using var verification = CreateContext();
        Assert.Equal(preserved, await OfferSnapshotAsync(verification));
        var histories = await verification.EntityChangeHistories.Where(history => history.Source == EntityChangeHistoryConstants.DataMigrationSource).ToListAsync();
        Assert.Equal(expectedChanged.Length, histories.Count(history => history.EntityType == EntityChangeHistoryConstants.WebmasterEntityType));
        Assert.Equal(expectedChanged.Sum(offer => offer.Webmaster.Offers.Count), histories.Count(history => history.EntityType == EntityChangeHistoryConstants.WebmasterOfferEntityType));
        for (var index = 0; index < offers.Count; index++)
        {
            var before = offers[index];
            var saved = await verification.Webmasters.SingleAsync(webmaster => webmaster.Id == before.WebmasterId);
            Assert.Equal(cases[index].ExpectedEmail, saved.PrimaryEmail);
            Assert.Equal(cases[index].Contact, saved.ContactRawText);
            if (cases[index].ExpectedEmail == cases[index].OldEmail)
            {
                Assert.Equal(versions[saved.Id], saved.Version);
                Assert.DoesNotContain(histories, history => history.EntityType == "Webmaster" && history.EntityId == saved.Id.ToString());
                continue;
            }
            Assert.NotEqual(versions[saved.Id], saved.Version);
            var history = Assert.Single(histories, history => history.EntityType == "Webmaster" && history.EntityId == saved.Id.ToString());
            Assert.Equal(Actor, history.ChangedBy);
            Assert.Equal(saved.UpdatedAtUtc, history.ChangedAtUtc);
            using var changes = JsonDocument.Parse(history.ChangesJson);
            Assert.Equal("Primary email", changes.RootElement[0].GetProperty("field").GetString());
            Assert.Equal(cases[index].OldEmail, changes.RootElement[0].GetProperty("oldValue").GetString());
            Assert.Equal(cases[index].ExpectedEmail, changes.RootElement[0].GetProperty("newValue").GetString());
            var visibleHistory = Assert.Single(await EntityChangeHistoryRecorder.GetHistoryAsync(verification, "SiteWebmasterOffer", before.Id.ToString(), CancellationToken.None), row => row.Source == "Data migration");
            Assert.Contains(visibleHistory.Changes, change => change.Field == "Cleanup rule" && !string.IsNullOrWhiteSpace(change.NewValue));
        }
    }

    [PostgresFact]
    public async Task Up_ReexecutedSql_DoesNotDuplicateRepairsOrHistory()
    {
        // Arrange
        await using var context = CreateContext();
        await SeedOfferAsync(context, new RepairCase("repeat@example.org\nrepeat@example.org", null, "repeat@example.org"));
        await context.Database.MigrateAsync();
        var historyCount = await context.EntityChangeHistories.CountAsync();
        var version = await context.Webmasters.Select(webmaster => webmaster.Version).SingleAsync();

        // Act
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = ReadUpSql();
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        // Assert
        Assert.Equal(historyCount, await context.EntityChangeHistories.CountAsync());
        Assert.Equal(version, await context.Webmasters.Select(webmaster => webmaster.Version).SingleAsync());
    }

    [PostgresFact]
    public async Task Up_DatabaseFailure_RollsBackEmailsAuditAndMigrationHistory()
    {
        // Arrange
        await using var context = CreateContext();
        await SeedOfferAsync(context, new RepairCase("repeat@example.org\nrepeat@example.org", null, "repeat@example.org"));
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_cleanup_update() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Injected cleanup failure'; END; $body$;
            CREATE TRIGGER reject_cleanup BEFORE UPDATE ON "Webmasters" FOR EACH ROW EXECUTE FUNCTION reject_cleanup_update();
            """);

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());

        // Assert
        Assert.Equal("P0001", exception.SqlState);
        Assert.Null(await context.Webmasters.Select(webmaster => webmaster.PrimaryEmail).SingleAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        Assert.DoesNotContain(RepairMigration, await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task Down_UnchangedRows_RestoresEmailKeepsAuditAndAllowsReapplication()
    {
        // Arrange
        await using var context = CreateContext();
        await SeedOfferAsync(context, new RepairCase("repeat@example.org\nrepeat@example.org", null, "repeat@example.org"));
        await context.Database.MigrateAsync();

        // Act
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var rolledBackEmail = await context.Webmasters.Select(webmaster => webmaster.PrimaryEmail).SingleAsync();
        var rollbackHistoryCount = await context.EntityChangeHistories.CountAsync();
        var rollbackEntries = await context.EntityChangeHistories.CountAsync(history => history.ChangedBy == "Rollback:" + RepairMigration);
        await context.Database.MigrateAsync();

        // Assert
        Assert.Null(rolledBackEmail);
        Assert.Equal(4, rollbackHistoryCount);
        Assert.Equal(2, rollbackEntries);
        Assert.Equal("repeat@example.org", await context.Webmasters.Select(webmaster => webmaster.PrimaryEmail).SingleAsync());
        Assert.Equal(6, await context.EntityChangeHistories.CountAsync());
    }

    [PostgresFact]
    public async Task Down_SubsequentManualEdit_StopsWithoutOverwritingOrRemovingAudit()
    {
        // Arrange
        await using var context = CreateContext();
        var offer = await SeedOfferAsync(context, new RepairCase("repeat@example.org\nrepeat@example.org", null, "repeat@example.org"));
        await context.Database.MigrateAsync();
        await using (var editor = CreateContext())
        {
            var webmaster = await editor.Webmasters.SingleAsync();
            webmaster.PrimaryEmail = "manual@example.org";
            webmaster.UpdatedAtUtc = DateTime.UtcNow;
            editor.EntityChangeHistories.Add(ManualHistory(offer.Id, "Primary email"));
            await editor.SaveChangesAsync();
        }
        var historyCount = await context.EntityChangeHistories.CountAsync();

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));

        // Assert
        Assert.Contains("subsequent edits", exception.MessageText, StringComparison.Ordinal);
        Assert.Equal("manual@example.org", await context.Webmasters.Select(webmaster => webmaster.PrimaryEmail).SingleAsync());
        Assert.Equal(historyCount, await context.EntityChangeHistories.CountAsync());
        Assert.Contains(RepairMigration, await context.Database.GetAppliedMigrationsAsync());
    }

    private ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connectionString).Options);

    private static async Task<SiteWebmasterOffer> SeedOfferAsync(ApplicationDbContext context, RepairCase item)
    {
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), ContactRawText = item.Contact, ImportFingerprint = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            Site = new Site { Domain = Guid.NewGuid().ToString("N") + ".example", Location = "US", CreatedAtUtc = now, UpdatedAtUtc = now },
            Webmaster = new Webmaster
            {
                Id = Guid.NewGuid(), ContactRawText = item.Contact, NormalizedContactRawText = Regex.Replace(item.Contact.Trim(), @"\s+", " ").ToLowerInvariant(),
                PrimaryEmail = item.OldEmail, CreatedAtUtc = now, UpdatedAtUtc = now
            },
            CreatedAtUtc = now, UpdatedAtUtc = now, CommentText = "Must remain unchanged"
        };
        offer.SiteDomain = offer.Site.Domain;
        offer.Prices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main, AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = 123m, WebmasterPriceDetails = "Preserved price", CreatedAtUtc = now, UpdatedAtUtc = now
        });
        context.SiteWebmasterOffers.Add(offer);
        if (item.Sibling)
        {
            context.SiteWebmasterOffers.Add(new SiteWebmasterOffer
            {
                Id = Guid.NewGuid(), Site = offer.Site, SiteDomain = offer.SiteDomain, Webmaster = offer.Webmaster,
                ContactRawText = item.Contact, ImportFingerprint = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                CreatedAtUtc = now, UpdatedAtUtc = now
            });
        }
        if (item.ManualField is not null) context.EntityChangeHistories.Add(ManualHistory(offer.Id, item.ManualField));
        await context.SaveChangesAsync();
        return offer;
    }

    private static EntityChangeHistory ManualHistory(Guid offerId, string field) => new()
    {
        Id = Guid.NewGuid(), EntityType = "SiteWebmasterOffer", EntityId = offerId.ToString(), Action = "Updated", Source = "Manual",
        ChangedBy = "editor@example.org", ChangedAtUtc = DateTime.UtcNow,
        ChangesJson = field == "malformed" ? "{}" : JsonSerializer.Serialize(new[] { new { field, oldValue = "before", newValue = "after" } })
    };

    private static async Task<string> OfferSnapshotAsync(ApplicationDbContext context)
        => JsonSerializer.Serialize(await context.SiteWebmasterOffers.OrderBy(offer => offer.Id).Select(offer => new
        {
            offer.Id, offer.WebmasterId, offer.SiteDomain, offer.ContactRawText, offer.ImportFingerprint, offer.CommentText,
            offer.CreatedAtUtc, offer.UpdatedAtUtc, offer.UpdatedBy, offer.Version,
            Prices = offer.Prices.OrderBy(price => price.Id).Select(price => new { price.Id, price.PriceType, price.WebmasterPriceUsd, price.WebmasterPriceDetails })
        }).ToListAsync());

    private static string ReadUpSql()
    {
        using var stream = typeof(ApplicationDbContext).Assembly.GetManifestResourceStream("Redhead.SitesCatalog.Infrastructure.Data.Migrations.Sql.NormalizeWebmasterPrimaryEmails.Up.sql")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record RepairCase(string Contact, string? OldEmail, string? ExpectedEmail, string? ManualField = null, bool Sibling = false);

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
        }
    }
}
