using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.Models.Pricing;
using Redhead.SitesCatalog.Application.Services.Pricing;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterOfferCurrencyUpdateTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _database = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private readonly ApplicationDbContext _context;
    private readonly TestClock _clock = new();

    public WebmasterOfferCurrencyUpdateTests() => _context = new ApplicationDbContext(_database);
    public void Dispose() => _context.Dispose();
    private OfferCurrencyService CreateCurrencies() => new(_context);
    private WebmasterOffersService CreateService() => new(_context, CreateCurrencies());

    [Fact]
    public async Task Save_RequiresFreshPreviewAfterRateUpdateAndPreservesOfferOnConflict()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.25m);
        AddSnapshot(0.75m);
        await _context.SaveChangesAsync();
        var preview = await CreateCurrencies().ConvertAsync(new OfferConversionRequest("CAD", 25m), false);
        var request = CreateRequest(offer);
        request.CommentText = "Draft notes";
        request.Prices[0].SourceCurrency = "CAD";
        request.Prices[0].RateReference = preview.RateReference;
        request.Prices[0].WebmasterPriceUsd = preview.WebmasterPriceUsd;
        AddSnapshot(0.9m);
        await _context.SaveChangesAsync();

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().UpdateAsync(offer.Id, request, null));

        // Assert
        Assert.Contains("Rates have changed", exception.Message);
        Assert.Contains("prices.0.sourceAmount", exception.FieldErrors!.Keys);
        Assert.Null(offer.CommentText);
        var saved = await _context.WebmasterOfferPrices.SingleAsync();
        Assert.Equal("EUR", saved.SourceCurrency);
        Assert.Equal(32m, saved.WebmasterPriceUsd);
        Assert.Equal(1.25m, saved.AppliedUsdRate);
        Assert.Empty(await _context.EntityChangeHistories.ToListAsync());
        Assert.Single(await _context.CurrencyReferenceSnapshots.ToListAsync());
    }

    [Fact]
    public async Task SavedQuote_KeepsActualProviderDateAfterCurrentTableChanges()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.25m);
        var first = AddSnapshot(0.8m);
        first.ProviderAsOfUtc = new DateTime(2026, 11, 11, 23, 30, 0, DateTimeKind.Utc);
        await _context.SaveChangesAsync();
        var preview = await CreateCurrencies().ConvertAsync(new OfferConversionRequest("CAD", 100m), false);
        var request = CreateRequest(offer);
        request.Prices[0].SourceCurrency = "CAD";
        request.Prices[0].SourceAmount = 100m;
        request.Prices[0].RateReference = preview.RateReference;
        request.Prices[0].WebmasterPriceUsd = preview.WebmasterPriceUsd;

        // Act
        await CreateService().UpdateAsync(offer.Id, request, null);
        AddSnapshot(0.9m);
        await _context.SaveChangesAsync();
        var read = await CreateService().GetForEditAsync(offer.Id);

        // Assert
        var price = Assert.Single(read!.Offer.Prices);
        Assert.Equal(new DateOnly(2026, 11, 11), preview.RateDate);
        Assert.Equal(preview.RateDate, price.RateDate);
        Assert.Equal(80m, price.WebmasterPriceUsd);
        Assert.Equal(0.8m, price.AppliedUsdRate);
        var json = JsonSerializer.Serialize(price, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"rateDate\":\"2026-11-11\"", json);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnchangedForeignQuote_PreservesHistoricFixedConversionAndNoOpAudit(bool omitClientUsd)
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.1m);
        var originalOfferTime = offer.UpdatedAtUtc;
        var originalPriceTime = offer.Prices.Single().UpdatedAtUtc;
        var originalWebmasterTime = offer.Webmaster.UpdatedAtUtc;
        var request = CreateRequest(offer);
        if (omitClientUsd)
        {
            request.Prices[0].WebmasterPriceUsd = null;
        }

        // Act
        await CreateService().UpdateAsync(offer.Id, request, "editor@example.com");
        _context.ChangeTracker.Clear();
        var saved = await _context.SiteWebmasterOffers.Include(x => x.Prices).Include(x => x.Webmaster).SingleAsync();

        // Assert
        Assert.Equal(originalOfferTime, saved.UpdatedAtUtc);
        Assert.Equal(originalPriceTime, saved.Prices.Single().UpdatedAtUtc);
        Assert.Equal(originalWebmasterTime, saved.Webmaster.UpdatedAtUtc);
        Assert.Equal(request.ExpectedVersion, saved.Version);
        Assert.Equal(request.ExpectedWebmasterVersion, saved.Webmaster.Version);
        Assert.Null(saved.UpdatedBy);
        var price = Assert.Single(saved.Prices);
        Assert.Equal(28m, price.WebmasterPriceUsd);
        Assert.Equal(1.1m, price.AppliedUsdRate);
        Assert.Equal(25m, price.SourceAmount);
        Assert.Empty(await _context.EntityChangeHistories.ToListAsync());
    }

    [Fact]
    public async Task SourceOnlyChange_IsAuditedEvenWhenUsdIsEqual_AndRoundTripsInAuthorizedRead()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25.20m, 1.25m);
        var request = CreateRequest(offer);
        request.Prices[0].SourceAmount = 25.21m;

        // Act
        await CreateService().UpdateAsync(offer.Id, request, "editor@example.com");
        var read = await CreateService().GetForEditAsync(offer.Id);
        var summary = await CreateService().GetSummaryByDomainAsync(offer.SiteDomain);

        // Assert
        var price = Assert.Single(read!.Offer.Prices);
        Assert.Equal(32m, price.WebmasterPriceUsd);
        Assert.Equal(25.21m, price.SourceAmount);
        Assert.Equal("EUR", price.SourceCurrency);
        Assert.Equal(1.25m, price.AppliedUsdRate);
        var priceJson = JsonSerializer.Serialize(price, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("rateSource", priceJson);
        Assert.DoesNotContain("rateReference", priceJson);
        Assert.DoesNotContain("rateAsOfUtc", priceJson);
        Assert.Contains("source amount", Assert.Single(await _context.EntityChangeHistories.ToListAsync()).ChangesJson);
        Assert.DoesNotContain("sourceAmount", JsonSerializer.Serialize(summary), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CurrencyChange_HistoryRecordsOnlyCurrencyAmountRateAndUsd()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("CAD", 25m, 0.75m);
        var request = CreateRequest(offer);
        request.Prices[0].SourceCurrency = "EUR";
        request.Prices[0].SourceAmount = 30m;
        request.Prices[0].RateReference = OfferCurrencyService.FixedEurReference;
        request.Prices[0].WebmasterPriceUsd = 38m;
        var service = CreateService();

        // Act
        await service.UpdateAsync(offer.Id, request, "editor@example.com");
        var history = await service.GetHistoryAsync(offer.Id);

        // Assert
        var changes = Assert.Single(history).Changes;
        Assert.Equal(
            new (string Field, string? OldValue, string? NewValue)[]
            {
                ("Price · Main · USD", "19", "38"),
                ("Price · Main · applied USD rate", "0.75", "1.25"),
                ("Price · Main · source amount", "25", "30"),
                ("Price · Main · source currency", "CAD", "EUR")
            },
            changes.Select(change => (change.Field, change.OldValue, change.NewValue)));
    }

    [Fact]
    public async Task ChangedSourceQuote_RejectsClientUsdCalculatedWithLegacyCeiling()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.25m);
        var request = CreateRequest(offer);
        request.Prices[0].SourceAmount = 25.01m;
        request.Prices[0].WebmasterPriceUsd = 32m;

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().UpdateAsync(offer.Id, request, null));

        // Assert
        Assert.Contains("prices.0.sourceAmount", exception.FieldErrors!.Keys);
        Assert.Equal(25m, offer.Prices.Single().SourceAmount);
        Assert.Equal(32m, offer.Prices.Single().WebmasterPriceUsd);
        Assert.Empty(_context.EntityChangeHistories);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForgedUsdOrMissingSourceFields_IsRejectedBeforeAnyMutation(bool omitSourceFields)
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.25m);
        var request = CreateRequest(offer);
        request.CommentText = "Must not persist";
        request.Prices[0].WebmasterPriceUsd = 1m;
        if (omitSourceFields)
        {
            request.Prices[0].SourceCurrency = string.Empty;
            request.Prices[0].SourceAmount = null;
            request.Prices[0].RateReference = null;
        }

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().UpdateAsync(offer.Id, request, null));

        // Assert
        Assert.Contains("prices.0.sourceAmount", exception.FieldErrors!.Keys);
        Assert.Null(offer.CommentText);
        Assert.Equal(32m, offer.Prices.Single().WebmasterPriceUsd);
        Assert.Empty(_context.EntityChangeHistories);
    }

    [Theory]
    [InlineData("EUR", "1.1", "28")]
    [InlineData("CAD", "0.75", "19")]
    public async Task UnrelatedSave_PreservesForeignQuoteWithoutAnyAvailableRate(string currency, string rateText, string expectedUsdText)
    {
        // Arrange
        var rate = decimal.Parse(rateText, System.Globalization.CultureInfo.InvariantCulture);
        var expectedUsd = decimal.Parse(expectedUsdText, System.Globalization.CultureInfo.InvariantCulture);
        var offer = await SeedForeignOfferAsync(currency, 25m, rate);
        var request = CreateRequest(offer);
        request.Prices[0].RateReference = null;
        request.Prices[0].WebmasterPriceDetails = "Payment instructions";
        request.CommentText = "Notes only";
        request.Status = SiteWebmasterOfferStatus.Inactive;

        // Act
        await CreateService().UpdateAsync(offer.Id, request, "editor@example.com");
        _context.ChangeTracker.Clear();
        var saved = await _context.SiteWebmasterOffers.Include(x => x.Prices).SingleAsync();

        // Assert
        var price = Assert.Single(saved.Prices);
        Assert.Equal(currency, price.SourceCurrency);
        Assert.Equal(25m, price.SourceAmount);
        Assert.Equal(rate, price.AppliedUsdRate);
        Assert.Equal(expectedUsd, price.WebmasterPriceUsd);
        Assert.Equal("Payment instructions", price.WebmasterPriceDetails);
        Assert.Equal("Notes only", saved.CommentText);
        Assert.Equal(SiteWebmasterOfferStatus.Inactive, saved.Status);
        var history = Assert.Single(await _context.EntityChangeHistories.ToListAsync());
        Assert.DoesNotContain("applied USD rate", history.ChangesJson);
    }

    [Fact]
    public async Task ReferenceChange_LeavesPersistedUsdPricingMarginsAndExportsUnchanged()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("CAD", 25m, 0.75m);
        offer.Site.PriceOptions.Add(new SitePriceOption
        {
            SiteDomain = offer.SiteDomain, PriceType = PriceType.Main,
            TermKey = "unknown", AmountUsd = 100m
        });
        AddSnapshot(9m);
        await _context.SaveChangesAsync();
        var pricing = new PricingService(_context, Options.Create(new LinkDetectiveOptions()), _clock);

        // Act
        var result = await pricing.SearchAsync(new PricingQuery(), CancellationToken.None);
        var export = await CreateService().GetExportDataAsync(new WebmasterOffersExportRequest { QueryText = offer.SiteDomain });
        using var workbook = WebmasterOffersExcelExportGenerator.Generate(export);
        var row = Assert.Single(XlsxTestWorkbook.ReadRows(workbook, "Webmaster Offers"));

        // Assert
        var price = Assert.Single(Assert.Single(result.Items).Prices, x => x.PriceType == PriceType.Main);
        Assert.Equal(19m, price.OfferPrice);
        Assert.Equal(81m, price.Margin);
        Assert.Equal(100m, price.CatalogPrice);
        Assert.Equal(19m, decimal.Parse(row["MainWebmasterPriceUsd"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.DoesNotContain("SourceCurrency", row.Keys);
        Assert.Equal(0.75m, offer.Prices.Single().AppliedUsdRate);
        Assert.Empty(_context.EntityChangeHistories);
    }

    [Fact]
    public async Task EditingAnotherService_PreservesForeignConversionAndImportedTerms()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.1m);
        var main = offer.Prices.Single();
        main.TermType = TermType.Permanent;
        await _context.SaveChangesAsync();
        var originalTime = main.UpdatedAtUtc;
        var request = CreateRequest(offer);
        request.Prices.Add(new UpdateWebmasterOfferPriceRequest
        {
            SourceCurrency = "USD",
            SourceAmount = 25.25m,
            PriceType = WebmasterOfferPriceType.Casino, AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = 25.25m
        });

        // Act
        await CreateService().UpdateAsync(offer.Id, request, null);

        // Assert
        Assert.Equal(28m, main.WebmasterPriceUsd);
        Assert.Equal(originalTime, main.UpdatedAtUtc);
        Assert.Equal(TermType.Permanent, main.TermType);
        var casino = Assert.Single(offer.Prices, x => x.PriceType == WebmasterOfferPriceType.Casino);
        Assert.Equal("USD", casino.SourceCurrency);
        Assert.Equal(25.25m, casino.SourceAmount);
        Assert.Equal(25.25m, casino.WebmasterPriceUsd);
    }

    [Theory]
    [InlineData(ServiceAvailabilityStatus.Unknown)]
    [InlineData(ServiceAvailabilityStatus.NotAvailable)]
    [InlineData(ServiceAvailabilityStatus.AvailableWithUnknownPrice)]
    public async Task NonNumericAvailability_ClearsConversionButKeepsDetails(ServiceAvailabilityStatus status)
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.25m);
        offer.Prices.Single().PriceType = WebmasterOfferPriceType.Casino;
        await _context.SaveChangesAsync();
        var request = CreateRequest(offer);
        var priceRequest = request.Prices.Single();
        priceRequest.AvailabilityStatus = status;
        priceRequest.SourceAmount = priceRequest.WebmasterPriceUsd = null;
        priceRequest.RateReference = null;
        priceRequest.WebmasterPriceDetails = "Keep payment notes";

        // Act
        await CreateService().UpdateAsync(offer.Id, request, null);

        // Assert
        var price = offer.Prices.Single();
        Assert.Null(price.SourceAmount);
        Assert.Null(price.WebmasterPriceUsd);
        Assert.Null(price.AppliedUsdRate);
        Assert.Equal("EUR", price.SourceCurrency);
        Assert.Equal("Keep payment notes", price.WebmasterPriceDetails);
    }

    [Fact]
    public async Task SourceAmountEdit_PreservesExistingServiceSpecificTerm()
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.25m);
        var price = offer.Prices.Single();
        price.TermType = TermType.Permanent;
        await _context.SaveChangesAsync();
        var request = CreateRequest(offer);
        request.Prices[0].SourceAmount = 30m;
        request.Prices[0].WebmasterPriceUsd = null;

        // Act
        await CreateService().UpdateAsync(offer.Id, request, null);

        // Assert
        Assert.Equal(38m, price.WebmasterPriceUsd);
        Assert.Equal(TermType.Permanent, price.TermType);
        Assert.Null(offer.TermType);
    }

    private CurrencyReferenceSnapshot AddSnapshot(decimal cadRate)
        => OfferCurrencyTestData.AddSnapshot(_context, cadRate, _clock.GetUtcNow().UtcDateTime);

    private Task<SiteWebmasterOffer> SeedForeignOfferAsync(string currency, decimal amount, decimal rate)
        => OfferCurrencyTestData.SeedForeignOfferAsync(_context, currency, amount, rate, _clock.GetUtcNow().UtcDateTime);

    private static UpdateWebmasterOfferRequest CreateRequest(SiteWebmasterOffer offer) => new()
    {
        ExpectedVersion = offer.Version, ExpectedWebmasterVersion = offer.Webmaster.Version, ContactRawText = offer.ContactRawText,
        PrimaryEmail = offer.Webmaster.PrimaryEmail, Status = offer.Status, CommentText = offer.CommentText,
        Prices = offer.Prices.Select(x => new UpdateWebmasterOfferPriceRequest { PriceType = x.PriceType, AvailabilityStatus = x.AvailabilityStatus,
            WebmasterPriceUsd = x.WebmasterPriceUsd, SourceCurrency = x.SourceCurrency, SourceAmount = x.SourceAmount,
            RateReference = x.SourceCurrency switch { "EUR" => OfferCurrencyService.FixedEurReference, "GBP" => OfferCurrencyService.FixedGbpReference, _ => null }, WebmasterPriceDetails = x.WebmasterPriceDetails }).ToList()
    };

    private sealed class TestClock : TimeProvider
    {
        private readonly DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
