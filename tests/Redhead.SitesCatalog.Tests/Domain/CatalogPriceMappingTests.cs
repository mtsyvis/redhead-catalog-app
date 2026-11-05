using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Domain;

public sealed class CatalogPriceMappingTests
{
    [Theory]
    [InlineData(PriceType.Main, WebmasterOfferPriceType.Main)]
    [InlineData(PriceType.Casino, WebmasterOfferPriceType.Casino)]
    [InlineData(PriceType.Crypto, WebmasterOfferPriceType.Crypto)]
    [InlineData(PriceType.Dating, WebmasterOfferPriceType.Dating)]
    [InlineData(PriceType.LinkInsertion, WebmasterOfferPriceType.LinkInsertion)]
    [InlineData(PriceType.LinkInsertionCasino, WebmasterOfferPriceType.LinkInsertion18Plus)]
    public void ToOfferType_MapsCatalogServicesToMatchingOfferServices(PriceType catalog, WebmasterOfferPriceType expected)
    {
        // Arrange
        var type = catalog;

        // Act
        var actual = CatalogPriceMapping.ToOfferType(type);

        // Assert
        Assert.Equal(expected, actual);
    }
}
