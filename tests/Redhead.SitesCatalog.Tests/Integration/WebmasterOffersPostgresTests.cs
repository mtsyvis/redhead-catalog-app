using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Npgsql;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Middleware;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Services.Import.Artifacts;
using Redhead.SitesCatalog.Application.Services.Import.Quarantine;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.Currencies;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;
using Redhead.SitesCatalog.Infrastructure.Options;

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
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            return;
        }

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [PostgresFact]
    public async Task GetSummaryByDomain_ProjectsOnlyAllowedColumnsOnPostgres()
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var offer = await SeedOfferAsync(setup, "remove-mailbox");
        offer.TermType = TermType.Finite;
        offer.TermValue = 2;
        offer.TermUnit = TermUnit.Year;
        await setup.SaveChangesAsync();
        var recorder = new ReadCommandRecorder();
        await using var context = CreateContext(recorder);
        var service = CreateService(context);

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

    [PostgresFact]
    public async Task MultiSearch_LoadsCollectionsWithoutCrossProductAndKeepsSummaryPrivateOnPostgres()
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var offer = await SeedOfferAsync(setup, "remove-mailbox");
        offer.Site.IsQuarantined = true;
        offer.Status = SiteWebmasterOfferStatus.Inactive;
        setup.WebmasterOfferPrices.Add(CreatePrice(offer.Id, WebmasterOfferPriceType.Casino, 50m));
        var secondMailbox = new LinkbuilderMailbox
        {
            Id = Guid.NewGuid(), Email = "second@example.com", DisplayName = "Second mailbox",
            IsActive = true, CreatedAtUtc = offer.CreatedAtUtc, UpdatedAtUtc = offer.UpdatedAtUtc
        };
        setup.LinkbuilderMailboxes.Add(secondMailbox);
        setup.SiteWebmasterOfferLinkbuilderMailboxes.Add(new SiteWebmasterOfferLinkbuilderMailbox
        {
            SiteWebmasterOfferId = offer.Id,
            LinkbuilderMailboxId = secondMailbox.Id,
            CreatedAtUtc = offer.CreatedAtUtc
        });
        setup.Sites.Add(new Site { Domain = "empty.example", CreatedAtUtc = offer.CreatedAtUtc, UpdatedAtUtc = offer.UpdatedAtUtc });
        await setup.SaveChangesAsync();
        var recorder = new ReadCommandRecorder();
        await using var context = CreateContext(recorder);
        var service = CreateService(context);
        const string query = "empty.example https://www.CONCURRENT.example/path missing.example concurrent.example";

        // Act
        var full = await service.MultiSearchAsync(query);
        var fullCommands = recorder.Commands.ToArray();
        recorder.Commands.Clear();
        var summary = await service.MultiSearchSummaryAsync(query);

        // Assert
        Assert.Equal(3, full.UniqueDomainCount);
        Assert.Equal(["concurrent.example"], full.Duplicates);
        Assert.Equal(["empty.example"], full.DomainsWithoutOffers);
        Assert.Equal(["missing.example"], full.NotFoundDomains);
        var fullGroup = Assert.Single(full.Results);
        Assert.Equal("concurrent.example", fullGroup.Domain);
        var fullOffer = Assert.Single(fullGroup.Offers);
        Assert.Equal(SiteWebmasterOfferStatus.Inactive, fullOffer.Status);
        Assert.Equal(2, fullOffer.Prices.Count);
        Assert.Equal(2, fullOffer.LinkbuilderMailboxes.Count);
        Assert.Equal(offer.ContactRawText, fullOffer.ContactRawText);
        Assert.DoesNotContain(fullCommands, sql => sql.Contains("JOIN \"WebmasterOfferPrices\"", StringComparison.Ordinal)
            && sql.Contains("JOIN \"SiteWebmasterOfferLinkbuilderMailboxes\"", StringComparison.Ordinal));
        Assert.Equal(full.DomainsWithoutOffers, summary.DomainsWithoutOffers);
        Assert.Equal(full.NotFoundDomains, summary.NotFoundDomains);
        Assert.Equal(offer.Id, Assert.Single(Assert.Single(summary.Results).Offers).Id);
        Assert.NotEmpty(recorder.Commands);
        foreach (var sql in recorder.Commands)
        {
            Assert.DoesNotContain("\"ContactRawText\"", sql);
            Assert.DoesNotContain("\"PrimaryEmail\"", sql);
            Assert.DoesNotContain("\"WebmasterOfferPrices\"", sql);
        }
    }

    [PostgresTheory]
    [InlineData("add-price")]
    [InlineData("update-price")]
    [InlineData("remove-price")]
    [InlineData("add-mailbox")]
    [InlineData("remove-mailbox")]
    [InlineData("details")]
    [InlineData("currency")]
    public async Task Update_OverlappingSaves_Returns409AndRollsBackAllLosingChanges(string scenario)
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var offer = await SeedOfferAsync(setup, scenario);
        var mailboxId = await setup.LinkbuilderMailboxes.Select(item => item.Id).SingleAsync();
        var winnerRequest = CreateRequest(offer, scenario, mailboxId, "Winner", 200m);
        var loserRequest = CreateRequest(offer, scenario, mailboxId, "Loser", 111m);
        WebmasterOfferUpdateResult? winnerResult = null;
        var interceptor = new BeforeSaveInterceptor(async () =>
        {
            await using var winnerContext = CreateContext();
            winnerResult = await CreateService(winnerContext)
                .UpdateAsync(offer.Id, winnerRequest, "winner@example.com");
        });
        await using var loserContext = CreateContext(interceptor);
        var controller = new WebmasterOffersController(CreateService(loserContext),
            NullLogger<WebmasterOffersController>.Instance, Mock.Of<IGoogleDriveExportService>())
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
            if (scenario == "currency")
            {
                Assert.Equal(expected.SourceCurrency, price.SourceCurrency);
                Assert.Equal(expected.SourceAmount, price.SourceAmount);
                Assert.Equal(OfferCurrencyService.FixedEurUsdRate, price.AppliedUsdRate);
            }
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
        await setup.Database.MigrateAsync();
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
        loserRequest.Prices[0].SourceCurrency = "EUR";
        loserRequest.Prices[0].SourceAmount = 25m;
        loserRequest.Prices[0].WebmasterPriceUsd = 31m;
        loserRequest.Prices[0].RateReference = OfferCurrencyService.FixedEurReference;
        WebmasterOfferUpdateResult? winnerResult = null;
        var interceptor = new BeforeSaveInterceptor(async () =>
        {
            await using var winnerContext = CreateContext();
            winnerResult = await CreateService(winnerContext)
                .UpdateAsync(siblingOffer.Id, winnerRequest, "winner@example.com");
        });
        await using var loserContext = CreateContext(interceptor);

        // Act
        var loserResult = await CreateService(loserContext)
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
        var unchangedPrice = await verification.WebmasterOfferPrices.SingleAsync(x => x.SiteWebmasterOfferId == selectedOffer.Id);
        Assert.Equal("USD", unchangedPrice.SourceCurrency);
        Assert.Equal("USD", unchangedPrice.SourceCurrency);
    }

    [PostgresFact]
    public async Task Update_DifferentOffersSharingWebmaster_AllowsIndependentOfferChanges()
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
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
            concurrentResult = await CreateService(concurrentContext)
                .UpdateAsync(secondOffer.Id, concurrentRequest, "second@example.com");
        });
        await using var selectedContext = CreateContext(interceptor);

        // Act
        var selectedResult = await CreateService(selectedContext)
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
        await context.Database.MigrateAsync();
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
        var offer = await SeedOfferAsync(context, "details", includePrices: false);
        var duplicate = CreatePrice(offer.Id, WebmasterOfferPriceType.Main, 200m);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WebmasterOfferPrices" ("Id", "SiteWebmasterOfferId", "PriceType", "AvailabilityStatus", "WebmasterPriceUsd", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({Guid.NewGuid()}, {offer.Id}, 0, 1, 100, {offer.CreatedAtUtc}, {offer.UpdatedAtUtc}),
                   ({duplicate.Id}, {offer.Id}, 0, 1, 200, {offer.CreatedAtUtc}, {offer.UpdatedAtUtc})
            """);

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

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quarantine_SavesSiteAndAllOffersTogetherAndAdvancesOfferVersions(bool manualEdit)
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        setup.Set<CanonicalLocation>().Add(new CanonicalLocation { Key = "US", DisplayName = "United States", IsActive = true });
        var offer = await SeedOfferAsync(setup, "remove-mailbox");
        var second = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), Site = offer.Site, Webmaster = offer.Webmaster,
            ImportFingerprint = new string('b', 64), ContactRawText = offer.ContactRawText,
            CreatedAtUtc = offer.CreatedAtUtc, UpdatedAtUtc = offer.UpdatedAtUtc
        };
        setup.SiteWebmasterOffers.Add(second);
        await setup.SaveChangesAsync();
        var firstVersion = offer.Version;
        var secondVersion = second.Version;
        await using var context = CreateContext();

        // Act
        await MarkUnavailableAsync(context, manualEdit);

        // Assert
        await using var verification = CreateContext();
        var site = await verification.Sites.SingleAsync();
        Assert.True(site.IsQuarantined);
        var saved = await verification.SiteWebmasterOffers.Include(item => item.Prices)
            .Include(item => item.LinkbuilderMailboxes).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, item =>
        {
            Assert.Equal(SiteWebmasterOfferStatus.Inactive, item.Status);
            Assert.Equal(site.UpdatedAtUtc, item.UpdatedAtUtc);
            Assert.Equal("quarantine@example.com", item.UpdatedBy);
        });
        var firstSaved = Assert.Single(saved, item => item.Id == offer.Id);
        Assert.NotEqual(firstVersion, firstSaved.Version);
        Assert.NotEqual(secondVersion, Assert.Single(saved, item => item.Id == second.Id).Version);
        Assert.Equal(100m, Assert.Single(firstSaved.Prices).WebmasterPriceUsd);
        Assert.Single(firstSaved.LinkbuilderMailboxes);
        Assert.Equal(manualEdit ? 3 : 0, await verification.EntityChangeHistories.CountAsync());
        Assert.Equal(manualEdit ? 0 : 1, await verification.ImportLogs.CountAsync());
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quarantine_ConcurrentOfferEdit_ReturnsConflictAndRollsBackSiteOffersAndLogs(bool manualEdit)
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        setup.Set<CanonicalLocation>().Add(new CanonicalLocation { Key = "US", DisplayName = "United States", IsActive = true });
        var offer = await SeedOfferAsync(setup, "details");
        var interceptor = new BeforeSaveInterceptor(async () =>
        {
            await using var winner = CreateContext();
            var current = await winner.SiteWebmasterOffers.SingleAsync();
            current.CommentText = "Concurrent edit";
            await winner.SaveChangesAsync();
        });
        await using var context = CreateContext(interceptor);
        using var responseBody = new MemoryStream();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = manualEdit ? "/api/sites/concurrent.example" : "/api/imports/availability";
        httpContext.Request.Method = manualEdit ? "PUT" : "POST";
        httpContext.Response.Body = responseBody;
        var exceptionHandler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<QuarantineConcurrencyException>(() => MarkUnavailableAsync(context, manualEdit));
        await exceptionHandler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await context.SaveChangesAsync());
        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
        responseBody.Position = 0;
        using var response = await System.Text.Json.JsonDocument.ParseAsync(responseBody);
        Assert.Equal("The data was changed by another user. Refresh the data and try again.",
            response.RootElement.GetProperty("message").GetString());
        await using var verification = CreateContext();
        Assert.False((await verification.Sites.SingleAsync()).IsQuarantined);
        var saved = await verification.SiteWebmasterOffers.SingleAsync();
        Assert.Equal(offer.Id, saved.Id);
        Assert.Equal(SiteWebmasterOfferStatus.Active, saved.Status);
        Assert.Equal("Concurrent edit", saved.CommentText);
        Assert.Empty(await verification.ImportLogs.ToListAsync());
        Assert.Empty(await verification.EntityChangeHistories.ToListAsync());
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quarantine_ConcurrentSiteDeletion_DoesNotTranslateUnrelatedConcurrency(bool manualEdit)
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        setup.Set<CanonicalLocation>().Add(new CanonicalLocation { Key = "US", DisplayName = "United States", IsActive = true });
        setup.Sites.Add(new Site
        {
            Domain = "concurrent.example", Location = "US",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await setup.SaveChangesAsync();
        var interceptor = new BeforeSaveInterceptor(async () =>
        {
            await using var concurrent = CreateContext();
            concurrent.Sites.Remove(await concurrent.Sites.SingleAsync());
            await concurrent.SaveChangesAsync();
        });
        await using var context = CreateContext(interceptor);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => MarkUnavailableAsync(context, manualEdit));

        // Assert
        Assert.IsType<Site>(Assert.Single(exception.Entries).Entity);
        await using var verification = CreateContext();
        Assert.Empty(await verification.Sites.ToListAsync());
        Assert.Empty(await verification.ImportLogs.ToListAsync());
        Assert.Empty(await verification.EntityChangeHistories.ToListAsync());
    }

    private static async Task MarkUnavailableAsync(ApplicationDbContext context, bool manualEdit)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        if (manualEdit)
        {
            var service = new SitesService(context, new SitesQueryBuilder(context),
                new SitesCatalogCache(cache), new LocationNormalizer());
            await service.UpdateSiteAsync("https://www.concurrent.example/",
                new Redhead.SitesCatalog.Application.Models.UpdateSiteRequest
                {
                    Location = "US", IsQuarantined = true, QuarantineReason = "Policy review"
                }, "quarantine@example.com");
        }
        else
        {
            var service = new QuarantineImportService(context, NullLogger<QuarantineImportService>.Instance,
                new ImportArtifactStorageService(cache));
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                "Domain,Reason\nhttps://www.concurrent.example/,Policy review\n"));
            await service.ImportAsync(stream, "quarantine.csv", "text/csv", "user-1", "quarantine@example.com",
                SiteAvailabilityImportAction.MarkUnavailable);
        }
    }

    [PostgresFact]
    public async Task CurrencySave_NoOpKeepsXminAndUnrelatedEditsKeepSavedConversion()
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var offer = await SeedOfferAsync(setup, "details");
        var request = CreateRequest(offer, "currency", Guid.Empty, null!, 25m);
        var first = await CreateService(setup).UpdateAsync(offer.Id, request, "editor@example.com");
        await setup.Entry(offer).ReloadAsync();
        await setup.Entry(offer.Prices.Single()).ReloadAsync();
        var savedTime = offer.UpdatedAtUtc;
        var savedPriceTime = offer.Prices.Single().UpdatedAtUtc;
        request.ExpectedVersion = first.Offer!.Version;

        // Act
        var noOp = await CreateService(setup).UpdateAsync(offer.Id, request, "editor@example.com");
        await using var read = CreateContext();
        var unchanged = await read.SiteWebmasterOffers.Include(x => x.Prices).SingleAsync();
        request.CommentText = "Unrelated edit";
        var changed = await CreateService(setup).UpdateAsync(offer.Id, request, "editor@example.com");

        // Assert
        Assert.Equal(first.Offer.Version, noOp.Offer!.Version);
        Assert.Equal(savedTime, unchanged.UpdatedAtUtc);
        Assert.Equal(savedPriceTime, unchanged.Prices.Single().UpdatedAtUtc);
        Assert.NotEqual(noOp.Offer.Version, changed.Offer!.Version);
        Assert.Equal(savedPriceTime, offer.Prices.Single().UpdatedAtUtc);
        Assert.Equal(2, await read.EntityChangeHistories.CountAsync());
    }

    [PostgresTheory]
    [InlineData("null-source")]
    [InlineData("zero-rate")]
    [InlineData("null-rate")]
    [InlineData("inconsistent-usd")]
    [InlineData("non-numeric")]
    public async Task CurrencyConstraints_RejectInconsistentQuotes(string scenario)
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var offer = await SeedOfferAsync(setup, "details");
        await CreateService(setup).UpdateAsync(offer.Id,
            CreateRequest(offer, "currency", Guid.Empty, "Foreign", 25m), null);
        var price = offer.Prices.Single();
        switch (scenario)
        {
            case "null-source": price.SourceAmount = null; break;
            case "zero-rate": price.AppliedUsdRate = 0; break;
            case "null-rate": price.AppliedUsdRate = null; break;
            case "inconsistent-usd": price.WebmasterPriceUsd = 1; break;
            case "non-numeric": price.AvailabilityStatus = ServiceAvailabilityStatus.Unknown; price.WebmasterPriceUsd = null; break;
        }

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => setup.SaveChangesAsync());

        // Assert
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        await using var verification = CreateContext();
        Assert.Equal(31m, (await verification.WebmasterOfferPrices.SingleAsync()).WebmasterPriceUsd);
        Assert.Single(await verification.EntityChangeHistories.ToListAsync());
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrencyRefresh_SkipsRepeatedTicksAndPreservesOffersAcrossRestart(bool fail)
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var offer = await SeedOfferAsync(setup, "details");
        var originalVersion = offer.Version;
        setup.CurrencyReferenceSnapshots.Add(new CurrencyReferenceSnapshot
        {
            Id = Guid.NewGuid(), ProviderAsOfUtc = DateTime.UtcNow.AddDays(-1),
            FetchedAtUtc = DateTime.UtcNow.AddDays(-1), UsdRatesJson = "{\"CAD\":0.75}"
        });
        await setup.SaveChangesAsync();
        var client = new Mock<Redhead.SitesCatalog.Infrastructure.Integrations.Currencies.ICurrencyReferenceRateClient>();
        if (fail)
        {
            client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new FormatException("Synthetic provider failure"));
        }
        else
        {
            client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new Redhead.SitesCatalog.Infrastructure.Integrations.Currencies.CurrencyRateTable(DateTime.UtcNow,
                new Dictionary<string, decimal> { ["CAD"] = 0.8m }));
        }

        var options = Microsoft.Extensions.Options.Options.Create(new Redhead.SitesCatalog.Infrastructure.Options.ExchangeRateApiOptions
            { ApiKey = "synthetic-test-key" });
        await using var first = CreateContext();
        var worker = new CurrencyReferenceRefreshService(first, client.Object, options, TimeProvider.System);

        // Act
        await worker.TickAsync(CancellationToken.None);
        await worker.TickAsync(CancellationToken.None);
        await using var restarted = CreateContext();
        await new CurrencyReferenceRefreshService(restarted, client.Object, options, TimeProvider.System).TickAsync(CancellationToken.None);

        // Assert
        client.Verify(x => x.FetchAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, await restarted.CurrencyReferenceSnapshots.CountAsync());
        Assert.Equal(!fail, (await restarted.CurrencyReferenceRefreshStates.SingleAsync()).LastAttemptSucceeded);
        var saved = await restarted.SiteWebmasterOffers.Include(x => x.Prices).SingleAsync();
        Assert.Equal(originalVersion, saved.Version);
        Assert.Equal(offer.UpdatedAtUtc, saved.UpdatedAtUtc);
        Assert.Equal(100m, saved.Prices.Single().WebmasterPriceUsd);
        Assert.Empty(await restarted.EntityChangeHistories.ToListAsync());
    }

    [PostgresFact]
    public async Task CurrencyRefresh_SnapshotSaveFailureDoesNotRepeatProviderRequestAfterRestart()
    {
        // Arrange
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var offer = await SeedOfferAsync(setup, "unchanged");
        setup.CurrencyReferenceSnapshots.Add(new CurrencyReferenceSnapshot
        {
            Id = Guid.NewGuid(), ProviderAsOfUtc = DateTime.UtcNow.AddDays(-1),
            FetchedAtUtc = DateTime.UtcNow.AddDays(-1), UsdRatesJson = "{\"CAD\":0.75}"
        });
        await setup.SaveChangesAsync();
        var client = new Mock<Redhead.SitesCatalog.Infrastructure.Integrations.Currencies.ICurrencyReferenceRateClient>();
        client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            await using var persisted = CreateContext();
            var claim = await persisted.CurrencyReferenceRefreshStates.SingleAsync();
            Assert.True(claim.NextAttemptAtUtc > DateTime.UtcNow);
            return new Redhead.SitesCatalog.Infrastructure.Integrations.Currencies.CurrencyRateTable(
                DateTime.UtcNow, new Dictionary<string, decimal> { ["CAD"] = 0.8m });
        });
        var options = Options.Create(new ExchangeRateApiOptions { ApiKey = "synthetic-test-key" });
        await using var failing = CreateContext(new FailCurrencySnapshotSaveInterceptor());

        // Act
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new CurrencyReferenceRefreshService(failing, client.Object, options, TimeProvider.System).TickAsync(CancellationToken.None));
        await using var restarted = CreateContext();
        await new CurrencyReferenceRefreshService(restarted, client.Object, options, TimeProvider.System).TickAsync(CancellationToken.None);

        // Assert
        client.Verify(x => x.FetchAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False((await restarted.CurrencyReferenceRefreshStates.SingleAsync()).LastAttemptSucceeded);
        var rates = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(
            (await restarted.CurrencyReferenceSnapshots.SingleAsync()).UsdRatesJson);
        Assert.Equal(0.75m, rates!["CAD"]);
        var saved = await restarted.SiteWebmasterOffers.Include(x => x.Prices).SingleAsync();
        Assert.Equal(offer.Version, saved.Version);
        Assert.Equal(offer.UpdatedAtUtc, saved.UpdatedAtUtc);
        Assert.Equal(100m, saved.Prices.Single().WebmasterPriceUsd);
        Assert.Empty(await restarted.EntityChangeHistories.ToListAsync());
    }

    [PostgresTheory]
    [InlineData("43.55", 54)]
    [InlineData("43.56", 55)]
    [InlineData("43.59", 55)]
    [InlineData("43.60", 55)]
    public async Task CurrencyRounding_PreviewAndPersistenceUsePoint45Threshold(string sourceAmount, int expectedUsd)
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var offer = await SeedOfferAsync(context, "details");
        var amount = decimal.Parse(sourceAmount, System.Globalization.CultureInfo.InvariantCulture);
        var currencies = new OfferCurrencyService(context);
        var request = CreateRequest(offer, "currency", Guid.Empty, "Rounded quote", amount);
        request.Prices[0].WebmasterPriceUsd = null;

        // Act
        var preview = await currencies.ConvertAsync(new OfferConversionRequest("EUR", amount), false);
        await new WebmasterOffersService(context, currencies).UpdateAsync(offer.Id, request, null);
        await using var verification = CreateContext();
        var saved = await verification.WebmasterOfferPrices.SingleAsync();

        // Assert
        Assert.Equal(expectedUsd, preview.WebmasterPriceUsd);
        Assert.Equal(preview.WebmasterPriceUsd, saved.WebmasterPriceUsd);
        Assert.Equal(amount, saved.SourceAmount);
        Assert.Single(await verification.EntityChangeHistories.ToListAsync());
    }

    [PostgresFact]
    public async Task CurrencyRounding_PreservesHistoricalCeilingUntilSourceQuoteChanges()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var offer = await SeedOfferAsync(context, "details");
        var price = offer.Prices.Single();
        price.SourceCurrency = "EUR";
        price.SourceAmount = 25m;
        price.AppliedUsdRate = OfferCurrencyService.FixedEurUsdRate;
        price.WebmasterPriceUsd = 32m;
        await context.SaveChangesAsync();
        var request = CreateRequest(offer, "details", Guid.Empty, "Notes only", 100m);
        request.Prices[0].SourceCurrency = "EUR";
        request.Prices[0].SourceAmount = 25m;
        request.Prices[0].WebmasterPriceUsd = 32m;
        var service = CreateService(context);

        // Act
        var notesOnly = await service.UpdateAsync(offer.Id, request, null);
        var unchangedUsd = price.WebmasterPriceUsd;
        var unchangedRate = price.AppliedUsdRate;
        request.ExpectedVersion = notesOnly.Offer!.Version;
        request.Prices[0].SourceCurrency = "EUR";
        request.Prices[0].SourceAmount = 25.01m;
        request.Prices[0].RateReference = OfferCurrencyService.FixedEurReference;
        request.Prices[0].WebmasterPriceUsd = null;
        await service.UpdateAsync(offer.Id, request, null);

        // Assert
        Assert.Equal(32m, unchangedUsd);
        Assert.Equal(OfferCurrencyService.FixedEurUsdRate, unchangedRate);
        await using var verification = CreateContext();
        var saved = await verification.WebmasterOfferPrices.SingleAsync();
        Assert.Equal(31m, saved.WebmasterPriceUsd);
        Assert.Equal(25.01m, saved.SourceAmount);
        Assert.Equal(2, await verification.EntityChangeHistories.CountAsync());
    }

    private static WebmasterOffersService CreateService(ApplicationDbContext context)
        => new(context, new OfferCurrencyService(context));

    private ApplicationDbContext CreateContext(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connectionString);
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return new ApplicationDbContext(options.Options);
    }

    private static async Task<SiteWebmasterOffer> SeedOfferAsync(ApplicationDbContext context, string scenario, bool includePrices = true)
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
                PrimaryEmail = "contact@example.com",
                CreatedAtUtc = now, UpdatedAtUtc = now
            }
        };
        if (includePrices)
        {
            offer.Prices.Add(CreatePrice(offer.Id, WebmasterOfferPriceType.Main, 100m));
        }

        if (includePrices && scenario == "remove-price")
        {
            offer.Prices.Add(CreatePrice(offer.Id, WebmasterOfferPriceType.Casino, 50m));
        }

        if (scenario == "remove-mailbox")
        {
            offer.LinkbuilderMailboxes.Add(new SiteWebmasterOfferLinkbuilderMailbox
        {
            SiteWebmasterOfferId = offer.Id, LinkbuilderMailbox = mailbox, LinkbuilderMailboxId = mailbox.Id,
            CreatedAtUtc = now
        });
        }

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
                SourceCurrency = "USD",
                SourceAmount = scenario == "update-price" ? amount : 100m,
                PriceType = WebmasterOfferPriceType.Main, AvailabilityStatus = ServiceAvailabilityStatus.Available,
                WebmasterPriceUsd = scenario == "update-price" ? amount : 100m
            }],
            LinkbuilderMailboxIds = scenario == "add-mailbox" ? [mailboxId] : []
        };
        if (scenario == "add-price")
        {
            request.Prices.Add(new UpdateWebmasterOfferPriceRequest
        {
            SourceCurrency = "USD",
            SourceAmount = amount,
            PriceType = WebmasterOfferPriceType.Casino, AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = amount
        });
        }

        if (scenario == "currency")
        {
            request.Prices[0].SourceCurrency = "EUR";
            request.Prices[0].SourceAmount = amount;
            request.Prices[0].RateReference = OfferCurrencyService.FixedEurReference;
            request.Prices[0].WebmasterPriceUsd = OfferCurrencyService.ConvertToUsd(amount, "EUR", OfferCurrencyService.FixedEurUsdRate);
        }
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

    private sealed class FailCurrencySnapshotSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<CurrencyReferenceSnapshot>().Any(x => x.State == EntityState.Added))
            {
                throw new DbUpdateException("Synthetic snapshot save failure");
            }
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
            {
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
            }
        }
    }

    private sealed class PostgresTheoryAttribute : TheoryAttribute
    {
        public PostgresTheoryAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
            }
        }
    }
}
