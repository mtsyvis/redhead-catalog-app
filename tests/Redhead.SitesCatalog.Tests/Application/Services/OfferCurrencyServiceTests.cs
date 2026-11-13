using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class OfferCurrencyServiceTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _database = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private readonly ApplicationDbContext _context;
    private readonly TestClock _clock = new();

    public OfferCurrencyServiceTests() => _context = new ApplicationDbContext(_database);
    public void Dispose() => _context.Dispose();
    private OfferCurrencyService CreateCurrencies() => new(_context);

    [Fact]
    public async Task CurrencyList_UsesValidatedSnapshotWithoutManualConfiguration()
    {
        // Arrange
        var service = CreateCurrencies();
        var withoutRates = await service.GetCurrenciesAsync();
        var snapshot = AddSnapshot(0.75m);
        snapshot.UsdRatesJson = "{\"USD\":1,\"EUR\":1.2,\"GBP\":1.3,\"CAD\":0.75,\"JPY\":0.006}";
        await _context.SaveChangesAsync();

        // Act
        var withRates = await service.GetCurrenciesAsync();
        var replacement = AddSnapshot(0.9m);
        replacement.UsdRatesJson = "{\"USD\":1,\"JPY\":0.006}";
        await _context.SaveChangesAsync();
        var afterRefresh = await service.GetCurrenciesAsync();

        // Assert
        Assert.Equal(["USD", "EUR", "GBP"], withoutRates);
        Assert.Equal(["USD", "EUR", "GBP", "CAD", "JPY"], withRates);
        Assert.Equal(["USD", "EUR", "GBP", "JPY"], afterRefresh);
    }

    [Theory]
    [InlineData("USD", "25.25", "25.25")]
    [InlineData("EUR", "25", "31")]
    [InlineData("GBP", "36", "50")]
    [InlineData("EUR", "100", "125")]
    [InlineData("USD", "54.44", "54.44")]
    [InlineData("USD", "54.49", "54.49")]
    [InlineData("GBP", "2.50", "4")]
    public async Task Conversion_UsesThresholdRoundingOnlyForForeignQuotes(string currency, string amountText, string expectedText)
    {
        // Arrange
        var amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);
        var expected = decimal.Parse(expectedText, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var quote = await CreateCurrencies().ConvertAsync(new OfferConversionRequest(currency, amount), false);

        // Assert
        Assert.Equal(expected, quote.WebmasterPriceUsd);
        Assert.Equal(currency, quote.SourceCurrency);
    }

    [Theory]
    [InlineData("0", "1")]
    [InlineData("-1", "1")]
    [InlineData("0.001", "1.25")]
    [InlineData("1", "0")]
    [InlineData("1", "-1")]
    [InlineData("0.01", "1.25")]
    [InlineData("0.01", "0.0000000000000000000000000001")]
    [InlineData("9999999999999999.99", "1.25")]
    [InlineData("9999999999999999.99", "79228162514264337593543950335")]
    public void Conversion_RejectsInvalidInputAndOverflow(string amountText, string rateText)
    {
        // Arrange
        var amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);
        var rate = decimal.Parse(rateText, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var exception = Record.Exception(() => OfferCurrencyService.ConvertToUsd(amount, "CAD", rate));

        // Assert
        Assert.IsType<RequestValidationException>(exception);
    }

    [Fact]
    public void Conversion_DoesNotRoundIntermediateProductToCents()
    {
        // Arrange
        const decimal amount = 54.44m;
        const decimal rate = 1.0001m;

        // Act
        var result = OfferCurrencyService.ConvertToUsd(amount, "CAD", rate);

        // Assert
        Assert.Equal(54m, result);
    }

    [Theory]
    [InlineData("54", 54)]
    [InlineData("54.44", 54)]
    [InlineData("54.44999999999999999999999999", 54)]
    [InlineData("54.45", 55)]
    [InlineData("54.49", 55)]
    [InlineData("54.50", 55)]
    [InlineData("54.99", 55)]
    [InlineData("55.44", 55)]
    [InlineData("55.45", 56)]
    public void Conversion_RoundsToTenthsThenWholeUsdAwayFromZero(string unroundedUsd, int expected)
    {
        // Arrange
        var rate = decimal.Parse(unroundedUsd, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var result = OfferCurrencyService.ConvertToUsd(1m, "CAD", rate);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Conversion_FixedRatesOverrideProviderRates()
    {
        // Arrange
        var snapshot = AddSnapshot(0.75m);
        snapshot.UsdRatesJson = "{\"USD\":1,\"EUR\":999,\"GBP\":999}";
        await _context.SaveChangesAsync();
        var service = CreateCurrencies();

        // Act
        var eur = await service.ConvertAsync(new OfferConversionRequest("EUR", 25m, OfferCurrencyService.FixedEurReference), true);
        var gbp = await service.ConvertAsync(new OfferConversionRequest("GBP", 36m, OfferCurrencyService.FixedGbpReference), true);

        // Assert
        Assert.Equal(31m, eur.WebmasterPriceUsd);
        Assert.Equal(1.25m, eur.AppliedUsdRate);
        Assert.Equal(50m, gbp.WebmasterPriceUsd);
        Assert.Equal(1.40m, gbp.AppliedUsdRate);
    }

    [Theory]
    [InlineData("CAD", null)]
    [InlineData("eur", null)]
    [InlineData("EUR", "forged")]
    public async Task Conversion_RejectsMissingRatesUnsupportedCodesAndForgedReferences(string currency, string? reference)
    {
        // Arrange
        var service = CreateCurrencies();

        // Act
        var exception = await Record.ExceptionAsync(() => service.ConvertAsync(new OfferConversionRequest(currency, 10m, reference), true));

        // Assert
        Assert.IsType<RequestValidationException>(exception);
    }

    [Fact]
    public void CurrencyContract_PreservesExactDecimalsAcrossBrowserJson()
    {
        // Arrange
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var price = new WebmasterOfferPriceDto
        {
            SourceCurrency = "CAD", SourceAmount = WebmasterOfferFieldLimits.MaxPriceUsd,
            AppliedUsdRate = 0.1234567890123456789012345678m
        };

        // Act
        var response = JsonSerializer.Serialize(price, json);
        var request = JsonSerializer.Deserialize<UpdateWebmasterOfferPriceRequest>(
            "{\"sourceCurrency\":\"USD\",\"sourceAmount\":\"9999999999999999.99\"}", json);

        // Assert
        Assert.Contains("\"sourceAmount\":\"9999999999999999.99\"", response);
        Assert.Contains("\"appliedUsdRate\":\"0.1234567890123456789012345678\"", response);
        Assert.Equal(WebmasterOfferFieldLimits.MaxPriceUsd, request!.SourceAmount);
    }

    [Fact]
    public void PreviewContract_PreservesReviewedUsdExactlyInSaveRequest()
    {
        // Arrange
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var quote = new OfferPriceConversion("CAD", 9999999999999999m, 9999999999999999m,
            1m, "reference", new DateOnly(2026, 11, 13));

        // Act
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(quote, json));
        var request = JsonSerializer.Deserialize<UpdateWebmasterOfferPriceRequest>(
            "{\"sourceCurrency\":\"CAD\",\"sourceAmount\":\"9999999999999999\",\"webmasterPriceUsd\":\"9999999999999999\"}", json);
        var previewRequest = JsonSerializer.Deserialize<OfferConversionRequest>(
            "{\"sourceCurrency\":\"CAD\",\"sourceAmount\":\"9999999999999999\"}", json);

        // Assert
        Assert.Equal("9999999999999999", response.RootElement.GetProperty("sourceAmount").GetString());
        Assert.Equal(JsonValueKind.String, response.RootElement.GetProperty("webmasterPriceUsd").ValueKind);
        Assert.Equal("9999999999999999", response.RootElement.GetProperty("webmasterPriceUsd").GetString());
        Assert.False(response.RootElement.TryGetProperty("rateSource", out _));
        Assert.Equal(9999999999999999m, request!.WebmasterPriceUsd);
        Assert.Equal(9999999999999999m, previewRequest!.SourceAmount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Preview_ShowsProviderDateWithoutClassifyingRatesAsStale(int hoursToUpdate)
    {
        // Arrange
        var snapshot = AddSnapshot(0.8m);
        snapshot.ProviderAsOfUtc = _clock.GetUtcNow().UtcDateTime.AddHours(-12);
        snapshot.ProviderNextUpdateUtc = _clock.GetUtcNow().UtcDateTime.AddHours(hoursToUpdate);
        await _context.SaveChangesAsync();

        // Act
        var preview = await CreateCurrencies().ConvertAsync(new OfferConversionRequest("CAD", 25m), false);

        // Assert
        Assert.Equal("CAD", preview.SourceCurrency);
        Assert.Equal(DateOnly.FromDateTime(snapshot.ProviderAsOfUtc), preview.RateDate);
    }

    private CurrencyReferenceSnapshot AddSnapshot(decimal cadRate)
        => OfferCurrencyTestData.AddSnapshot(_context, cadRate, _clock.GetUtcNow().UtcDateTime);

    private sealed class TestClock : TimeProvider
    {
        private readonly DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
