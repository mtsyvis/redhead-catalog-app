using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain;

public static class CatalogPriceMapping
{
    public static WebmasterOfferPriceType ToOfferType(PriceType type) => type switch
    {
        PriceType.Main => WebmasterOfferPriceType.Main,
        PriceType.Casino => WebmasterOfferPriceType.Casino,
        PriceType.Crypto => WebmasterOfferPriceType.Crypto,
        PriceType.Dating => WebmasterOfferPriceType.Dating,
        PriceType.LinkInsertion => WebmasterOfferPriceType.LinkInsertion,
        PriceType.LinkInsertionCasino => WebmasterOfferPriceType.LinkInsertion18Plus,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
