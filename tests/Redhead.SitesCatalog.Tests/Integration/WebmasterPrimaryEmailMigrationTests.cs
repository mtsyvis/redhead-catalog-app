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
    private const string MissingPrimaryMigration = "20261014102750_PopulateWebmasterPrimaryContacts";
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
        await setup.GetService<IMigrator>().MigrateAsync(RepairMigration);

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
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);
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
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(RepairMigration));

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
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var rolledBackEmail = await context.Webmasters.Select(webmaster => webmaster.PrimaryEmail).SingleAsync();
        var rollbackHistoryCount = await context.EntityChangeHistories.CountAsync();
        var rollbackEntries = await context.EntityChangeHistories.CountAsync(history => history.ChangedBy == "Rollback:" + RepairMigration);
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);

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
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);
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

    [PostgresFact]
    public async Task Followup_AllMissingContacts_AreFilledAndAuditIsIdempotent()
    {
        // Arrange
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);
        var cases = new[]
        {
            new RepairCase("first@example.org\nsecond@example.org - answ", null, "second@example.org", Sibling: true),
            new RepairCase("first@example.org\nsecond@example.org - пишет отсюда", null, "second@example.org"),
            new RepairCase("first@example.org\nsecond@example.org\n(ответ)", null, "second@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - новая почта", null, "second@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - otvet", null, "second@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - answear", null, "second@example.org"),
            new RepairCase("first7@example.org\nsecond7@example.org - answ", "first7@example.org", "first7@example.org"),
            new RepairCase("first8@example.org\nsecond8@example.org - answ", null, "second8@example.org", ManualField: "Primary email"),
            new RepairCase("first9@example.org\nsecond9@example.org - answ", null, "second9@example.org", ManualField: "Contact"),
            new RepairCase("first10@example.org\nsecond10@example.org - answ", null, "second10@example.org", ManualField: "malformed"),
            new RepairCase("first@example.org - answ\nsecond@example.org - ans", null, "first@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - более актуальная цена", null, "second@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - второй дешевле", null, "second@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - новый", null, "second@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - пишет отсюда - больше с ним не работаем", null, "first@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - answ\nНЕ ПИШЕМ ЭТОМУ ВЕБУ", null, "first@example.org"),
            new RepairCase("first@example.org\nsecond@example.org\n\n(ответ)", null, "first@example.org"),
            new RepairCase("first@example.org second@example.org\n(ответ)", null, "first@example.org"),
            new RepairCase("cleared@example.org", null, "cleared@example.org"),
            new RepairCase("first@example.org\nsecond@example.org - ответ", null, "second@example.org"),
            new RepairCase("WHL - по вопросом публикации к Лене", null, "WHL - по вопросом публикации к Лене"),
            new RepairCase("", null, "Placeholder"),
            new RepairCase("Contact instructions: " + new string('x', 5_000), null, "Long contact")
        }.Select((item, index) => item with
        {
            Contact = item.Contact.Replace("@example.org", $"@case{index}.example.org", StringComparison.Ordinal),
            OldEmail = item.OldEmail?.Replace("@example.org", $"@case{index}.example.org", StringComparison.Ordinal),
            ExpectedEmail = item.ExpectedEmail?.Replace("@example.org", $"@case{index}.example.org", StringComparison.Ordinal)
        }).ToArray();
        cases[^1] = cases[^1] with { ExpectedEmail = cases[^1].Contact };
        var offers = new List<SiteWebmasterOffer>();
        foreach (var item in cases) offers.Add(await SeedOfferAsync(context, item));
        var preserved = await OfferSnapshotAsync(context);

        // Act
        await context.Database.MigrateAsync();
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = ReadUpSql("PopulateWebmasterPrimaryContacts");
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        // Assert
        await using var verification = CreateContext();
        Assert.Equal(preserved, await OfferSnapshotAsync(verification));
        for (var index = 0; index < offers.Count; index++)
            Assert.Equal(cases[index].ExpectedEmail == "Placeholder" ? $"Contact not specified ({offers[index].WebmasterId:D})" : cases[index].ExpectedEmail,
                (await verification.Webmasters.SingleAsync(w => w.Id == offers[index].WebmasterId)).PrimaryEmail);
        var entries = await verification.EntityChangeHistories.Where(h => h.ChangedBy == "Migration:" + MissingPrimaryMigration).ToListAsync();
        Assert.Equal(22, entries.Count(h => h.EntityType == "Webmaster"));
        Assert.Equal(23, entries.Count(h => h.EntityType == "SiteWebmasterOffer"));
        Assert.All(entries, entry => Assert.Equal("Data migration", entry.Source));
    }

    [PostgresFact]
    public async Task Followup_DatabaseFailure_RollsBackSchemaDataAndAuditTogether()
    {
        // Arrange
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);
        await SeedOfferAsync(context, new RepairCase("Publication instructions", null, "Publication instructions"));
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_population_update() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Injected population failure'; END; $body$;
            CREATE TRIGGER reject_population BEFORE UPDATE ON "Webmasters" FOR EACH ROW EXECUTE FUNCTION reject_population_update();
            """);

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());

        // Assert
        Assert.Equal("P0001", exception.SqlState);
        Assert.Null(await context.Webmasters.Select(w => w.PrimaryEmail).SingleAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        Assert.DoesNotContain(MissingPrimaryMigration, await context.Database.GetAppliedMigrationsAsync());
        var columnType = await context.Database.SqlQueryRaw<string>("""
            SELECT data_type AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Webmasters' AND column_name = 'PrimaryEmail'
            """).SingleAsync();
        var indexMethod = await context.Database.SqlQueryRaw<string>("""
            SELECT am.amname::text AS "Value" FROM pg_class c JOIN pg_am am ON am.oid = c.relam
            WHERE c.oid = 'public."IX_Webmasters_PrimaryEmail"'::regclass
            """).SingleAsync();
        Assert.Equal("character varying", columnType);
        Assert.Equal("btree", indexMethod);
    }

    [PostgresFact]
    public async Task Followup_Down_RestoresMissingValuePreservesAuditAndCanReapply()
    {
        // Arrange
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);
        await SeedOfferAsync(context, new RepairCase("first@example.org\nsecond@example.org - answ", null, "second@example.org"));
        await context.Database.MigrateAsync();

        // Act
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);
        var restored = await context.Webmasters.Select(w => w.PrimaryEmail).SingleAsync();
        var rollbackEntries = await context.EntityChangeHistories.CountAsync(h => h.ChangedBy == "Rollback:" + MissingPrimaryMigration);
        await context.Database.MigrateAsync();

        // Assert
        Assert.Null(restored);
        Assert.Equal(2, rollbackEntries);
        Assert.Equal("second@example.org", await context.Webmasters.Select(w => w.PrimaryEmail).SingleAsync());
        Assert.Equal(6, await context.EntityChangeHistories.CountAsync());
    }

    [PostgresFact]
    public async Task Followup_Down_RejectsLaterManualEdit()
    {
        // Arrange
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(RepairMigration);
        var offer = await SeedOfferAsync(context, new RepairCase("first@example.org\nsecond@example.org - answ", null, "second@example.org"));
        await context.Database.MigrateAsync();
        await using (var editor = CreateContext())
        {
            var webmaster = await editor.Webmasters.SingleAsync();
            webmaster.PrimaryEmail = "manual@example.org";
            webmaster.UpdatedAtUtc = DateTime.UtcNow;
            editor.EntityChangeHistories.Add(ManualHistory(offer.Id, "Primary email"));
            await editor.SaveChangesAsync();
        }

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(RepairMigration));

        // Assert
        Assert.Contains("subsequent edits", exception.MessageText, StringComparison.Ordinal);
        Assert.Equal("manual@example.org", await context.Webmasters.Select(w => w.PrimaryEmail).SingleAsync());
        Assert.Contains(MissingPrimaryMigration, await context.Database.GetAppliedMigrationsAsync());
    }

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

    private static string ReadUpSql(string name = "NormalizeWebmasterPrimaryEmails")
    {
        using var stream = typeof(ApplicationDbContext).Assembly.GetManifestResourceStream($"Redhead.SitesCatalog.Infrastructure.Data.Migrations.Sql.{name}.Up.sql")!;
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
