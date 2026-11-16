using Microsoft.EntityFrameworkCore;
using Npgsql;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Tests.Application.Services;

namespace Redhead.SitesCatalog.Tests.Integration;

public sealed class BulkOfferPricePreviewPostgresTests
{
    [PostgresFact]
    public async Task Preview_RunsInReadOnlyTransaction_WithExactContactMatchingAndOneSharedCurrencyReference()
    {
        // Arrange
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")!) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var database = "redhead_bulk_preview_test_" + Guid.NewGuid().ToString("N");
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
        await create.ExecuteNonQueryAsync();
        builder.Database = database;
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(builder.ConnectionString).Options);
        try
        {
            await context.Database.MigrateAsync();
            var now = DateTime.UtcNow;
            var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 20m, 1m, now);
            offer.Site.IsQuarantined = true;
            offer.Status = SiteWebmasterOfferStatus.Inactive;
            var second = new SiteWebmasterOffer
            {
                Id = Guid.NewGuid(), SiteDomain = "second.example", Site = new Site { Domain = "second.example", CreatedAtUtc = now, UpdatedAtUtc = now },
                Webmaster = new Webmaster { Id = Guid.NewGuid(), PrimaryEmail = "contact@example.com", ContactRawText = "different technical record", NormalizedContactRawText = "different technical record", CreatedAtUtc = now, UpdatedAtUtc = now },
                ImportFingerprint = new string('b', 64), ContactRawText = "raw evidence", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            context.SiteWebmasterOffers.Add(second);
            OfferCurrencyTestData.AddSnapshot(context, 0.8m, now);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
            var request = new BulkOfferPricePreviewRequest(" CONTACT@EXAMPLE.COM ", TermType.Finite, 1, TermUnit.Year, "CAD",
                [WebmasterOfferPriceType.Main], [new(1, "https://www.CURRENCY.example/path", ["100"]), new(2, "second.example", ["50"])]);

            // Act
            var preview = await new BulkOfferPricePreviewService(context, new OfferCurrencyService(context)).PreviewAsync(request);

            // Assert
            Assert.Equal(2, preview.ReadyCount);
            Assert.Equal(85m, preview.Rows[0].Prices[0].Proposed.WebmasterPriceUsd);
            Assert.Equal(45m, preview.Rows[1].Prices[0].Proposed.WebmasterPriceUsd);
            Assert.Equal(5m, preview.Rows[0].Prices[0].Proposed.AppliedUsdSurcharge);
            Assert.Equal(5m, preview.Rows[1].Prices[0].Proposed.AppliedUsdSurcharge);
            Assert.Equal(5m, preview.ConversionReference!.AppliedUsdSurcharge);
            Assert.Equal(SiteWebmasterOfferStatus.Active, preview.Rows[0].ProposedStatus);
            Assert.Equal("No term", preview.Rows[0].CurrentTerm);
            Assert.Equal("1 year", preview.Rows[0].ProposedTerm);
            Assert.Equal(new DateOnly(now.Year, now.Month, now.Day), preview.ConversionReference!.RateDate);
            Assert.Equal(20m, await context.WebmasterOfferPrices.Select(price => price.WebmasterPriceUsd).SingleAsync());
            Assert.Equal(0m, await context.WebmasterOfferPrices.Select(price => price.AppliedUsdSurcharge).SingleAsync());
            Assert.Equal(SiteWebmasterOfferStatus.Inactive, await context.SiteWebmasterOffers.Where(value => value.Id == offer.Id).Select(value => value.Status).SingleAsync());
            Assert.True(await context.Sites.Where(site => site.Domain == offer.SiteDomain).Select(site => site.IsQuarantined).SingleAsync());
            Assert.Empty(await context.EntityChangeHistories.ToListAsync());
            Assert.Empty(context.ChangeTracker.Entries());
            await transaction.RollbackAsync();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")))
            {
                Skip = "Set REDHEAD_TEST_POSTGRES to run isolated PostgreSQL tests.";
            }
        }
    }
}
