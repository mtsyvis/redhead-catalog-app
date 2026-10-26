using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterPriceComparisonServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public WebmasterPriceComparisonServiceTests()
    {
        _context.Sites.Add(new Site { Domain = "example.com", PriceUsd = 999m });
        _context.SaveChanges();
    }

    [Theory]
    [InlineData(PriceType.Main, WebmasterOfferPriceType.Main)]
    [InlineData(PriceType.Casino, WebmasterOfferPriceType.Casino)]
    [InlineData(PriceType.Crypto, WebmasterOfferPriceType.Crypto)]
    [InlineData(PriceType.Dating, WebmasterOfferPriceType.Dating)]
    [InlineData(PriceType.LinkInsertion, WebmasterOfferPriceType.LinkInsertion)]
    [InlineData(PriceType.LinkInsertionCasino, WebmasterOfferPriceType.LinkInsertion18Plus)]
    public async Task GetByDomainAsync_MapsSupportedServicesAndCalculatesNoTermMargin(PriceType catalogType, WebmasterOfferPriceType offerType)
    {
        // Arrange
        AddCatalogPrice(catalogType, 600m, PricingTerm.Unknown);
        AddOffer(offerType, 400m, PricingTerm.Unknown);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("https://www.EXAMPLE.com/path");

        // Assert
        Assert.True(result.SiteFound);
        Assert.Equal("example.com", result.Domain);
        Assert.Equal(PricingTerm.UnknownKey, result.DefaultTermKey);
        var term = Assert.Single(result.Terms);
        Assert.Equal("No term", term.Label);
        Assert.Equal(1, term.ComparableOfferCount);
        Assert.Equal(600m, term.CatalogPrices.Single(price => price.PriceType == offerType).AmountUsd);
        var price = Assert.Single(result.Offers).Prices.Single(price => price.PriceType == offerType);
        Assert.Equal(200m, Assert.Single(price.Margins).AmountUsd);
        Assert.Null(Assert.Single(price.Margins).UnavailableReason);
    }

    [Theory]
    [InlineData(400, 200)]
    [InlineData(600, 0)]
    [InlineData(650, -50)]
    public async Task GetByDomainAsync_ReturnsPositiveZeroAndNegativeDifferences(decimal offerPrice, decimal margin)
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Permanent);
        AddOffer(WebmasterOfferPriceType.Main, offerPrice, PricingTerm.Permanent);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        var comparison = MainMargin(result, PricingTerm.PermanentKey);
        Assert.Equal(margin, comparison.AmountUsd);
        Assert.Null(comparison.UnavailableReason);
    }

    [Fact]
    public async Task GetByDomainAsync_UsesExactPriceTermAndDefaultsToMostComparableCatalogTerm()
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Permanent);
        AddCatalogPrice(PriceType.Main, 450m, PricingTerm.FiniteYears(1));
        AddCatalogPrice(PriceType.Main, 700m, PricingTerm.FiniteYears(2));
        var offer = AddOffer(WebmasterOfferPriceType.Main, 400m, PricingTerm.FiniteYears(1));
        offer.TermType = TermType.Permanent; // The offer badge must not replace a price's own term.
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        Assert.Equal(PricingTerm.FiniteYears(1).TermKey, result.DefaultTermKey);
        Assert.Equal(50m, MainMargin(result, PricingTerm.FiniteYears(1).TermKey).AmountUsd);
        Assert.Equal("Terms differ", MainMargin(result, PricingTerm.PermanentKey).UnavailableReason);
        Assert.Null(MainMargin(result, PricingTerm.FiniteYears(2).TermKey).AmountUsd);
        Assert.Equal([0, 1, 0], result.Terms.Select(term => term.ComparableOfferCount));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByDomainAsync_NoTermDoesNotMatchPermanent(bool catalogHasNoTerm)
    {
        // Arrange
        var catalogTerm = catalogHasNoTerm ? PricingTerm.Unknown : PricingTerm.Permanent;
        var offerTerm = catalogHasNoTerm ? PricingTerm.Permanent : PricingTerm.Unknown;
        AddCatalogPrice(PriceType.Main, 600m, catalogTerm);
        AddOffer(WebmasterOfferPriceType.Main, 400m, offerTerm);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        var comparison = MainMargin(result, catalogTerm.TermKey);
        Assert.Null(comparison.AmountUsd);
        Assert.Equal("Terms differ", comparison.UnavailableReason);
        Assert.Equal(0, Assert.Single(result.Terms).ComparableOfferCount);
    }

    [Fact]
    public async Task GetByDomainAsync_DoesNotTreatMalformedPriceTermAsNoTerm()
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Unknown);
        var offer = AddOffer(WebmasterOfferPriceType.Main, 400m, PricingTerm.Unknown);
        offer.Prices.Single().TermValue = 1;
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        Assert.Null(MainMargin(result, PricingTerm.UnknownKey).AmountUsd);
        Assert.Equal("Price term is invalid", MainMargin(result, PricingTerm.UnknownKey).UnavailableReason);
        var price = Assert.Single(result.Offers).Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Main);
        Assert.Null(price.TermKey);
        Assert.Equal("Invalid term", price.TermLabel);
    }

    [Fact]
    public async Task GetByDomainAsync_DoesNotUseMalformedCatalogPriceWhenAnotherServiceHasTheSameTermKey()
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Permanent);
        _context.SitePriceOptions.Add(new SitePriceOption
        {
            SiteDomain = "example.com", PriceType = PriceType.Casino, AmountUsd = 900m,
            TermKey = PricingTerm.PermanentKey, TermType = null
        });
        AddOffer(WebmasterOfferPriceType.Casino, 400m, PricingTerm.Permanent);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        var term = Assert.Single(result.Terms);
        Assert.Equal(0, term.ComparableOfferCount);
        Assert.Null(term.CatalogPrices.Single(price => price.PriceType == WebmasterOfferPriceType.Casino).AmountUsd);
        var price = Assert.Single(result.Offers).Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Casino);
        var margin = Assert.Single(price.Margins);
        Assert.Null(margin.AmountUsd);
        Assert.Equal("No catalog price", margin.UnavailableReason);
    }

    [Fact]
    public async Task GetByDomainAsync_CountsEachComparableOfferOnceAndKeepsStableDefaultForTiedCounts()
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Permanent);
        AddCatalogPrice(PriceType.Casino, 900m, PricingTerm.Permanent);
        AddCatalogPrice(PriceType.Main, 450m, PricingTerm.FiniteYears(1));
        var offer = AddOffer(WebmasterOfferPriceType.Main, 400m, PricingTerm.Permanent);
        offer.Prices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Casino,
            AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = 700m,
            TermType = TermType.Permanent
        });
        AddOffer(WebmasterOfferPriceType.Main, 300m, PricingTerm.FiniteYears(1));
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        Assert.Equal([PricingTerm.PermanentKey, PricingTerm.FiniteYears(1).TermKey], result.Terms.Select(term => term.TermKey));
        Assert.All(result.Terms, term => Assert.Equal(1, term.ComparableOfferCount));
        Assert.Equal(PricingTerm.PermanentKey, result.DefaultTermKey);
    }

    [Fact]
    public async Task GetByDomainAsync_OmitsRawPriceDetailsWithoutChangingOrdinaryOffers()
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Permanent);
        var offer = AddOffer(WebmasterOfferPriceType.Main, 400m, PricingTerm.Permanent);
        offer.Prices.Single().WebmasterPriceDetails = "Original price details";
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");
        var ordinary = await new WebmasterOffersService(_context).GetByDomainAsync("example.com");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        // Assert
        var prices = json.RootElement.GetProperty("offers")[0].GetProperty("prices").EnumerateArray();
        Assert.All(prices, price => Assert.False(price.TryGetProperty("webmasterPriceDetails", out _)));
        Assert.Equal("Original price details", Assert.Single(ordinary.Offers).Prices.Single().WebmasterPriceDetails);
        Assert.Equal(200m, MainMargin(result, PricingTerm.PermanentKey).AmountUsd);
    }

    [Theory]
    [InlineData(ServiceAvailabilityStatus.NotAvailable, null, "Service unavailable")]
    [InlineData(ServiceAvailabilityStatus.AvailableWithUnknownPrice, null, "No numeric price")]
    [InlineData(ServiceAvailabilityStatus.Unknown, 400, "No numeric price")]
    [InlineData(ServiceAvailabilityStatus.Available, null, "No numeric price")]
    [InlineData(ServiceAvailabilityStatus.Available, 0, "No numeric price")]
    [InlineData(ServiceAvailabilityStatus.Available, -1, "No numeric price")]
    public async Task GetByDomainAsync_DoesNotCalculateUnavailableOrInvalidPrices(ServiceAvailabilityStatus status, int? amount, string reason)
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Unknown);
        AddOffer(WebmasterOfferPriceType.Main, amount, PricingTerm.Unknown, status);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        Assert.Null(MainMargin(result, PricingTerm.UnknownKey).AmountUsd);
        Assert.Equal(reason, MainMargin(result, PricingTerm.UnknownKey).UnavailableReason);
    }

    [Fact]
    public async Task GetByDomainAsync_PreservesOriginalOfferAndColumnOrderIncludingInactiveAndBannerOnlyOffers()
    {
        // Arrange
        AddCatalogPrice(PriceType.Main, 600m, PricingTerm.Unknown);
        AddOffer(WebmasterOfferPriceType.Main, 650m, PricingTerm.Unknown, offerStatus: SiteWebmasterOfferStatus.Inactive);
        AddOffer(WebmasterOfferPriceType.Main, 350m, PricingTerm.FiniteYears(1));
        AddOffer(WebmasterOfferPriceType.Banner, 1200m, PricingTerm.Unknown);
        await _context.SaveChangesAsync();
        var ordinary = await new WebmasterOffersService(_context).GetByDomainAsync("example.com");

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        Assert.Equal(ordinary.Offers.Select(offer => offer.Id), result.Offers.Select(offer => offer.Id));
        Assert.Contains(result.Offers, offer => offer.Status == SiteWebmasterOfferStatus.Inactive);
        Assert.All(result.Offers, offer => Assert.Equal(
            [WebmasterOfferPriceType.Main, WebmasterOfferPriceType.Casino, WebmasterOfferPriceType.Crypto,
                WebmasterOfferPriceType.Dating, WebmasterOfferPriceType.LinkInsertion, WebmasterOfferPriceType.LinkInsertion18Plus],
            offer.Prices.Select(price => price.PriceType)));
        Assert.All(result.Offers.Last().Prices, price => Assert.Null(price.WebmasterPriceUsd));
    }

    [Fact]
    public async Task GetByDomainAsync_UsesOnlyTermAwareCatalogPricesAndDoesNotCreateFallbackTerms()
    {
        // Arrange
        AddCatalogPrice(PriceType.Casino, 900m, PricingTerm.Unknown);
        AddOffer(WebmasterOfferPriceType.Main, 400m, PricingTerm.Permanent);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().GetByDomainAsync("example.com");

        // Assert
        Assert.Equal(PricingTerm.UnknownKey, Assert.Single(result.Terms).TermKey);
        Assert.Null(result.Terms[0].CatalogPrices.Single(price => price.PriceType == WebmasterOfferPriceType.Main).AmountUsd);
        Assert.Equal("No catalog price", MainMargin(result, PricingTerm.UnknownKey).UnavailableReason);
        Assert.DoesNotContain(result.Terms, term => term.TermKey == PricingTerm.PermanentKey);
    }

    [Fact]
    public async Task GetByDomainAsync_HandlesMissingSiteMissingCatalogPricesAndNoOffers()
    {
        // Arrange
        var service = CreateService();

        // Act
        var missing = await service.GetByDomainAsync("missing.example");
        var empty = await service.GetByDomainAsync("example.com");

        // Assert
        Assert.False(missing.SiteFound);
        Assert.Empty(missing.Terms);
        Assert.Empty(missing.Offers);
        Assert.True(empty.SiteFound);
        Assert.Empty(empty.Terms);
        Assert.Empty(empty.Offers);
        Assert.Null(empty.DefaultTermKey);
    }

    private WebmasterPriceComparisonService CreateService() => new(_context, new WebmasterOffersService(_context));

    private static WebmasterComparisonMarginDto MainMargin(WebmasterPriceComparisonResult result, string termKey)
        => Assert.Single(result.Offers).Prices.Single(price => price.PriceType == WebmasterOfferPriceType.Main)
            .Margins.Single(margin => margin.TermKey == termKey);

    private void AddCatalogPrice(PriceType type, decimal amount, PricingTerm term)
        => _context.SitePriceOptions.Add(new SitePriceOption
        {
            SiteDomain = "example.com", PriceType = type, AmountUsd = amount,
            TermKey = term.TermKey, TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit
        });

    private SiteWebmasterOffer AddOffer(WebmasterOfferPriceType type, decimal? amount, PricingTerm term,
        ServiceAvailabilityStatus status = ServiceAvailabilityStatus.Available,
        SiteWebmasterOfferStatus offerStatus = SiteWebmasterOfferStatus.Active)
    {
        var id = Guid.NewGuid();
        var email = $"{id:N}@example.com";
        var offer = new SiteWebmasterOffer
        {
            Id = id, SiteDomain = "example.com", Status = offerStatus, ContactRawText = email,
            ImportFingerprint = id.ToString("N").PadRight(64, '0'),
            Webmaster = new Webmaster { Id = Guid.NewGuid(), PrimaryEmail = email, ContactRawText = email, NormalizedContactRawText = email },
            TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit,
            Prices = [new WebmasterOfferPrice
            {
                Id = Guid.NewGuid(), PriceType = type, AvailabilityStatus = status, WebmasterPriceUsd = amount,
                TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit
            }]
        };
        _context.SiteWebmasterOffers.Add(offer);
        return offer;
    }

    public void Dispose() => _context.Dispose();
}
