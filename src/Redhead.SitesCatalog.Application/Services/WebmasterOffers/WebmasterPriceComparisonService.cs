using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public interface IWebmasterPriceComparisonService
{
    Task<WebmasterPriceComparisonResult> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);
}

public sealed class WebmasterPriceComparisonService(
    ApplicationDbContext context,
    IWebmasterOffersService offersService) : IWebmasterPriceComparisonService
{
    // Order and offer-facing names match the original Webmaster Offers table.
    private static readonly (PriceType Catalog, WebmasterOfferPriceType Offer)[] PriceTypes =
    [
        (PriceType.Main, WebmasterOfferPriceType.Main),
        (PriceType.Casino, WebmasterOfferPriceType.Casino),
        (PriceType.Crypto, WebmasterOfferPriceType.Crypto),
        (PriceType.Dating, WebmasterOfferPriceType.Dating),
        (PriceType.LinkInsertion, WebmasterOfferPriceType.LinkInsertion),
        (PriceType.LinkInsertionCasino, WebmasterOfferPriceType.LinkInsertion18Plus)
    ];

    public async Task<WebmasterPriceComparisonResult> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        // Reuse both domain normalization and the existing effective-price ordering.
        var source = await offersService.GetByDomainAsync(domain, cancellationToken);
        if (!source.SiteFound)
        {
            return new(source.Domain, false, null, [], []);
        }

        var supportedTypes = PriceTypes.Select(pair => pair.Catalog).ToArray();
        var priceRows = await context.SitePriceOptions.AsNoTracking()
            .Where(price => price.SiteDomain == source.Domain && supportedTypes.Contains(price.PriceType) && price.AmountUsd > 0)
            .ToListAsync(cancellationToken);
        var validPrices = priceRows
            .Select(price => (Price: price, Term: PricingTerm.TryCreate(
                price.TermKey, price.TermType, price.TermValue, price.TermUnit, out var term) ? term : null))
            .Where(entry => entry.Term is not null)
            .ToList();
        var terms = validPrices
            .Select(entry => entry.Term!)
            .DistinctBy(term => term.TermKey)
            .OrderBy(term => term.TermType == TermType.Permanent ? 0 : term.TermType == TermType.Finite ? 1 : 2)
            .ThenBy(term => term.TermValue)
            .ToList();
        var catalogPrices = validPrices.ToLookup(
            entry => (entry.Term!.TermKey, entry.Price.PriceType), entry => entry.Price);

        var offers = source.Offers.Select(offer => new WebmasterComparisonOfferDto(
            offer.Id, offer.PrimaryEmail, offer.Status, offer.TermLabel,
            PriceTypes.Select(pair =>
            {
                var price = offer.Prices.FirstOrDefault(row => row.PriceType == pair.Offer);
                var term = GetPriceTerm(price);
                return new WebmasterComparisonPriceDto(pair.Offer,
                    price?.AvailabilityStatus ?? ServiceAvailabilityStatus.Unknown,
                    price?.WebmasterPriceUsd,
                    term?.TermKey, price is null ? "No term" : term is null ? "Invalid term" : Label(term),
                    terms.Select(catalogTerm => Calculate(price,
                        catalogPrices[(catalogTerm.TermKey, pair.Catalog)].FirstOrDefault()?.AmountUsd,
                        catalogTerm.TermKey)).ToList());
            }).ToList())).ToList();

        var termOptions = terms.Select(term => new WebmasterComparisonTermDto(
            term.TermKey,
            Label(term),
            offers.Count(offer => offer.Prices.Any(price => price.Margins.Any(margin =>
                margin.TermKey == term.TermKey && margin.AmountUsd.HasValue))),
            PriceTypes.Select(pair => new WebmasterComparisonCatalogPriceDto(pair.Offer,
                catalogPrices[(term.TermKey, pair.Catalog)].FirstOrDefault()?.AmountUsd)).ToList())).ToList();

        var defaultTermKey = termOptions.OrderByDescending(term => term.ComparableOfferCount).FirstOrDefault()?.TermKey;
        return new(source.Domain, true, defaultTermKey, termOptions, offers);
    }

    private static WebmasterComparisonMarginDto Calculate(WebmasterOfferPriceDto? price, decimal? catalogPrice, string termKey)
    {
        string? reason = null;
        if (catalogPrice is not > 0)
        {
            reason = "No catalog price";
        }
        else if (price?.AvailabilityStatus == ServiceAvailabilityStatus.NotAvailable)
        {
            reason = "Service unavailable";
        }
        else if (price?.AvailabilityStatus != ServiceAvailabilityStatus.Available || price.WebmasterPriceUsd is not > 0)
        {
            reason = "No numeric price";
        }
        else if (GetPriceTerm(price) is not { } term)
        {
            reason = "Price term is invalid";
        }
        else if (term.TermKey != termKey)
        {
            reason = "Terms differ";
        }

        return new(termKey, reason is null ? catalogPrice!.Value - price!.WebmasterPriceUsd!.Value : null, reason);
    }

    private static PricingTerm? GetPriceTerm(WebmasterOfferPriceDto? price)
    {
        if (price is null)
        {
            return null;
        }
        var term = PricingTerm.FromTerm(price.TermType, price.TermValue, price.TermUnit);
        // No term matches No term; malformed partial terms must not silently become No term.
        return term.TermKey == PricingTerm.UnknownKey &&
            (price.TermType is not null || price.TermValue is not null || price.TermUnit is not null) ? null : term;
    }

    private static string Label(PricingTerm term) => term.TermKey == PricingTerm.UnknownKey ? "No term" : term.DisplayLabel;
}
