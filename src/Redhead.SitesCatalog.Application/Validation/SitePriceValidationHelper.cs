using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Validation;

public readonly record struct SitePriceState(
    decimal? PriceUsd,
    decimal? PriceCasino,
    ServiceAvailabilityStatus PriceCasinoStatus,
    decimal? PriceCrypto,
    ServiceAvailabilityStatus PriceCryptoStatus,
    decimal? PriceLinkInsert,
    ServiceAvailabilityStatus PriceLinkInsertStatus,
    decimal? PriceLinkInsertCasino,
    ServiceAvailabilityStatus PriceLinkInsertCasinoStatus,
    decimal? PriceDating,
    ServiceAvailabilityStatus PriceDatingStatus);

public static class SitePriceValidationHelper
{
    public static bool HasAnyNumericPrice(SitePriceState prices)
    {
        return HasPositivePrice(prices.PriceUsd)
               || HasAvailableServicePrice(prices.PriceCasino, prices.PriceCasinoStatus)
               || HasAvailableServicePrice(prices.PriceCrypto, prices.PriceCryptoStatus)
               || HasAvailableServicePrice(prices.PriceLinkInsert, prices.PriceLinkInsertStatus)
               || HasAvailableServicePrice(prices.PriceLinkInsertCasino, prices.PriceLinkInsertCasinoStatus)
               || HasAvailableServicePrice(prices.PriceDating, prices.PriceDatingStatus);
    }

    private static bool HasPositivePrice(decimal? price)
        => price.HasValue && price.Value > 0;

    private static bool HasAvailableServicePrice(decimal? price, ServiceAvailabilityStatus status)
        => status == ServiceAvailabilityStatus.Available && price.HasValue && price.Value >= 0;
}
