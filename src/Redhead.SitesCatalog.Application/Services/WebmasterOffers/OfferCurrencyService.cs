using Redhead.SitesCatalog.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public sealed record OfferPriceConversion(
    string SourceCurrency,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal SourceAmount,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal WebmasterPriceUsd,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal AppliedUsdRate, string? RateReference,
    DateOnly? RateDate);

public sealed record OfferConversionRequest(string SourceCurrency, decimal SourceAmount, string? RateReference = null);

public sealed class OfferCurrencyService(ApplicationDbContext context)
{
    public const decimal FixedEurUsdRate = 1.25m;
    public const decimal FixedGbpUsdRate = 1.40m;
    public const string FixedEurReference = "fixed-eur-1.25-v1";
    public const string FixedGbpReference = "fixed-gbp-1.40-v1";

    public async Task<IReadOnlyList<string>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentSnapshotAsync(cancellationToken);
        Dictionary<string, decimal> rates = snapshot is null
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, decimal>>(snapshot.UsdRatesJson) ?? [];
        return new[] { CurrencyConversionConstants.DefaultCurrency, "EUR", "GBP" }.Concat(rates.Keys.Order(StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    private Task<CurrencyReferenceSnapshot?> GetCurrentSnapshotAsync(CancellationToken cancellationToken)
        => context.CurrencyReferenceSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public static bool IsValidAmount(decimal amount)
        => amount > 0 && amount <= WebmasterOfferFieldLimits.MaxPriceUsd && decimal.Round(amount, 2) == amount;

    public static decimal ConvertToUsd(decimal amount, string currency, decimal rate)
    {
        if (!IsValidAmount(amount))
        {
            throw new RequestValidationException("Price must be positive, within supported bounds and have at most 2 decimal places.");
        }

        if (rate <= 0)
        {
            throw new RequestValidationException("The USD reference rate must be positive.");
        }

        if (currency == CurrencyConversionConstants.DefaultCurrency)
        {
            return amount;
        }

        decimal converted;
        try
        {
            converted = amount * rate;
        }
        catch (OverflowException)
        {
            throw new RequestValidationException("Converted USD price is too large.");
        }

        var roundedToTenths = decimal.Round(converted, 1, MidpointRounding.AwayFromZero);
        var usd = decimal.Round(roundedToTenths, 0, MidpointRounding.AwayFromZero);

        if (usd <= 0)
        {
            throw new RequestValidationException("Converted USD price must be greater than 0 after rounding.");
        }

        if (usd > WebmasterOfferFieldLimits.MaxPriceUsd)
        {
            throw new RequestValidationException("Converted USD price is too large.");
        }
        return usd;
    }

    // Preview and save both use this method. Save requires the reference reviewed in preview.
    public async Task<OfferPriceConversion> ConvertAsync(OfferConversionRequest request, bool requireReference,
        CancellationToken cancellationToken = default)
    {
        var currency = request.SourceCurrency;
        if (!CurrencyCodeValidator.IsValid(currency))
        {
            throw new RequestValidationException("Currency must be an uppercase three-letter code.");
        }

        decimal rate;
        string? reference;
        DateOnly? rateDate = null;
        switch (currency)
        {
            case CurrencyConversionConstants.DefaultCurrency:
                rate = CurrencyConversionConstants.IdentityUsdRate;
                reference = null;
                break;
            case "EUR": rate = FixedEurUsdRate; reference = FixedEurReference; break;
            case "GBP": rate = FixedGbpUsdRate; reference = FixedGbpReference; break;
            default:
                var snapshot = await GetCurrentSnapshotAsync(cancellationToken);
                if (snapshot is null || !(JsonSerializer.Deserialize<Dictionary<string, decimal>>(snapshot.UsdRatesJson) ?? [])
                        .TryGetValue(currency, out rate))
                {
                    throw new RequestValidationException("A rate is not available for this currency. Enter the price in USD or try again later.");
                }

                reference = snapshot.Id.ToString("D");
                rateDate = DateOnly.FromDateTime(snapshot.ProviderAsOfUtc);
                break;
        }

        if (requireReference && currency != CurrencyConversionConstants.DefaultCurrency && request.RateReference != reference)
        {
            throw new RequestValidationException("Rates have changed. Refresh the conversion preview before saving.");
        }

        return new OfferPriceConversion(currency, request.SourceAmount,
            ConvertToUsd(request.SourceAmount, currency, rate), rate, reference, rateDate);
    }
}
