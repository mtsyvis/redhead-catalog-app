using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Tests.Application.Services;

namespace Redhead.SitesCatalog.Tests.Integration;

public sealed class BulkOfferPriceOperationPostgresTests : IClassFixture<BulkOfferOperationDatabase>
{
    private readonly BulkOfferOperationDatabase _database;
    public BulkOfferPriceOperationPostgresTests(BulkOfferOperationDatabase database) => _database = database;

    [PostgresFact]
    public async Task Apply_SavesFollowUpForUnchangedDomains_AndReadsLegacyResultsWithoutFollowUp()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 20, 1, DateTime.UtcNow);
        var unchanged = AddOffer(context, new Site { Domain = "unchanged.example" }, "contact@example.com", 10);
        unchanged.Prices.Add(Price(WebmasterOfferPriceType.Casino, 40));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var input = Input("30") with { Rows = [new(1, "currency.example", ["30"]), new(2, "unchanged.example", ["10"])] };
        var preview = await Preview(context).PreviewAsync(input);

        // Act
        var result = await Service(context).ApplyAsync(new(Guid.NewGuid(), input, preview.PreviewVersion), "editor@example.com");
        await using var fresh = _database.CreateContext();
        var reopened = await Service(fresh).GetAsync(result.Id);
        var saved = await fresh.BulkOfferPriceOperations.SingleAsync();
        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(saved.SnapshotJson)!;
        foreach (var row in legacyJson["operation"]!["result"]!["rows"]!.AsArray())
        {
            row!.AsObject().Remove("unsubmittedPriceTypes");
        }
        saved.SnapshotJson = legacyJson.ToJsonString();
        await fresh.SaveChangesAsync();
        var legacy = await Service(fresh).GetAsync(result.Id);

        // Assert
        var unchangedRow = Assert.Single(reopened!.Result.Rows, row => row.Domain == "unchanged.example");
        Assert.Equal(BulkOfferPricePreviewOutcomes.NoChanges, unchangedRow.Outcome);
        Assert.Equal(new[] { WebmasterOfferPriceType.Casino }, unchangedRow.UnsubmittedPriceTypes);
        Assert.All(legacy!.Result.Rows, row => Assert.Empty(row.UnsubmittedPriceTypes));
    }

    [PostgresFact]
    public async Task Apply_PatchesSelectedQuotes_CorrectsAllTerms_ReactivatesWithoutChangingCatalog_AndSavesImmutableResult()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        var now = DateTime.UtcNow;
        var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 20, 1, now);
        offer.Status = SiteWebmasterOfferStatus.Inactive;
        offer.Site.IsQuarantined = true;
        offer.Site.QuarantineReason = "keep quarantine";
        offer.Site.PriceUsd = 999;
        offer.TermRawText = "imported raw term";
        offer.CommentText = "keep comment";
        offer.Prices.Single().WebmasterPriceDetails = "keep payment details";
        var unselected = Price(WebmasterOfferPriceType.Casino, 40, "CAD");
        offer.Prices.Add(unselected);
        context.WebmasterOfferPrices.Add(unselected);
        OfferCurrencyTestData.AddSnapshot(context, .8m, now);
        await context.SaveChangesAsync();
        await context.Entry(offer).ReloadAsync();
        await context.Entry(offer.Site).ReloadAsync();
        var beforeSite = await context.Database.SqlQuery<string>($"SELECT to_jsonb(s)::text AS \"Value\" FROM \"Sites\" s WHERE \"Domain\" = {offer.SiteDomain}").SingleAsync();
        var oldVersion = offer.Version;
        var created = offer.CreatedAtUtc;
        var fingerprint = offer.ImportFingerprint;
        context.ChangeTracker.Clear();
        var input = Input("100", "CAD", TermType.Finite, 1);
        var preview = await Preview(context).PreviewAsync(input);
        var request = new BulkOfferPriceApplyRequest(Guid.NewGuid(), input, preview.PreviewVersion);

        // Act
        var result = await Service(context).ApplyAsync(request, "editor@example.com");
        await using var fresh = _database.CreateContext();
        var reopened = await Service(fresh).GetAsync(result.Id);
        var retry = await Service(fresh).ApplyAsync(request, "editor@example.com");
        var saved = await fresh.SiteWebmasterOffers.Include(value => value.Prices).Include(value => value.Site).SingleAsync();
        var history = await fresh.EntityChangeHistories.SingleAsync();
        var snapshot = ReadSnapshot(await fresh.BulkOfferPriceOperations.SingleAsync());
        using var calculation = new MemoryStream((await Service(fresh).DownloadAsync(result.Id, true))!);

        // Assert
        Assert.Equal(result.Id, reopened!.Id);
        Assert.Equal(JsonSerializer.Serialize(reopened), JsonSerializer.Serialize(retry));
        Assert.Equal(85m, saved.Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Main).WebmasterPriceUsd);
        Assert.Equal(5m, saved.Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Main).AppliedUsdSurcharge);
        Assert.Equal("keep payment details", saved.Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Main).WebmasterPriceDetails);
        var casino = saved.Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Casino);
        Assert.Equal(40m, casino.WebmasterPriceUsd);
        Assert.Equal("CAD", casino.SourceCurrency);
        Assert.Equal(5m, casino.AppliedUsdSurcharge);
        Assert.Equal("preserve details", casino.WebmasterPriceDetails);
        Assert.All(saved.Prices, price => Assert.Equal(1, price.TermValue));
        Assert.Equal(SiteWebmasterOfferStatus.Active, saved.Status);
        Assert.NotEqual(oldVersion, saved.Version);
        Assert.Equal(created, saved.CreatedAtUtc);
        Assert.Equal(fingerprint, saved.ImportFingerprint);
        Assert.Equal("imported raw term", saved.TermRawText);
        Assert.Equal("keep comment", saved.CommentText);
        Assert.True(saved.Site.IsQuarantined);
        Assert.Equal("keep quarantine", saved.Site.QuarantineReason);
        Assert.Equal(999m, saved.Site.PriceUsd);
        Assert.Equal("Bulk price update", history.Source);
        Assert.Contains(request.OperationId.ToString("D"), history.ChangesJson);
        Assert.Contains("applied USD surcharge", history.ChangesJson);
        Assert.Equal(4, snapshot.Minima.Count);
        Assert.True(Assert.Single(preview.Rows).CatalogRecalculationNeeded);
        Assert.True(Assert.Single(reopened.Result.Rows).CatalogRecalculationNeeded);
        Assert.Equal(JsonSerializer.Serialize(preview.Rows[0].PurchaseMinima), JsonSerializer.Serialize(reopened.Result.Rows[0].PurchaseMinima));
        var calculationRow = Assert.Single(XlsxTestWorkbook.ReadRows(calculation, "Webmaster costs"));
        Assert.Equal("85", calculationRow["Main cost USD"]);
        Assert.Equal(40m, decimal.Parse(calculationRow["Casino cost USD"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(calculationRow.ContainsKey("Quarantined"));
        Assert.Contains(snapshot.Minima, key => key.Service == PriceType.Casino && key.Term == PricingTerm.FiniteYears(1) && key.BeforeUsd == null && key.AfterUsd == 40);
        Assert.Equal(2, Assert.Single(snapshot.Offers).Before.Count);
        Assert.Equal(2, Assert.Single(snapshot.Offers).After.Count);
        Assert.Equal(beforeSite, await fresh.Database.SqlQuery<string>($"SELECT to_jsonb(s)::text AS \"Value\" FROM \"Sites\" s WHERE \"Domain\" = {offer.SiteDomain}").SingleAsync());
        Assert.Single(await fresh.EntityChangeHistories.ToListAsync());
        Assert.Single(await fresh.BulkOfferPriceOperations.ToListAsync());
        Assert.Single(await Service(fresh).RecentAsync());
        var repeat = await Preview(fresh).PreviewAsync(input);
        Assert.Equal(1, repeat.NoChangesCount);
        Assert.Equal(0, repeat.ReadyCount);
        Assert.Equal(reopened.Result.Rows[0].Prices[0].Proposed, repeat.Rows[0].Prices[0].Current);
        Assert.Equal(new[] { WebmasterOfferPriceType.Casino }, Assert.Single(reopened.Result.Rows).UnsubmittedPriceTypes);
        casino.AvailabilityStatus = ServiceAvailabilityStatus.Unknown;
        casino.WebmasterPriceUsd = null;
        casino.SourceAmount = null;
        casino.AppliedUsdRate = null;
        await fresh.SaveChangesAsync();
        var reopenedAfterEdit = await Service(fresh).GetAsync(result.Id);
        Assert.Equal(new[] { WebmasterOfferPriceType.Casino }, Assert.Single(reopenedAfterEdit!.Result.Rows).UnsubmittedPriceTypes);
    }

    [PostgresFact]
    public async Task Apply_YesNoOnlyPreservesInactiveStatus_ClearsProvenance_AndCreatesMissingPrice()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "CAD", 25, .8m, DateTime.UtcNow);
        offer.Status = SiteWebmasterOfferStatus.Inactive;
        var optional = Price(WebmasterOfferPriceType.Casino, 40, "CAD");
        offer.Prices.Add(optional);
        context.WebmasterOfferPrices.Add(optional);
        await context.SaveChangesAsync();
        var input = Input("NO") with { PriceTypes = [WebmasterOfferPriceType.Casino, WebmasterOfferPriceType.Crypto],
            Rows = [new(1, offer.SiteDomain, ["NO", "YES"])] };
        var preview = await Preview(context).PreviewAsync(input);
        context.ChangeTracker.Clear();

        // Act
        await Service(context).ApplyAsync(new(Guid.NewGuid(), input, preview.PreviewVersion), "editor@example.com");

        // Assert
        var saved = await context.SiteWebmasterOffers.Include(value => value.Prices).SingleAsync();
        Assert.Equal(SiteWebmasterOfferStatus.Inactive, saved.Status);
        var casino = saved.Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Casino);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, casino.AvailabilityStatus);
        Assert.Null(casino.SourceAmount);
        Assert.Null(casino.AppliedUsdRate);
        Assert.Null(casino.WebmasterPriceUsd);
        Assert.Null(casino.RateDate);
        Assert.Equal(0, casino.AppliedUsdSurcharge);
        Assert.Equal("preserve details", casino.WebmasterPriceDetails);
        Assert.Equal(ServiceAvailabilityStatus.AvailableWithUnknownPrice,
            saved.Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Crypto).AvailabilityStatus);
        Assert.Equal(20, saved.Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Main).WebmasterPriceUsd);
    }

    [PostgresTheory]
    [InlineData("version")]
    [InlineData("contact")]
    [InlineData("duplicate")]
    [InlineData("quarantine")]
    [InlineData("currency")]
    public async Task Apply_StalePreviewAbortsEntireBatch(string change)
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 20, 1, DateTime.UtcNow);
        var second = AddOffer(context, offer.Site, "second@example.com", 30);
        second.Webmaster.PrimaryEmail = "contact@example.com";
        second.Site = new Site { Domain = "second.example", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        second.SiteDomain = second.Site.Domain;
        OfferCurrencyTestData.AddSnapshot(context, .8m, DateTime.UtcNow);
        await context.SaveChangesAsync();
        var input = Input("100", "CAD") with { Rows = [new(1, offer.SiteDomain, ["100"]), new(2, second.SiteDomain, ["120"])] };
        var preview = await Preview(context).PreviewAsync(input);
        await using (var concurrent = _database.CreateContext())
        {
            var current = await concurrent.SiteWebmasterOffers.Include(value => value.Webmaster).Include(value => value.Site)
                .SingleAsync(value => value.Id == offer.Id);
            switch (change)
            {
                case "version": current.CommentText = "concurrent edit"; break;
                case "contact": current.Webmaster.PrimaryEmail = "changed@example.com"; break;
                case "duplicate": AddOffer(concurrent, current.Site, "contact@example.com", 10); break;
                case "quarantine": current.Site.IsQuarantined = true; break;
                case "currency": OfferCurrencyTestData.AddSnapshot(concurrent, .9m, DateTime.UtcNow); break;
            }
            await concurrent.SaveChangesAsync();
        }
        context.ChangeTracker.Clear();

        // Act
        await Assert.ThrowsAsync<BulkOfferPriceConflictException>(() => Service(context)
            .ApplyAsync(new(Guid.NewGuid(), input, preview.PreviewVersion), "editor@example.com"));

        // Assert
        Assert.Empty(await context.BulkOfferPriceOperations.ToListAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        Assert.Equal(20, await context.WebmasterOfferPrices.Where(price => price.SiteWebmasterOfferId == offer.Id)
            .Select(price => price.WebmasterPriceUsd).SingleAsync());
        Assert.Equal(30, await context.WebmasterOfferPrices.Where(price => price.SiteWebmasterOfferId == second.Id)
            .Select(price => price.WebmasterPriceUsd).SingleAsync());
    }

    [PostgresFact]
    public async Task Apply_SnapshotsCompetingExactTermMinima_UnselectedTypesAndNullTransitions()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 100, 1, DateTime.UtcNow);
        var insertionPrice = Price(WebmasterOfferPriceType.LinkInsertion18Plus, 50);
        var bannerPrice = Price(WebmasterOfferPriceType.Banner, 10);
        offer.Prices.Add(insertionPrice);
        offer.Prices.Add(bannerPrice);
        context.WebmasterOfferPrices.AddRange(insertionPrice, bannerPrice);
        AddOffer(context, offer.Site, "competitor@example.com", 70);
        var otherTerm = AddOffer(context, offer.Site, "other-term@example.com", 1);
        otherTerm.Prices.Single().TermType = TermType.Permanent;
        var malformed = AddOffer(context, offer.Site, "malformed@example.com", 2);
        malformed.Prices.Single().TermValue = 2;
        await context.SaveChangesAsync();
        var input = Input("120") with { PriceTypes = [WebmasterOfferPriceType.Main, WebmasterOfferPriceType.LinkInsertion18Plus],
            Rows = [new(1, offer.SiteDomain, ["120", "NO"])] };
        var preview = await Preview(context).PreviewAsync(input);
        context.ChangeTracker.Clear();

        // Act
        var operation = await Service(context).ApplyAsync(new(Guid.NewGuid(), input, preview.PreviewVersion), "editor@example.com");
        var snapshot = ReadSnapshot(await context.BulkOfferPriceOperations.SingleAsync());
        using var calculation = new MemoryStream((await Service(context).DownloadAsync(operation.Id, true))!);
        var reportBefore = await Service(context).DownloadAsync(operation.Id, false);
        var resultBefore = JsonSerializer.Serialize(await Service(context).GetAsync(operation.Id));
        await using var later = _database.CreateContext();
        await later.WebmasterOfferPrices.Where(price => price.AvailabilityStatus == ServiceAvailabilityStatus.Available)
            .ExecuteUpdateAsync(update => update.SetProperty(price => price.WebmasterPriceUsd, 999m).SetProperty(price => price.SourceAmount, 999m));
        await later.Sites.ExecuteUpdateAsync(update => update.SetProperty(site => site.IsQuarantined, true));
        using var historical = new MemoryStream((await Service(later).DownloadAsync(operation.Id, true))!);
        using var oldUpdate = new MemoryStream(reportBefore!);
        using var laterUpdate = new MemoryStream((await Service(later).DownloadAsync(operation.Id, false))!);

        // Assert
        Assert.Equal(2, snapshot.Minima.Count);
        var main = Assert.Single(snapshot.Minima, key => key.Service == PriceType.Main);
        Assert.Equal(70, main.BeforeUsd);
        Assert.Equal(70, main.AfterUsd);
        var insertion = Assert.Single(snapshot.Minima, key => key.Service == PriceType.LinkInsertionCasino);
        Assert.Equal(50, insertion.BeforeUsd);
        Assert.Null(insertion.AfterUsd);
        Assert.True(preview.Rows[0].CatalogRecalculationNeeded);
        Assert.Equal(JsonSerializer.Serialize(snapshot.Minima), JsonSerializer.Serialize(preview.Rows[0].PurchaseMinima));
        Assert.Equal(new[] { "Needs review" }, XlsxTestWorkbook.GetSheetNames(calculation));
        var missing = Assert.Single(XlsxTestWorkbook.ReadRows(calculation, "Needs review"));
        Assert.Equal("Link insertion casino", missing["Service"]);
        Assert.Equal("No webmaster price is available for this service and term. Review the catalog price manually.", missing["Reason"]);
        foreach (var sheet in XlsxTestWorkbook.GetSheetNames(calculation))
        {
            Assert.Equal(JsonSerializer.Serialize(XlsxTestWorkbook.ReadRows(calculation, sheet)), JsonSerializer.Serialize(XlsxTestWorkbook.ReadRows(historical, sheet)));
        }

        foreach (var sheet in XlsxTestWorkbook.GetSheetNames(oldUpdate))
        {
            Assert.Equal(JsonSerializer.Serialize(XlsxTestWorkbook.ReadRows(oldUpdate, sheet)), JsonSerializer.Serialize(XlsxTestWorkbook.ReadRows(laterUpdate, sheet)));
        }

        Assert.Equal(resultBefore, JsonSerializer.Serialize(await Service(later).GetAsync(operation.Id)));
    }

    [PostgresFact]
    public async Task Apply_ConcurrentRetriesCommitOnce_AndCannotReuseIdentityForDifferentInput()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 20, 1, DateTime.UtcNow);
        var input = Input("30");
        var preview = await Preview(context).PreviewAsync(input);
        var request = new BulkOfferPriceApplyRequest(Guid.NewGuid(), input, preview.PreviewVersion);
        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();

        // Act
        var results = await Task.WhenAll(Service(first).ApplyAsync(request, "one@example.com"), Service(second).ApplyAsync(request, "two@example.com"));

        // Assert
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(results[0].Actor, results[1].Actor);
        Assert.Single(await context.BulkOfferPriceOperations.ToListAsync());
        Assert.Single(await context.EntityChangeHistories.ToListAsync());
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(context).ApplyAsync(request with { Input = Input("40") }, "one@example.com"));
    }

    [PostgresFact]
    public async Task Apply_UnchangedForeignQuoteRetainsHistoricalConversion_NoOpsAndExceptionsAreNeverWritten()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "CAD", 25, .8m, DateTime.UtcNow);
        offer.Status = SiteWebmasterOfferStatus.Inactive;
        var noOp = AddOffer(context, new Site { Domain = "noop.example", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }, "other technical record", 20);
        noOp.Webmaster.PrimaryEmail = "contact@example.com";
        noOp.Prices.Single().SourceCurrency = "CAD";
        noOp.Prices.Single().SourceAmount = 25;
        noOp.Prices.Single().AppliedUsdRate = .8m;
        OfferCurrencyTestData.AddSnapshot(context, .9m, DateTime.UtcNow);
        await context.SaveChangesAsync();
        var noOpVersion = noOp.Version;
        var noOpTimestamp = await context.SiteWebmasterOffers.Where(value => value.Id == noOp.Id).Select(value => value.UpdatedAtUtc).SingleAsync();
        var input = Input("25", "CAD") with { Rows = [new(1, offer.SiteDomain, ["25"]), new(2, noOp.SiteDomain, ["25"]), new(3, "absent.example", ["100"])] };
        var preview = await Preview(context).PreviewAsync(input);
        context.ChangeTracker.Clear();

        // Act
        var result = await Service(context).ApplyAsync(new(Guid.NewGuid(), input, preview.PreviewVersion), "editor@example.com");
        await using var fresh = _database.CreateContext();
        var savedNoOp = await fresh.SiteWebmasterOffers.SingleAsync(value => value.Id == noOp.Id);
        var price = await fresh.WebmasterOfferPrices.SingleAsync(value => value.SiteWebmasterOfferId == offer.Id);

        // Assert
        Assert.Equal(1, result.Result.ReadyCount);
        Assert.Equal(1, result.Result.NoChangesCount);
        Assert.Equal(1, result.Result.ExceptionCount);
        var recent = Assert.Single(await Service(fresh).RecentAsync());
        Assert.Equal(1, recent.UpdatedCount);
        Assert.Equal(1, recent.NoChangesCount);
        Assert.Equal(1, recent.ExceptionCount);
        Assert.Null(result.Result.ConversionReference);
        Assert.Equal(20, price.WebmasterPriceUsd);
        Assert.Equal(.8m, price.AppliedUsdRate);
        Assert.Equal(0, price.AppliedUsdSurcharge);
        Assert.Equal(noOpVersion, savedNoOp.Version);
        Assert.Equal(noOpTimestamp, savedNoOp.UpdatedAtUtc);
        Assert.Equal(offer.Id.ToString("D"), (await fresh.EntityChangeHistories.SingleAsync()).EntityId);
        var before = JsonSerializer.Serialize(await Service(fresh).GetAsync(result.Id));
        OfferCurrencyTestData.AddSnapshot(fresh, 1.2m, DateTime.UtcNow);
        await fresh.SaveChangesAsync();
        Assert.Equal(before, JsonSerializer.Serialize(await Service(fresh).GetAsync(result.Id)));
    }

    [PostgresFact]
    public async Task Apply_SaveFailureRollsBackPricesHistoryAndOperation()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 20, 1, DateTime.UtcNow);
        var input = Input("30");
        var preview = await Preview(context).PreviewAsync(input);
        (decimal? Price, uint Version, int History, int Operations) written = default;
        var failure = new FailAfterSave(async saved =>
        {
            written = (await saved.WebmasterOfferPrices.Select(price => price.WebmasterPriceUsd).SingleAsync(),
                await saved.SiteWebmasterOffers.Select(value => value.Version).SingleAsync(),
                await saved.EntityChangeHistories.CountAsync(), await saved.BulkOfferPriceOperations.CountAsync());
        });
        await using var applying = _database.CreateContext(failure);

        // Act
        await Assert.ThrowsAsync<InjectedSaveFailure>(() => Service(applying)
            .ApplyAsync(new(Guid.NewGuid(), input, preview.PreviewVersion), "editor@example.com"));

        // Assert
        // All writes reached the database within the transaction before the injected failure.
        Assert.Equal(30, written.Price);
        Assert.NotEqual(offer.Version, written.Version);
        Assert.Equal(1, written.History);
        Assert.Equal(1, written.Operations);
        Assert.Equal(20, await context.WebmasterOfferPrices.Select(price => price.WebmasterPriceUsd).SingleAsync());
        Assert.Equal(offer.Version, await context.SiteWebmasterOffers.Select(value => value.Version).SingleAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        Assert.Empty(await context.BulkOfferPriceOperations.ToListAsync());
    }

    [PostgresFact]
    public async Task Apply_LocksMembershipCandidateInsertionPricesAndQuarantineUntilCommit()
    {
        // Arrange
        await using var context = await _database.ResetAsync();
        var offer = await OfferCurrencyTestData.SeedForeignOfferAsync(context, "USD", 20, 1, DateTime.UtcNow);
        var input = Input("30");
        var preview = await Preview(context).PreviewAsync(input);
        var gate = new SaveGate();
        await using var applying = _database.CreateContext(gate);
        var apply = Service(applying).ApplyAsync(new(Guid.NewGuid(), input, preview.PreviewVersion), "editor@example.com");
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var attempts = new[]
        {
            $"UPDATE \"Sites\" SET \"IsQuarantined\" = true WHERE \"Domain\" = '{offer.SiteDomain}'",
            $"UPDATE \"Webmasters\" SET \"PrimaryEmail\" = 'changed@example.com' WHERE \"Id\" = '{offer.WebmasterId}'",
            $"UPDATE \"WebmasterOfferPrices\" SET \"WebmasterPriceDetails\" = 'concurrent' WHERE \"SiteWebmasterOfferId\" = '{offer.Id}'",
            $"INSERT INTO \"SiteWebmasterOffers\" (\"Id\", \"SiteDomain\", \"WebmasterId\", \"ImportFingerprint\", \"ContactRawText\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES ('{Guid.NewGuid()}', '{offer.SiteDomain}', '{offer.WebmasterId}', '{new string('c', 64)}', 'contact', now(), now())"
        };

        // Act
        try
        {
            foreach (var sql in attempts)
            {
                await using var concurrent = _database.CreateContext();
                await concurrent.Database.OpenConnectionAsync();
                await concurrent.Database.ExecuteSqlRawAsync("SET lock_timeout = '150ms'");
                var error = await Assert.ThrowsAsync<PostgresException>(() => concurrent.Database.ExecuteSqlRawAsync(sql));
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
            }
        }
        finally { gate.Release.SetResult(); }
        await apply;

        // Assert
        Assert.Single(await context.BulkOfferPriceOperations.ToListAsync());
        Assert.Single(await context.EntityChangeHistories.ToListAsync());
        Assert.Equal(30, await context.WebmasterOfferPrices.Select(price => price.WebmasterPriceUsd).SingleAsync());
    }

    private static BulkOfferPricePreviewService Preview(ApplicationDbContext context) => new(context, new OfferCurrencyService(context));
    private static BulkOfferPriceOperationService Service(ApplicationDbContext context) => new(context, Preview(context));
    private static BulkOfferPricePreviewRequest Input(string amount, string currency = "USD", TermType? term = null, int? years = null)
        => new("contact@example.com", term, years, years.HasValue ? TermUnit.Year : null, currency,
            [WebmasterOfferPriceType.Main], [new(1, "currency.example", [amount])]);
    private static WebmasterOfferPrice Price(WebmasterOfferPriceType type, decimal amount, string currency = "USD")
        => new() { Id = Guid.NewGuid(), PriceType = type, AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = amount, SourceCurrency = currency, SourceAmount = amount, AppliedUsdRate = 1,
            AppliedUsdSurcharge = currency == "USD" ? 0 : 5, WebmasterPriceDetails = "preserve details",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1), UpdatedAtUtc = DateTime.UtcNow.AddDays(-1) };
    private static SiteWebmasterOffer AddOffer(ApplicationDbContext context, Site site, string contact, decimal amount)
    {
        var offer = new SiteWebmasterOffer { Id = Guid.NewGuid(), Site = site, SiteDomain = site.Domain,
            Webmaster = new Webmaster { Id = Guid.NewGuid(), PrimaryEmail = contact, ContactRawText = Guid.NewGuid().ToString(),
                NormalizedContactRawText = Guid.NewGuid().ToString(), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            ImportFingerprint = Guid.NewGuid().ToString("N"), ContactRawText = "original contact", CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow, Prices = [Price(WebmasterOfferPriceType.Main, amount)] };
        context.SiteWebmasterOffers.Add(offer);
        return offer;
    }
    private static BulkOfferPriceOperationSnapshot ReadSnapshot(BulkOfferPriceOperation operation)
        => JsonSerializer.Deserialize<BulkOfferPriceOperationSnapshot>(operation.SnapshotJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private sealed class InjectedSaveFailure : Exception { }

    private sealed class FailAfterSave(Func<ApplicationDbContext, Task> inspectWrites) : SaveChangesInterceptor
    {
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            await inspectWrites((ApplicationDbContext)eventData.Context!);
            throw new InjectedSaveFailure();
        }
    }

    private sealed class SaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute() { if (!BulkOfferOperationDatabase.Enabled) Skip = "Set REDHEAD_TEST_POSTGRES to run isolated PostgreSQL tests."; }
    }
    private sealed class PostgresTheoryAttribute : TheoryAttribute
    {
        public PostgresTheoryAttribute() { if (!BulkOfferOperationDatabase.Enabled) Skip = "Set REDHEAD_TEST_POSTGRES to run isolated PostgreSQL tests."; }
    }
}

public sealed class BulkOfferOperationDatabase : IAsyncLifetime
{
    public static bool Enabled => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES"));
    private string _connection = "";
    public async Task InitializeAsync()
    {
        if (!Enabled) return;
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REDHEAD_TEST_POSTGRES")) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        builder.Database = "redhead_bulk_apply_test_" + Guid.NewGuid().ToString("N");
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{builder.Database}\"", admin);
        await create.ExecuteNonQueryAsync();
        _connection = builder.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }
    public ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connection).AddInterceptors(interceptors).Options);
    public async Task<ApplicationDbContext> ResetAsync()
    {
        var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE \"Sites\", \"Webmasters\", \"EntityChangeHistories\", \"BulkOfferPriceOperations\", \"CurrencyReferenceSnapshots\" CASCADE");
        return context;
    }
    public async Task DisposeAsync()
    {
        if (!Enabled) return;
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
