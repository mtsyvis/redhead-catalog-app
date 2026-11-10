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
        var domainCount = string.IsNullOrWhiteSpace(request.TermKey)
            ? await filteredMargins.Select(row => row.Domain).Distinct().CountAsync(cancellationToken)
            : totalCount;
        var sortedMargins = SortMargins(sites, filteredMargins, request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PaginationDefaults.MaxTablePageSize);
        var offset = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);

        // Page domain/term rows in SQL before loading their catalog details.
        var pageRows = await sortedMargins
            .Skip(offset)
            .Take(pageSize)
            .Select(row => new { row.Domain, row.TermKey })
            .ToListAsync(cancellationToken);
        var domains = pageRows.Select(row => row.Domain).Distinct().ToArray();
        var rows = await LoadSitesAsync(domains, cancellationToken);
        var rowsByDomain = rows.ToDictionary(row => row.Domain);
        var items = pageRows
            .Where(row => rowsByDomain.ContainsKey(row.Domain))
            .Select(row => rowsByDomain[row.Domain] with
            {
                Terms = rowsByDomain[row.Domain].Terms.Where(term => term.TermKey == row.TermKey).ToList(),
                Prices = rowsByDomain[row.Domain].Prices.Where(price => price.TermKey == row.TermKey).ToList()
            })
            .ToList();

        return new PricingPage(items, totalCount, domainCount, page, pageSize);
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
        if (request.MultipleTerms)
        {
            sites = sites.Where(site => context.SitePriceOptions
                .Where(price => price.SiteDomain == site.Domain)
                .Select(price => price.TermKey).Distinct().Count() > 1);
        }

        return sites;
    }

    private static IQueryable<MarginRow> FilterMargins(IQueryable<MarginRow> margins, PricingQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.TermKey))
        {
            margins = margins.Where(row => row.TermKey == request.TermKey);
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
            var byDomain = request.Descending
                ? filteredMargins.OrderByDescending(row => row.Domain)
                : filteredMargins.OrderBy(row => row.Domain);
            return SortTerms(byDomain);
        }

        var sortPriceType = request.SortPriceType ?? request.PriceType;
        if (sortPriceType != request.PriceType)
        {
            // Keep the filtered domain/term pairs when sorting by another service.
            var sortMargins = CalculateMargins(sites, sortPriceType);
            filteredMargins = from filtered in filteredMargins
                              join sorted in sortMargins
                                  on new { filtered.Domain, filtered.TermKey } equals new { sorted.Domain, sorted.TermKey }
                              select sorted;
        }

        var knownMarginsFirst = filteredMargins.OrderBy(row => row.Margin == null);
        var ordered = request.Descending
            ? knownMarginsFirst.ThenByDescending(row => row.Margin)
            : knownMarginsFirst.ThenBy(row => row.Margin);

        return SortTerms(ordered.ThenBy(row => row.Domain));
    }

    private static IOrderedQueryable<MarginRow> SortTerms(IOrderedQueryable<MarginRow> rows) =>
        rows.ThenBy(row => row.TermKey == PricingTerm.UnknownKey ? 0 : row.TermType == TermType.Finite ? 1 : 2)
            .ThenBy(row => row.TermValue).ThenBy(row => row.TermKey);

    private IQueryable<MarginRow> CalculateMargins(IQueryable<Site> sites, PriceType priceType)
    {
        var selectedPrices = context.SitePriceOptions.Where(price =>
            price.PriceType == priceType &&
            (price.PriceType == PriceType.Main || !context.SiteServiceAvailabilities.Any(status =>
                status.SiteDomain == price.SiteDomain &&
                status.ServiceType == price.PriceType &&
                status.Status != ServiceAvailabilityStatus.Available)));

        var offerType = CatalogPriceMapping.ToOfferType(priceType);
        var offers = context.WebmasterOfferPrices.Where(offer =>
            offer.SiteWebmasterOffer.Status == SiteWebmasterOfferStatus.Active &&
            offer.PriceType == offerType &&
            offer.AvailabilityStatus == ServiceAvailabilityStatus.Available &&
            offer.WebmasterPriceUsd > 0);
        var terms = context.SitePriceOptions
            .Select(price => new { price.SiteDomain, price.TermKey, price.TermType, price.TermValue })
            .Distinct();

        return from site in sites
               join term in terms on site.Domain equals term.SiteDomain into siteTerms
               from term in siteTerms.DefaultIfEmpty()
               select new MarginRow
               {
                   Domain = site.Domain,
                   TermKey = term.TermKey ?? "",
                   TermType = term.TermType,
                   TermValue = term.TermValue,
                   Margin = selectedPrices
                       .Where(price => price.SiteDomain == site.Domain && price.TermKey == (term.TermKey ?? ""))
                       .Select(price => (decimal?)price.AmountUsd - offers
                           .Where(offer => offer.SiteWebmasterOffer.SiteDomain == site.Domain &&
                               offer.TermType == price.TermType && offer.TermValue == price.TermValue &&
                               offer.TermUnit == price.TermUnit)
                           .Select(offer => offer.WebmasterPriceUsd).Min())
                       .FirstOrDefault()
               };
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
            return CreateSiteDto(site, offersByDomain[site.Domain], marketCache, freshnessCutoff,
                !string.IsNullOrWhiteSpace(options.Value.ApiToken));
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
        DateTime freshnessCutoff,
        bool canRefreshMarket)
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

        var market = new PricingMarketDto(
            marketCache?.MinimumUsd,
            marketCache?.AverageUsd,
            marketCache?.MedianUsd,
            marketCache?.CheckedAtUtc,
            marketCache?.CheckedAtUtc < freshnessCutoff,
            marketCache?.LastError,
            marketCache?.RetryAfterUtc,
            canRefreshMarket);

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
        public string TermKey { get; init; } = string.Empty;
        public TermType? TermType { get; init; }
        public int? TermValue { get; init; }
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
