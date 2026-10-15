using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Integration;

// Opt-in: each test creates and drops its own database on the supplied test PostgreSQL server.
public sealed class WebmasterOffersPostgresTests : IAsyncLifetime
{
    private const string ConnectionVariable = "REDHEAD_TEST_POSTGRES";
    private const string PreviousMigration = "20260818105816_AddEntityChangeHistoryAndWebmasterOfferUpdatedBy";
    private const string UniquePricesMigration = "20260905070440_EnforceWebmasterOfferConcurrencyAndUniquePrices";
    private readonly string _databaseName = "redhead_offer_test_" + Guid.NewGuid().ToString("N");
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!)
        {
            Pooling = false
        };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
        await command.ExecuteNonQueryAsync();
        builder.Database = _databaseName;
        _connectionString = builder.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_connectionString)) return;
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [PostgresFact]
    public async Task GetSummaryByDomain_ProjectsOnlyAllowedColumnsOnPostgres()
    {
        // Arrange
        await using var setup = CreateContext();
        var offer = await SeedOfferAsync(setup, "remove-mailbox");
        offer.TermType = TermType.Finite;
        offer.TermValue = 2;
        offer.TermUnit = TermUnit.Year;
        await setup.SaveChangesAsync();
        var recorder = new ReadCommandRecorder();
        await using var context = CreateContext(recorder);
        var service = new WebmasterOffersService(context);

        // Act
        var result = await service.GetSummaryByDomainAsync("https://www.concurrent.example/path");

        // Assert
        Assert.True(result.SiteFound);
        var summary = Assert.Single(result.Offers);
        Assert.Equal(offer.Id, summary.Id);
        Assert.Equal("2 years", summary.TermLabel);
        Assert.Equal("mailbox@example.com", Assert.Single(summary.LinkbuilderMailboxes).Email);
        Assert.NotEmpty(recorder.Commands);
        foreach (var sql in recorder.Commands)
        {
            Assert.DoesNotContain("\"ContactRawText\"", sql);
            Assert.DoesNotContain("\"PrimaryEmail\"", sql);
            Assert.DoesNotContain("\"Webmasters\"", sql);
            Assert.DoesNotContain("\"WebmasterOfferPrices\"", sql);
            Assert.DoesNotContain("\"WebmasterPriceUsd\"", sql);
            Assert.DoesNotContain("\"WebmasterPriceDetails\"", sql);
        }
    }

    [PostgresTheory]
    [InlineData("add-price")]
    [InlineData("update-price")]
    [InlineData("remove-price")]
    [InlineData("add-mailbox")]
    [InlineData("remove-mailbox")]
    [InlineData("details")]
    public async Task Update_OverlappingSaves_Returns409AndRollsBackAllLosingChanges(string scenario)
    {
        // Arrange
        await using var setup = CreateContext();
        var offer = await SeedOfferAsync(setup, scenario);
        var mailboxId = await setup.LinkbuilderMailboxes.Select(item => item.Id).SingleAsync();
        var winnerRequest = CreateRequest(offer, scenario, mailboxId, "Winner", 200m);
        var loserRequest = CreateRequest(offer, scenario, mailboxId, "Loser", 111m);
        WebmasterOfferUpdateResult? winnerResult = null;
        var interceptor = new BeforeSaveInterceptor(async () =>
        {
            await using var winnerContext = CreateContext();
            winnerResult = await new WebmasterOffersService(winnerContext)
                .UpdateAsync(offer.Id, winnerRequest, "winner@example.com");
        });
        await using var loserContext = CreateContext(interceptor);
        var controller = new WebmasterOffersController(new WebmasterOffersService(loserContext))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var response = await controller.Update(offer.Id, loserRequest, CancellationToken.None);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(response.Result).StatusCode);
        Assert.Equal(WebmasterOfferUpdateStatus.Success, winnerResult!.Status);
        Assert.Empty(loserContext.ChangeTracker.Entries());
        Assert.Equal(0, await loserContext.SaveChangesAsync());
        await using var verification = CreateContext();
        var saved = await verification.SiteWebmasterOffers.Include(item => item.Prices)
            .Include(item => item.LinkbuilderMailboxes).SingleAsync();
        Assert.Equal("Winner", saved.CommentText);
        Assert.Equal("winner@example.com", saved.UpdatedBy);
        Assert.Equal(winnerResult.Offer!.Version, saved.Version);
        Assert.Equal(winnerRequest.Prices.Count, saved.Prices.Count);
        foreach (var expected in winnerRequest.Prices)
        {
            var price = Assert.Single(saved.Prices, item => item.PriceType == expected.PriceType);
            Assert.Equal(expected.WebmasterPriceUsd, price.WebmasterPriceUsd);
            Assert.Equal(expected.AvailabilityStatus, price.AvailabilityStatus);
            Assert.Equal(expected.WebmasterPriceDetails, price.WebmasterPriceDetails);
        }
        Assert.Equal(winnerRequest.LinkbuilderMailboxIds.Order(), saved.LinkbuilderMailboxes.Select(item => item.LinkbuilderMailboxId).Order());
        var history = Assert.Single(await verification.EntityChangeHistories.ToListAsync());
        Assert.Equal("winner@example.com", history.ChangedBy);
        Assert.Contains("Winner", history.ChangesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Loser", history.ChangesJson, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task Update_DifferentOffersSharingWebmaster_RejectsStaleWebmasterVersion()
    {
        // Arrange
        await using var setup = CreateContext();
        var selectedOffer = await SeedOfferAsync(setup, "details");
        var siblingOffer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = selectedOffer.SiteDomain,
            WebmasterId = selectedOffer.WebmasterId,
            Webmaster = selectedOffer.Webmaster,
            ImportFingerprint = new string('b', 64),
            ContactRawText = selectedOffer.ContactRawText,
            Status = selectedOffer.Status,
            CreatedAtUtc = selectedOffer.CreatedAtUtc,
            UpdatedAtUtc = selectedOffer.UpdatedAtUtc
        };
        setup.SiteWebmasterOffers.Add(siblingOffer);
        await setup.SaveChangesAsync();
        var mailboxId = await setup.LinkbuilderMailboxes.Select(item => item.Id).SingleAsync();
        var winnerRequest = CreateRequest(siblingOffer, "details", mailboxId, "Winner", 100m);
        winnerRequest.PrimaryEmail = "winner@example.com";
        var loserRequest = CreateRequest(selectedOffer, "details", mailboxId, "Loser", 100m);
        loserRequest.PrimaryEmail = "loser@example.com";
        WebmasterOfferUpdateResult? winnerResult = null;
        var interceptor = new BeforeSaveInterceptor(async () =>
        {
            await using var winnerContext = CreateContext();
            winnerResult = await new WebmasterOffersService(winnerContext)
                .UpdateAsync(siblingOffer.Id, winnerRequest, "winner@example.com");
        });
        await using var loserContext = CreateContext(interceptor);

        // Act
        var loserResult = await new WebmasterOffersService(loserContext)
            .UpdateAsync(selectedOffer.Id, loserRequest, "loser@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, winnerResult!.Status);
        Assert.Equal(WebmasterOfferUpdateStatus.Conflict, loserResult.Status);
        Assert.Empty(loserContext.ChangeTracker.Entries());
        await using var verification = CreateContext();
        var savedWebmaster = await verification.Webmasters.SingleAsync();
        Assert.Equal("winner@example.com", savedWebmaster.PrimaryEmail);
        Assert.Null((await verification.SiteWebmasterOffers.SingleAsync(item => item.Id == selectedOffer.Id)).CommentText);
        Assert.Equal("Winner", (await verification.SiteWebmasterOffers.SingleAsync(item => item.Id == siblingOffer.Id)).CommentText);
        var history = Assert.Single(await verification.EntityChangeHistories.ToListAsync());
        Assert.Equal("winner@example.com", history.ChangedBy);
    }

    [PostgresFact]
    public async Task Update_DifferentOffersSharingWebmaster_AllowsIndependentOfferChanges()
    {
        // Arrange
        await using var setup = CreateContext();
        var firstOffer = await SeedOfferAsync(setup, "details");
        var secondOffer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = firstOffer.SiteDomain,
            WebmasterId = firstOffer.WebmasterId,
            Webmaster = firstOffer.Webmaster,
            ImportFingerprint = new string('b', 64),
            ContactRawText = firstOffer.ContactRawText,
            Status = firstOffer.Status,
            CreatedAtUtc = firstOffer.CreatedAtUtc,
            UpdatedAtUtc = firstOffer.UpdatedAtUtc
        };
        setup.SiteWebmasterOffers.Add(secondOffer);
        await setup.SaveChangesAsync();
        var webmasterVersion = firstOffer.Webmaster.Version;
        var mailboxId = await setup.LinkbuilderMailboxes.Select(item => item.Id).SingleAsync();
        var concurrentRequest = CreateRequest(secondOffer, "details", mailboxId, "Second", 100m);
        var selectedRequest = CreateRequest(firstOffer, "details", mailboxId, "First", 100m);
        WebmasterOfferUpdateResult? concurrentResult = null;
        var interceptor = new BeforeSaveInterceptor(async () =>
        {
            await using var concurrentContext = CreateContext();
            concurrentResult = await new WebmasterOffersService(concurrentContext)
                .UpdateAsync(secondOffer.Id, concurrentRequest, "second@example.com");
        });
        await using var selectedContext = CreateContext(interceptor);

        // Act
        var selectedResult = await new WebmasterOffersService(selectedContext)
            .UpdateAsync(firstOffer.Id, selectedRequest, "first@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, concurrentResult!.Status);
        Assert.Equal(WebmasterOfferUpdateStatus.Success, selectedResult.Status);
        await using var verification = CreateContext();
        Assert.Equal("First", (await verification.SiteWebmasterOffers.SingleAsync(item => item.Id == firstOffer.Id)).CommentText);
        Assert.Equal("Second", (await verification.SiteWebmasterOffers.SingleAsync(item => item.Id == secondOffer.Id)).CommentText);
        Assert.Equal(webmasterVersion, (await verification.Webmasters.SingleAsync()).Version);
        Assert.Equal(2, await verification.EntityChangeHistories.CountAsync());
    }

    [PostgresFact]
    public async Task UniquePriceIndex_RejectsDuplicateButAllowsAnotherPriceType()
    {
        // Arrange
        await using var context = CreateContext();
        var offer = await SeedOfferAsync(context, "details");
        context.WebmasterOfferPrices.Add(CreatePrice(offer.Id, WebmasterOfferPriceType.Casino, 50m));
        await context.SaveChangesAsync();
        context.WebmasterOfferPrices.Add(CreatePrice(offer.Id, WebmasterOfferPriceType.Main, 200m));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Assert
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        await using var verification = CreateContext();
        Assert.Equal(2, await verification.WebmasterOfferPrices.CountAsync());
        Assert.Equal(100m, (await verification.WebmasterOfferPrices.SingleAsync(item => item.PriceType == WebmasterOfferPriceType.Main)).WebmasterPriceUsd);
    }

    [PostgresFact]
    public async Task Migration_ExistingDuplicates_StopsWithoutChangingPricesOrIndex()
    {
        // Arrange
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var offer = await SeedOfferAsync(context, "details");
        var duplicate = CreatePrice(offer.Id, WebmasterOfferPriceType.Main, 200m);
        context.WebmasterOfferPrices.Add(duplicate);
        await context.SaveChangesAsync();

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());

        // Assert
        Assert.Contains("Duplicate webmaster offer price types exist", exception.MessageText, StringComparison.Ordinal);
        await using var verification = CreateContext();
        Assert.Equal(new[] { 100m, 200m }, await verification.WebmasterOfferPrices.OrderBy(item => item.WebmasterPriceUsd)
            .Select(item => item.WebmasterPriceUsd!.Value).ToArrayAsync());
        Assert.DoesNotContain(UniquePricesMigration, await verification.Database.GetAppliedMigrationsAsync());
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var indexCommand = new NpgsqlCommand("""
            SELECT indisunique FROM pg_index
            WHERE indexrelid = '"IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType"'::regclass
            """, connection);
        Assert.Equal(false, await indexCommand.ExecuteScalarAsync());
    }

    private ApplicationDbContext CreateContext(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connectionString);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new ApplicationDbContext(options.Options);
    }

    private static async Task<SiteWebmasterOffer> SeedOfferAsync(ApplicationDbContext context, string scenario)
    {
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var mailbox = new LinkbuilderMailbox
        {
            Id = Guid.NewGuid(), Email = "mailbox@example.com", DisplayName = "Mailbox",
            IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = "concurrent.example", ImportFingerprint = new string('a', 64),
            ContactRawText = "contact@example.com", CreatedAtUtc = now, UpdatedAtUtc = now,
            Site = new Site { Domain = "concurrent.example", Location = "US", CreatedAtUtc = now, UpdatedAtUtc = now },
            Webmaster = new Webmaster
            {
                Id = Guid.NewGuid(), ContactRawText = "contact@example.com", NormalizedContactRawText = "contact@example.com",
                CreatedAtUtc = now, UpdatedAtUtc = now
            }
        };
        offer.Prices.Add(CreatePrice(offer.Id, WebmasterOfferPriceType.Main, 100m));
        if (scenario == "remove-price") offer.Prices.Add(CreatePrice(offer.Id, WebmasterOfferPriceType.Casino, 50m));
        if (scenario == "remove-mailbox") offer.LinkbuilderMailboxes.Add(new SiteWebmasterOfferLinkbuilderMailbox
        {
            SiteWebmasterOfferId = offer.Id, LinkbuilderMailbox = mailbox, LinkbuilderMailboxId = mailbox.Id,
            CreatedAtUtc = now
        });
        context.LinkbuilderMailboxes.Add(mailbox);
        context.SiteWebmasterOffers.Add(offer);
        await context.SaveChangesAsync();
        return offer;
    }

    private static WebmasterOfferPrice CreatePrice(Guid offerId, WebmasterOfferPriceType type, decimal amount) => new()
    {
        Id = Guid.NewGuid(), SiteWebmasterOfferId = offerId, PriceType = type,
        AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = amount,
        CreatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static UpdateWebmasterOfferRequest CreateRequest(
        SiteWebmasterOffer offer, string scenario, Guid mailboxId, string comment, decimal amount)
    {
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = offer.Version,
            ExpectedWebmasterVersion = offer.Webmaster.Version,
            ContactRawText = offer.ContactRawText,
            PrimaryEmail = offer.Webmaster.PrimaryEmail,
            Status = offer.Status,
            CommentText = comment,
            Prices = [new UpdateWebmasterOfferPriceRequest
            {
                PriceType = WebmasterOfferPriceType.Main, AvailabilityStatus = ServiceAvailabilityStatus.Available,
                WebmasterPriceUsd = scenario == "update-price" ? amount : 100m
            }],
            LinkbuilderMailboxIds = scenario == "add-mailbox" ? [mailboxId] : []
        };
        if (scenario == "add-price") request.Prices.Add(new UpdateWebmasterOfferPriceRequest
        {
            PriceType = WebmasterOfferPriceType.Casino, AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = amount
        });
        return request;
    }

    private sealed class ReadCommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BeforeSaveInterceptor(Func<Task> beforeSave) : SaveChangesInterceptor
    {
        private bool _invoked;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_invoked)
            {
                _invoked = true;
                await beforeSave();
            }
            return result;
        }
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
        }
    }

    private sealed class PostgresTheoryAttribute : TheoryAttribute
    {
        public PostgresTheoryAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
        }
    }
}
