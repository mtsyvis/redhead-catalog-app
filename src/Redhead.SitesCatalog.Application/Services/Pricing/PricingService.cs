using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.Pricing;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Services.Pricing;

public sealed class PricingService(ApplicationDbContext context, IOptions<LinkDetectiveOptions> options, TimeProvider clock)
{
    public async Task<PricingPage> SearchAsync(PricingQuery request, CancellationToken cancellationToken)
    {
        ValidateQuery(request);

        var sites = FilterSites(request);
        var margins = CalculateMargins(sites, request.PriceType);
        var filteredMargins = FilterMargins(margins, request);
        var totalCount = await filteredMargins.CountAsync(cancellationToken);
        var sortedMargins = SortMargins(sites, filteredMargins, request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PaginationDefaults.MaxTablePageSize);
        var offset = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);

        // Page domains in SQL before loading details, then preserve that order in the response.
        var domains = await sortedMargins
            .Skip(offset)
            .Take(pageSize)
            .Select(row => row.Domain)
            .ToArrayAsync(cancellationToken);
        var rows = await LoadSitesAsync(domains, cancellationToken);
        var rowsByDomain = rows.ToDictionary(row => row.Domain);
        var items = domains
            .Where(rowsByDomain.ContainsKey)
            .Select(domain => rowsByDomain[domain])
            .ToList();

        return new PricingPage(items, totalCount, page, pageSize);
    }

    public async Task<PricingSiteDto?> GetAsync(string domain, CancellationToken cancellationToken)
    {
        var normalizedDomain = DomainNormalizer.Normalize(domain);
        var rows = await LoadSitesAsync([normalizedDomain], cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<IReadOnlyList<PricingTermDto>> GetTermsAsync(CancellationToken cancellationToken)
    {
        var terms = await context.SitePriceOptions
            .Select(price => new { price.TermKey, price.TermType, price.TermValue, price.TermUnit })
            .Distinct()
            .ToListAsync(cancellationToken);

        return terms
            .Select(term => new PricingTerm(term.TermKey, term.TermType, term.TermValue, term.TermUnit))
            .OrderBy(GetTermSortOrder)
            .ThenBy(term => term.TermValue)
            .Select(ToTermDto)
            .ToList();
    }

    private IQueryable<Site> FilterSites(PricingQuery request)
    {
        var sites = context.Sites.AsNoTracking();
        var search = DomainNormalizer.Normalize(request.Domain);
        if (!string.IsNullOrEmpty(search))
        {
            sites = sites.Where(site => site.Domain.Contains(search));
        }
        if (request.IsQuarantined is { } quarantined)
        {
            sites = sites.Where(site => site.IsQuarantined == quarantined);
        }
        if (!string.IsNullOrWhiteSpace(request.TermKey))
        {
            sites = sites.Where(site => context.SitePriceOptions.Any(price =>
                price.SiteDomain == site.Domain && price.TermKey == request.TermKey));
        }

        return sites;
    }

    private static IQueryable<MarginRow> FilterMargins(IQueryable<MarginRow> margins, PricingQuery request)
    {
        if (request.NeedsReview)
        {
            margins = margins.Where(row => row.TermCount > 1);
        }

        margins = request.Margin switch
        {
            "nonPositive" => margins.Where(row => row.Margin <= 0),
            "missing" => margins.Where(row => row.Margin == null),
            _ => margins
        };

        if (request.MinMargin is { } minimum)
        {
            margins = margins.Where(row => row.Margin >= minimum);
        }
        if (request.MaxMargin is { } maximum)
        {
            margins = margins.Where(row => row.Margin <= maximum);
        }

        return margins;
    }

    private IOrderedQueryable<MarginRow> SortMargins(
        IQueryable<Site> sites,
        IQueryable<MarginRow> filteredMargins,
        PricingQuery request)
    {
        if (request.SortBy == "domain")
        {
            return request.Descending
                ? filteredMargins.OrderByDescending(row => row.Domain)
                : filteredMargins.OrderBy(row => row.Domain);
        }

        var sortPriceType = request.SortPriceType ?? request.PriceType;
        if (sortPriceType != request.PriceType)
        {
            // Join back to the filtered domains so sorting by another service cannot change membership.
            var sortMargins = CalculateMargins(sites, sortPriceType);
            filteredMargins = from filtered in filteredMargins
                              join sorted in sortMargins on filtered.Domain equals sorted.Domain
                              select sorted;
        }

        var knownMarginsFirst = filteredMargins.OrderBy(row => row.Margin == null);
        var ordered = request.Descending
            ? knownMarginsFirst.ThenByDescending(row => row.Margin)
            : knownMarginsFirst.ThenBy(row => row.Margin);

        return ordered.ThenBy(row => row.Domain);
    }

    private IQueryable<MarginRow> CalculateMargins(IQueryable<Site> sites, PriceType priceType)
    {
        var selectedPrices = context.SitePriceOptions.Where(price =>
            price.PriceType == priceType &&
            (price.PriceType == PriceType.Main || !context.SiteServiceAvailabilities.Any(status =>
                status.SiteDomain == price.SiteDomain &&
                status.ServiceType == price.PriceType &&
                status.Status != ServiceAvailabilityStatus.Available)));

        var offerType = CatalogPriceMapping.ToOfferType(priceType);
        var matchingOffers =
            from price in selectedPrices
            from offer in context.WebmasterOfferPrices
            where offer.SiteWebmasterOffer.SiteDomain == price.SiteDomain &&
                  offer.SiteWebmasterOffer.Status == SiteWebmasterOfferStatus.Active &&
                  offer.PriceType == offerType &&
                  offer.AvailabilityStatus == ServiceAvailabilityStatus.Available &&
                  offer.WebmasterPriceUsd > 0 &&
                  offer.TermType == price.TermType &&
                  offer.TermValue == price.TermValue &&
                  offer.TermUnit == price.TermUnit
            select new { price.SiteDomain, Amount = offer.WebmasterPriceUsd };

        return sites.Select(site => new
        {
            site.Domain,
            TermCount = context.SitePriceOptions
                .Where(price => price.SiteDomain == site.Domain)
                .Select(price => price.TermKey)
                .Distinct()
                .Count(),
            Price = selectedPrices
                .Where(price => price.SiteDomain == site.Domain)
                .Select(price => (decimal?)price.AmountUsd)
                .FirstOrDefault(),
            Offer = matchingOffers
                .Where(offer => offer.SiteDomain == site.Domain)
                .Select(offer => offer.Amount)
                .Min()
        }).Select(site => new MarginRow
        {
            Domain = site.Domain,
            TermCount = site.TermCount,
            // Multiple terms require manual review; never combine their prices into one margin.
            Margin = site.TermCount == 1 ? site.Price - site.Offer : null
        });
    }

    private async Task<List<PricingSiteDto>> LoadSitesAsync(string[] domains, CancellationToken cancellationToken)
    {
        if (domains.Length == 0) return [];

        var sites = await context.Sites.AsNoTracking()
            .Where(site => domains.Contains(site.Domain))
            .Include(site => site.PriceOptions)
            .Include(site => site.ServiceAvailabilities)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var offers = await LoadActiveOffersAsync(domains, cancellationToken);
        var offersByDomain = offers.ToLookup(offer => offer.Domain);

        // Read saved market references only; this screen must not trigger LinkDetective API calls.
        var marketCaches = await context.LinkDetectivePriceCaches.AsNoTracking()
            .Where(cache => domains.Contains(cache.SiteDomain))
            .ToDictionaryAsync(cache => cache.SiteDomain, cancellationToken);
        var freshnessCutoff = options.Value.FreshnessCutoffUtc(clock.GetUtcNow().UtcDateTime);

        return sites.Select(site =>
        {
            marketCaches.TryGetValue(site.Domain, out var marketCache);
            return CreateSiteDto(site, offersByDomain[site.Domain], marketCache, freshnessCutoff);
        }).ToList();
    }

    private Task<List<OfferRow>> LoadActiveOffersAsync(string[] domains, CancellationToken cancellationToken)
    {
        return context.WebmasterOfferPrices.AsNoTracking()
            .Where(price => domains.Contains(price.SiteWebmasterOffer.SiteDomain) &&
                price.SiteWebmasterOffer.Status == SiteWebmasterOfferStatus.Active &&
                price.AvailabilityStatus == ServiceAvailabilityStatus.Available &&
                price.WebmasterPriceUsd > 0)
            .Select(price => new OfferRow(
                price.SiteWebmasterOffer.SiteDomain,
                price.SiteWebmasterOfferId,
                price.PriceType,
                price.WebmasterPriceUsd!.Value,
                price.TermType,
                price.TermValue,
                price.TermUnit))
            .ToListAsync(cancellationToken);
    }

    private static PricingSiteDto CreateSiteDto(
        Site site,
        IEnumerable<OfferRow> offers,
        LinkDetectivePriceCache? marketCache,
        DateTime freshnessCutoff)
    {
        var terms = GetSiteTerms(site);
        var hasInvalidTerms = site.PriceOptions.Any(price => !PricingTerm.TryCreate(
            price.TermKey, price.TermType, price.TermValue, price.TermUnit, out _));
        var pricing = SitePricingUpdater.Snapshot(site);
        var comparisons = new List<PricingPriceDto>();
        foreach (var term in terms.DefaultIfEmpty(PricingTerm.Unknown))
        {
            foreach (var priceType in Enum.GetValues<PriceType>())
            {
                comparisons.Add(CreatePriceComparison(pricing, offers, term, priceType));
            }
        }

        var market = marketCache is null ? null : new PricingMarketDto(
            marketCache.MinimumUsd,
            marketCache.AverageUsd,
            marketCache.MedianUsd,
            marketCache.CheckedAtUtc,
            marketCache.CheckedAtUtc < freshnessCutoff,
            marketCache.LastError);

        return new PricingSiteDto(
            site.Domain,
            site.IsQuarantined,
            SitePricingUpdater.Version(pricing),
            terms.Select(ToTermDto).ToList(),
            hasInvalidTerms,
            pricing,
            comparisons,
            market);
    }

    private static List<PricingTerm> GetSiteTerms(Site site)
    {
        return site.PriceOptions
            .Where(price => price.AmountUsd > 0 && PricingTerm.TryCreate(
                price.TermKey, price.TermType, price.TermValue, price.TermUnit, out _))
            .Select(price => new PricingTerm(price.TermKey, price.TermType, price.TermValue, price.TermUnit))
            .DistinctBy(term => term.TermKey)
            .OrderBy(GetTermSortOrder)
            .ThenBy(term => term.TermValue)
            .ToList();
    }

    private static PricingPriceDto CreatePriceComparison(
        UpdateSitePricingRequest pricing,
        IEnumerable<OfferRow> offers,
        PricingTerm term,
        PriceType priceType)
    {
        var status = pricing.ServiceAvailabilities.FirstOrDefault(row => row.ServiceType == priceType)?.Status;
        var isAvailable = priceType == PriceType.Main || status is null or ServiceAvailabilityStatus.Available;
        var catalogPrice = isAvailable
            ? pricing.Prices.FirstOrDefault(price =>
                price.PriceType == priceType && price.TermKey == term.TermKey && price.AmountUsd > 0 &&
                PricingTerm.TryCreate(price.TermKey, price.TermType, price.TermValue, price.TermUnit, out _))?.AmountUsd
            : null;

        // No term matches only No term; it is not a wildcard for other placement terms.
        var offerType = CatalogPriceMapping.ToOfferType(priceType);
        var bestOffer = offers
            .Where(offer => offer.Type == offerType &&
                offer.TermType == term.TermType && offer.TermValue == term.TermValue && offer.TermUnit == term.TermUnit)
            .OrderBy(offer => offer.Amount)
            .ThenBy(offer => offer.OfferId)
            .FirstOrDefault();

        return new PricingPriceDto(
            priceType,
            term.TermKey,
            catalogPrice,
            bestOffer?.Amount,
            bestOffer?.OfferId,
            catalogPrice - bestOffer?.Amount);
    }

    private static int GetTermSortOrder(PricingTerm term) =>
        term.TermKey == PricingTerm.UnknownKey ? 0 : term.TermType == TermType.Finite ? 1 : 2;

    private static PricingTermDto ToTermDto(PricingTerm term) =>
        new(term.TermKey, term.TermKey == PricingTerm.UnknownKey ? "No term" : term.DisplayLabel);

    private static void ValidateQuery(PricingQuery query)
    {
        if (!Enum.IsDefined(query.PriceType) ||
            query.Margin is not ("all" or "nonPositive" or "missing") ||
            query.SortBy is not ("domain" or "margin") ||
            (query.SortPriceType is { } sortType && !Enum.IsDefined(sortType)) ||
            DomainNormalizer.Normalize(query.Domain).Length > 253 ||
            query.TermKey?.Length > 100 ||
            query.MinMargin > query.MaxMargin)
        {
            throw new RequestValidationException("Invalid pricing filters or sort order.");
        }
    }

    private sealed class MarginRow
    {
        public string Domain { get; init; } = string.Empty;
        public int TermCount { get; init; }
        public decimal? Margin { get; init; }
    }

    private sealed record OfferRow(
        string Domain,
        Guid OfferId,
        WebmasterOfferPriceType Type,
        decimal Amount,
        TermType? TermType,
        int? TermValue,
        TermUnit? TermUnit);
}
