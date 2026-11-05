using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

// Shared pricing writes for the full site editor and the Pricing workspace.
public static class SitePricingUpdater
{
    public static void Apply(ApplicationDbContext context, Site site, UpdateSitePricingRequest pricing, DateTime now)
    {
        ReplacePricingCollections(context, site, pricing, now);
        ApplyLegacyPricingCompatibility(site, pricing);
    }

    public static UpdateSitePricingRequest Snapshot(Site site) => new()
    {
        Prices = site.PriceOptions.Select(price => new UpdateSitePriceOptionRequest
        {
            PriceType = price.PriceType,
            TermKey = price.TermKey,
            TermType = price.TermType,
            TermValue = price.TermValue,
            TermUnit = price.TermUnit,
            AmountUsd = price.AmountUsd
        }).ToList(),
        ServiceAvailabilities = site.ServiceAvailabilities.Select(status => new UpdateSiteServiceAvailabilityRequest
        { ServiceType = status.ServiceType, Status = status.Status }).ToList()
    };

    public static string Version(UpdateSitePricingRequest pricing)
    {
        // Compare pricing values only. Unrelated site edits must never be overwritten.
        var canonical = JsonSerializer.Serialize(new
        {
            Prices = pricing.Prices.OrderBy(price => price.PriceType).ThenBy(price => price.TermKey, StringComparer.Ordinal)
                .Select(price => new
                {
                    price.PriceType,
                    price.TermKey,
                    price.TermType,
                    price.TermValue,
                    price.TermUnit,
                    Amount = price.AmountUsd.ToString("G29", CultureInfo.InvariantCulture)
                }),
            Statuses = Enum.GetValues<PriceType>().Where(type => type != PriceType.Main)
                .Select(type => new { Type = type, Status = GetServiceStatus(pricing, type) })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void ReplacePricingCollections(ApplicationDbContext context, Site site, UpdateSitePricingRequest pricing, DateTime now)
    {
        var existingPrices = site.PriceOptions.ToList();
        var existingAvailabilities = site.ServiceAvailabilities.ToList();

        if (existingPrices.Count > 0)
        {
            context.SitePriceOptions.RemoveRange(existingPrices);
        }

        if (existingAvailabilities.Count > 0)
        {
            context.SiteServiceAvailabilities.RemoveRange(existingAvailabilities);
        }

        site.PriceOptions.Clear();
        site.ServiceAvailabilities.Clear();

        foreach (var price in pricing.Prices)
        {
            site.PriceOptions.Add(new SitePriceOption
            {
                SiteDomain = site.Domain,
                Site = site,
                PriceType = price.PriceType,
                TermKey = price.TermKey,
                TermType = price.TermType,
                TermValue = price.TermValue,
                TermUnit = price.TermUnit,
                AmountUsd = price.AmountUsd,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        foreach (var availability in GetEffectiveServiceAvailabilities(pricing))
        {
            site.ServiceAvailabilities.Add(new SiteServiceAvailability
            {
                SiteDomain = site.Domain,
                Site = site,
                ServiceType = availability.ServiceType,
                Status = availability.Status,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
    }

    private static void ApplyLegacyPricingCompatibility(Site site, UpdateSitePricingRequest pricing)
    {
        site.PriceUsd = GetRepresentativePriceAmount(pricing.Prices, PriceType.Main);
        site.PriceCasino = GetRepresentativePriceAmount(pricing.Prices, PriceType.Casino);
        site.PriceCrypto = GetRepresentativePriceAmount(pricing.Prices, PriceType.Crypto);
        site.PriceLinkInsert = GetRepresentativePriceAmount(pricing.Prices, PriceType.LinkInsertion);
        site.PriceLinkInsertCasino = GetRepresentativePriceAmount(pricing.Prices, PriceType.LinkInsertionCasino);
        site.PriceDating = GetRepresentativePriceAmount(pricing.Prices, PriceType.Dating);

        site.PriceCasinoStatus = GetServiceStatus(pricing, PriceType.Casino);
        site.PriceCryptoStatus = GetServiceStatus(pricing, PriceType.Crypto);
        site.PriceLinkInsertStatus = GetServiceStatus(pricing, PriceType.LinkInsertion);
        site.PriceLinkInsertCasinoStatus = GetServiceStatus(pricing, PriceType.LinkInsertionCasino);
        site.PriceDatingStatus = GetServiceStatus(pricing, PriceType.Dating);
    }

    private static decimal? GetRepresentativePriceAmount(
        IReadOnlyList<UpdateSitePriceOptionRequest> prices,
        PriceType priceType)
        => GetRepresentativePrice(prices, priceType)?.AmountUsd;

    private static UpdateSitePriceOptionRequest? GetRepresentativePrice(
        IReadOnlyList<UpdateSitePriceOptionRequest> prices,
        PriceType priceType)
    {
        return prices
            .Where(price => price.PriceType == priceType)
            .OrderBy(price => price.AmountUsd)
            .ThenBy(GetTermSortOrder)
            .ThenBy(price => price.TermValue)
            .FirstOrDefault();
    }

    private static ServiceAvailabilityStatus GetServiceStatus(UpdateSitePricingRequest pricing, PriceType serviceType)
    {
        var explicitStatus = pricing.ServiceAvailabilities
            .FirstOrDefault(availability => availability.ServiceType == serviceType);

        if (explicitStatus is not null)
        {
            return explicitStatus.Status;
        }

        return pricing.Prices.Any(price => price.PriceType == serviceType)
            ? ServiceAvailabilityStatus.Available
            : ServiceAvailabilityStatus.Unknown;
    }

    private static IReadOnlyList<UpdateSiteServiceAvailabilityRequest> GetEffectiveServiceAvailabilities(UpdateSitePricingRequest pricing)
    {
        var availabilities = pricing.ServiceAvailabilities.ToList();
        foreach (var serviceType in new[]
                 {
                     PriceType.Casino,
                     PriceType.Crypto,
                     PriceType.LinkInsertion,
                     PriceType.LinkInsertionCasino,
                     PriceType.Dating
                 })
        {
            if (availabilities.Any(availability => availability.ServiceType == serviceType))
            {
                continue;
            }

            if (!pricing.Prices.Any(price => price.PriceType == serviceType))
            {
                continue;
            }

            availabilities.Add(new UpdateSiteServiceAvailabilityRequest
            {
                ServiceType = serviceType,
                Status = ServiceAvailabilityStatus.Available
            });
        }

        return availabilities;
    }

    private static int GetTermSortOrder(UpdateSitePriceOptionRequest priceOption)
        => priceOption.TermKey switch
        {
            PricingTerm.UnknownKey => 0,
            _ when priceOption.TermType == TermType.Finite => 1,
            PricingTerm.PermanentKey => 2,
            _ => 3
        };
}
