using System.Text.Json;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

internal static class OfferCurrencyTestData
{
    internal static CurrencyReferenceSnapshot AddSnapshot(ApplicationDbContext context, decimal cadRate, DateTime now)
    {
        var snapshot = new CurrencyReferenceSnapshot
        {
            Id = Guid.NewGuid(), ProviderAsOfUtc = now,
            FetchedAtUtc = now,
            UsdRatesJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["CAD"] = cadRate, ["EUR"] = 999m, ["GBP"] = 999m })
        };
        context.CurrencyReferenceSnapshots.RemoveRange(context.CurrencyReferenceSnapshots);
        context.CurrencyReferenceSnapshots.Add(snapshot);
        return snapshot;
    }

    internal static async Task<SiteWebmasterOffer> SeedForeignOfferAsync(
        ApplicationDbContext context, string currency, decimal amount, decimal rate, DateTime now)
    {
        var old = now.AddDays(-10);
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = "currency.example", ContactRawText = "contact@example.com", ImportFingerprint = new string('a', 64),
            CreatedAtUtc = old, UpdatedAtUtc = old, Status = SiteWebmasterOfferStatus.Active,
            Site = new Site { Domain = "currency.example", CreatedAtUtc = old, UpdatedAtUtc = old },
            Webmaster = new Webmaster { Id = Guid.NewGuid(), ContactRawText = "contact@example.com", NormalizedContactRawText = "contact@example.com", PrimaryEmail = "contact@example.com", CreatedAtUtc = old, UpdatedAtUtc = old },
            Prices = [new WebmasterOfferPrice { Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main,
                AvailabilityStatus = ServiceAvailabilityStatus.Available, SourceCurrency = currency, SourceAmount = amount,
                // Historical quotes may retain the former upward-only rounding.
                WebmasterPriceUsd = decimal.Ceiling(amount * rate), AppliedUsdRate = rate,
                CreatedAtUtc = old, UpdatedAtUtc = old }]
        };
        context.SiteWebmasterOffers.Add(offer);
        await context.SaveChangesAsync();
        return offer;
    }
}
