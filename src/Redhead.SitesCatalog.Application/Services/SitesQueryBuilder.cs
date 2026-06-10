using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for building IQueryable for sites with filters and sorting
/// </summary>
public class SitesQueryBuilder : ISitesQueryBuilder
{
    private static readonly MethodInfo ILikeMethod = typeof(NpgsqlDbFunctionsExtensions)
        .GetMethods()
        .Single(method =>
            method.Name == nameof(NpgsqlDbFunctionsExtensions.ILike) &&
            method.GetParameters() is
            [
                { ParameterType: var dbFunctionsType },
                { ParameterType: var matchExpressionType },
                { ParameterType: var patternType },
                { ParameterType: var escapeCharacterType }
            ] &&
            dbFunctionsType == typeof(DbFunctions) &&
            matchExpressionType == typeof(string) &&
            patternType == typeof(string) &&
            escapeCharacterType == typeof(string));

    private static readonly MethodInfo StringIndexOfMethod = typeof(string)
        .GetMethod(nameof(string.IndexOf), [typeof(string), typeof(StringComparison)])!;

    private readonly ApplicationDbContext? _context;

    public SitesQueryBuilder(ApplicationDbContext? context = null)
    {
        _context = context;
    }

    public IQueryable<Site> BuildQuery(IQueryable<Site> baseQuery, SitesQuery query)
    {
        var sitesQuery = baseQuery;
        var selectedTermKey = NormalizeTermKey(query.TermKey);

        // Apply search filter (partial domain match)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalizedSearch = DomainNormalizer.Normalize(query.Search);
            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                sitesQuery = sitesQuery.Where(s => s.Domain.Contains(normalizedSearch));
            }
        }

        if (query.StopListDomains is { Count: > 0 })
        {
            var stopListDomains = StopListParser.Parse(query.StopListDomains);
            if (stopListDomains is { Count: > 0 })
            {
                sitesQuery = sitesQuery.Where(s => !stopListDomains.Contains(s.Domain));
            }
        }

        // Apply range filters
        sitesQuery = ApplySelectedTermFilter(sitesQuery, selectedTermKey);

        sitesQuery = ApplyRangeFilters(sitesQuery, query, selectedTermKey);

        sitesQuery = ApplyLocationFilter(sitesQuery, query);

        sitesQuery = ApplyLanguageFilter(sitesQuery, query.Languages);

        sitesQuery = ApplyTopicFitFilter(sitesQuery, query);

        sitesQuery = ApplyAvailabilityFilters(sitesQuery, query, selectedTermKey);

        // Apply quarantine filter
        sitesQuery = ApplyQuarantineFilter(sitesQuery, query.Quarantine);

        // Apply last published date filter
        sitesQuery = ApplyLastPublishedDateFilter(sitesQuery, query);

        // Apply sorting
        sitesQuery = ApplySorting(sitesQuery, query.SortBy, query.SortDir, selectedTermKey);

        return sitesQuery;
    }

    private static string[] NormalizeNicheFilter(IReadOnlyCollection<string>? niches)
    {
        if (niches is null || niches.Count == 0)
        {
            return [];
        }

        return NicheNormalizer.NormalizeTokens(niches);
    }

    private IQueryable<Site> ApplyTopicFitFilter(IQueryable<Site> query, SitesQuery filters)
    {
        var includedNicheTokens = NormalizeNicheFilter(filters.Niches);
        var includedCategoryTerms = CategorySearchTermParser.NormalizeAndValidate(filters.CategorySearchTerms) ?? [];
        var excludedNicheTokens = NormalizeNicheFilter(filters.ExcludedNiches);
        var excludedCategoryTerms = CategorySearchTermParser.NormalizeAndValidate(filters.ExcludedCategorySearchTerms) ?? [];
        var hasIncludedNiches = includedNicheTokens.Length > 0;
        var hasIncludedCategories = includedCategoryTerms.Count > 0;

        if (hasIncludedNiches && hasIncludedCategories && IsExpandTopicFitMode(filters.TopicFitMode))
        {
            query = query.Where(BuildTopicFitIncludeAnyPredicate(includedNicheTokens, includedCategoryTerms));
        }
        else
        {
            if (hasIncludedNiches)
            {
                query = query.Where(BuildNichePredicate(includedNicheTokens));
            }

            if (hasIncludedCategories)
            {
                query = ApplyCategorySearchFilter(query, includedCategoryTerms);
            }
        }

        if (excludedNicheTokens.Length > 0)
        {
            query = query.Where(BuildExcludedNichePredicate(excludedNicheTokens));
        }

        if (excludedCategoryTerms.Count > 0)
        {
            query = ApplyExcludedCategorySearchFilter(query, excludedCategoryTerms);
        }

        return query;
    }

    private static bool IsExpandTopicFitMode(string? mode)
        => string.Equals(mode, TopicFitModeValues.Expand, StringComparison.OrdinalIgnoreCase);

    private IQueryable<Site> ApplyLocationFilter(IQueryable<Site> query, SitesQuery filters)
    {
        var explicitLocationKeys = NormalizeLocationKeys(filters.LocationKeys ?? filters.Locations);
        var groupKeys = NormalizeLocationKeys(filters.LocationGroupKeys);
        var excludedLocationKeys = NormalizeLocationKeys(filters.ExcludedLocationKeys);
        var hasLocationFilter = explicitLocationKeys.Length > 0
                                || groupKeys.Length > 0
                                || filters.IncludeUnknownLocation
                                || filters.IncludeOtherLocation;

        if (!hasLocationFilter)
        {
            return query;
        }

        var groupLocationKeys = Array.Empty<string>();
        if (groupKeys.Length > 0 && _context is not null)
        {
            groupLocationKeys = _context.LocationGroupItems
                .Where(item => groupKeys.Contains(item.GroupKey))
                .Select(item => item.LocationKey)
                .Distinct()
                .ToArray();
        }

        return query.Where(site =>
            (site.LocationKey != null
                && !excludedLocationKeys.Contains(site.LocationKey)
                && explicitLocationKeys.Contains(site.LocationKey))
            || (site.LocationKey != null
                && !excludedLocationKeys.Contains(site.LocationKey)
                && groupLocationKeys.Contains(site.LocationKey))
            || (filters.IncludeUnknownLocation && site.LocationKey == LocationConstants.UnknownLocationKey)
            || (filters.IncludeOtherLocation && site.LocationKey == null)
            || (site.Location != null
                && (site.LocationKey == null || !excludedLocationKeys.Contains(site.LocationKey))
                && explicitLocationKeys.Contains(site.Location)));
    }

    private static string[] NormalizeLocationKeys(IReadOnlyCollection<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    private IQueryable<Site> ApplyCategorySearchFilter(
        IQueryable<Site> query,
        IReadOnlyCollection<string?>? categorySearchTerms)
    {
        var terms = CategorySearchTermParser.NormalizeAndValidate(categorySearchTerms);
        if (terms is null || terms.Count == 0)
        {
            return query;
        }

        return IsInMemoryProvider()
            ? query.Where(BuildInMemoryCategorySearchPredicate(terms))
            : query.Where(BuildPostgresCategorySearchPredicate(terms));
    }

    private IQueryable<Site> ApplyExcludedCategorySearchFilter(
        IQueryable<Site> query,
        IReadOnlyCollection<string?>? categorySearchTerms)
    {
        var terms = CategorySearchTermParser.NormalizeAndValidate(categorySearchTerms);
        if (terms is null || terms.Count == 0)
        {
            return query;
        }

        return IsInMemoryProvider()
            ? query.Where(BuildInMemoryExcludedCategorySearchPredicate(terms))
            : query.Where(BuildPostgresExcludedCategorySearchPredicate(terms));
    }

    private Expression<Func<Site, bool>> BuildTopicFitIncludeAnyPredicate(
        IReadOnlyCollection<string> nicheTokens,
        IReadOnlyList<string> categoryTerms)
    {
        var site = Expression.Parameter(typeof(Site), "site");
        Expression body = BuildNichePredicateBody(site, nicheTokens);
        body = Expression.OrElse(
            body,
            IsInMemoryProvider()
                ? BuildInMemoryCategorySearchPredicateBody(site, categoryTerms)
                : BuildPostgresCategorySearchPredicateBody(site, categoryTerms));

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static Expression<Func<Site, bool>> BuildPostgresCategorySearchPredicate(IReadOnlyList<string> terms)
    {
        var site = Expression.Parameter(typeof(Site), "site");
        var body = BuildPostgresCategorySearchPredicateBody(site, terms);

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static Expression<Func<Site, bool>> BuildPostgresExcludedCategorySearchPredicate(IReadOnlyList<string> terms)
    {
        var site = Expression.Parameter(typeof(Site), "site");
        var body = Expression.Not(BuildPostgresCategorySearchPredicateBody(site, terms));

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static Expression BuildPostgresCategorySearchPredicateBody(
        ParameterExpression site,
        IReadOnlyList<string> terms)
    {
        var categories = Expression.Property(site, nameof(Site.Categories));
        Expression body = Expression.Constant(false);

        foreach (var term in terms)
        {
            var pattern = $"%{CategorySearchTermParser.EscapeLikeTerm(term)}%";
            var ilike = Expression.Call(
                ILikeMethod,
                Expression.Property(null, typeof(EF), nameof(EF.Functions)),
                categories,
                Expression.Constant(pattern),
                Expression.Constant(@"\"));
            body = Expression.OrElse(body, ilike);
        }

        body = Expression.AndAlso(
            Expression.NotEqual(categories, Expression.Constant(null, typeof(string))),
            body);

        return body;
    }

    // Unit tests only. The in-memory provider does not support translating the ILike method, so we build a predicate using string.IndexOf for case-insensitive search.
    private static Expression<Func<Site, bool>> BuildInMemoryCategorySearchPredicate(IReadOnlyList<string> terms)
    {
        var site = Expression.Parameter(typeof(Site), "site");
        var body = BuildInMemoryCategorySearchPredicateBody(site, terms);

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static Expression<Func<Site, bool>> BuildInMemoryExcludedCategorySearchPredicate(IReadOnlyList<string> terms)
    {
        var site = Expression.Parameter(typeof(Site), "site");
        var body = Expression.Not(BuildInMemoryCategorySearchPredicateBody(site, terms));

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static Expression BuildInMemoryCategorySearchPredicateBody(
        ParameterExpression site,
        IReadOnlyList<string> terms)
    {
        var categories = Expression.Property(site, nameof(Site.Categories));
        Expression body = Expression.Constant(false);

        foreach (var term in terms)
        {
            var contains = Expression.GreaterThanOrEqual(
                Expression.Call(
                    categories,
                    StringIndexOfMethod,
                    Expression.Constant(term),
                    Expression.Constant(StringComparison.OrdinalIgnoreCase)),
                Expression.Constant(0));
            body = Expression.OrElse(body, contains);
        }

        body = Expression.AndAlso(
            Expression.NotEqual(categories, Expression.Constant(null, typeof(string))),
            body);

        return body;
    }

    private bool IsInMemoryProvider()
    {
        return string.Equals(
            _context?.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);
    }

    private static IQueryable<Site> ApplyLanguageFilter(IQueryable<Site> query, IReadOnlyCollection<string>? languages)
    {
        if (languages is null || languages.Count == 0)
        {
            return query;
        }

        var normalizedLanguages = languages
            .Select(LanguageNormalizer.Normalize)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedLanguages.Length == 0)
        {
            return query;
        }

        if (normalizedLanguages.Contains(LanguageNormalizer.Unknown, StringComparer.Ordinal))
        {
            return query.Where(s => s.Language == null || normalizedLanguages.Contains(s.Language));
        }

        return query.Where(s => normalizedLanguages.Contains(s.Language));
    }

    private static Expression<Func<Site, bool>> BuildNichePredicate(IReadOnlyCollection<string> nicheTokens)
    {
        var site = Expression.Parameter(typeof(Site), "site");
        var body = BuildNichePredicateBody(site, nicheTokens);

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static Expression<Func<Site, bool>> BuildExcludedNichePredicate(IReadOnlyCollection<string> nicheTokens)
    {
        var site = Expression.Parameter(typeof(Site), "site");
        var body = Expression.Not(BuildNichePredicateBody(site, nicheTokens));

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static Expression BuildNichePredicateBody(
        ParameterExpression site,
        IReadOnlyCollection<string> nicheTokens)
    {
        var nicheTokensProperty = Expression.Property(site, nameof(Site.NicheTokens));
        Expression body = Expression.Constant(false);

        foreach (var nicheToken in nicheTokens)
        {
            var contains = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [typeof(string)],
                nicheTokensProperty,
                Expression.Constant(nicheToken));
            body = Expression.OrElse(body, contains);
        }

        return body;
    }

    private IQueryable<Site> ApplySelectedTermFilter(IQueryable<Site> query, string? selectedTermKey)
    {
        if (selectedTermKey is null)
        {
            return query;
        }

        var priceOptions = GetRequiredContext().SitePriceOptions;
        return query.Where(site => priceOptions.Any(priceOption =>
            priceOption.SiteDomain == site.Domain &&
            priceOption.TermKey == selectedTermKey));
    }

    private IQueryable<Site> ApplyRangeFilters(IQueryable<Site> query, SitesQuery filters, string? selectedTermKey)
    {
        if (filters.DrMin.HasValue)
        {
            query = query.Where(s => s.DR >= filters.DrMin.Value);
        }
        if (filters.DrMax.HasValue)
        {
            query = query.Where(s => s.DR <= filters.DrMax.Value);
        }

        if (filters.TrafficMin.HasValue)
        {
            query = query.Where(s => s.Traffic >= filters.TrafficMin.Value);
        }
        if (filters.TrafficMax.HasValue)
        {
            query = query.Where(s => s.Traffic <= filters.TrafficMax.Value);
        }

        if (filters.PriceMin.HasValue || filters.PriceMax.HasValue)
        {
            var priceOptions = GetRequiredContext().SitePriceOptions;
            query = query.Where(site => priceOptions.Any(priceOption =>
                priceOption.SiteDomain == site.Domain &&
                priceOption.PriceType == PriceType.Main &&
                priceOption.AmountUsd > 0 &&
                (selectedTermKey == null || priceOption.TermKey == selectedTermKey) &&
                (!filters.PriceMin.HasValue || priceOption.AmountUsd >= filters.PriceMin.Value) &&
                (!filters.PriceMax.HasValue || priceOption.AmountUsd <= filters.PriceMax.Value)));
        }

        return query;
    }

    private IQueryable<Site> ApplyAvailabilityFilters(IQueryable<Site> query, SitesQuery filters, string? selectedTermKey)
    {
        query = ApplyAvailabilityFilter(query, filters.CasinoAvailability, PriceType.Casino, selectedTermKey);
        query = ApplyAvailabilityFilter(query, filters.CryptoAvailability, PriceType.Crypto, selectedTermKey);
        query = ApplyAvailabilityFilter(query, filters.LinkInsertAvailability, PriceType.LinkInsertion, selectedTermKey);
        query = ApplyAvailabilityFilter(query, filters.LinkInsertCasinoAvailability, PriceType.LinkInsertionCasino, selectedTermKey);
        query = ApplyAvailabilityFilter(query, filters.DatingAvailability, PriceType.Dating, selectedTermKey);

        return query;
    }

    private IQueryable<Site> ApplyAvailabilityFilter(
        IQueryable<Site> query,
        IReadOnlyCollection<ServiceAvailabilityStatus>? filters,
        PriceType serviceType,
        string? selectedTermKey)
    {
        if (filters is null || filters.Count == 0)
        {
            return query;
        }

        var statuses = filters
            .Distinct()
            .ToArray();

        if (statuses.Length == 0)
        {
            return query;
        }

        var includeHasPrice = statuses.Contains(ServiceAvailabilityStatus.Available);
        var includeYes = statuses.Contains(ServiceAvailabilityStatus.AvailableWithUnknownPrice);
        var includeNo = statuses.Contains(ServiceAvailabilityStatus.NotAvailable);
        var includeUnknown = statuses.Contains(ServiceAvailabilityStatus.Unknown);
        var context = GetRequiredContext();
        var priceOptions = context.SitePriceOptions;
        var serviceAvailabilities = context.SiteServiceAvailabilities;

        return query.Where(site =>
            (includeHasPrice && priceOptions.Any(priceOption =>
                priceOption.SiteDomain == site.Domain &&
                priceOption.PriceType == serviceType &&
                priceOption.AmountUsd > 0 &&
                (selectedTermKey == null || priceOption.TermKey == selectedTermKey))) ||
            (includeYes && serviceAvailabilities.Any(availability =>
                availability.SiteDomain == site.Domain &&
                availability.ServiceType == serviceType &&
                availability.Status == ServiceAvailabilityStatus.AvailableWithUnknownPrice)) ||
            (includeNo && serviceAvailabilities.Any(availability =>
                availability.SiteDomain == site.Domain &&
                availability.ServiceType == serviceType &&
                availability.Status == ServiceAvailabilityStatus.NotAvailable)) ||
            (includeUnknown &&
                !priceOptions.Any(priceOption =>
                    priceOption.SiteDomain == site.Domain &&
                    priceOption.PriceType == serviceType &&
                    priceOption.AmountUsd > 0) &&
                (!serviceAvailabilities.Any(availability =>
                    availability.SiteDomain == site.Domain &&
                    availability.ServiceType == serviceType) ||
                 serviceAvailabilities.Any(availability =>
                    availability.SiteDomain == site.Domain &&
                    availability.ServiceType == serviceType &&
                    availability.Status == ServiceAvailabilityStatus.Unknown))));
    }

    private static IQueryable<Site> ApplyLastPublishedDateFilter(IQueryable<Site> query, SitesQuery filters)
    {
        if (!filters.LastPublishedFrom.HasValue && !filters.LastPublishedToExclusive.HasValue)
        {
            return query;
        }

        query = query.Where(s => s.LastPublishedDate != null);

        if (filters.LastPublishedFrom.HasValue)
        {
            query = query.Where(s => s.LastPublishedDate >= filters.LastPublishedFrom.Value);
        }

        if (filters.LastPublishedToExclusive.HasValue)
        {
            query = query.Where(s => s.LastPublishedDate < filters.LastPublishedToExclusive.Value);
        }

        return query;
    }

    private static IQueryable<Site> ApplyQuarantineFilter(IQueryable<Site> query, string quarantine)
    {
        var filter = quarantine?.ToLowerInvariant() ?? QuarantineFilterValues.All;

        return filter switch
        {
            QuarantineFilterValues.Only => query.Where(s => s.IsQuarantined),
            QuarantineFilterValues.Exclude => query.Where(s => !s.IsQuarantined),
            _ => query
        };
    }

    private IQueryable<Site> ApplySorting(IQueryable<Site> query, string sortBy, string sortDir, string? selectedTermKey)
    {
        var sort = sortBy?.ToLowerInvariant() ?? SortingDefaults.DefaultSortBy;
        var direction = sortDir?.ToLowerInvariant() ?? SortingDefaults.DefaultSortDirection;

        return sort switch
        {
            SortFields.Domain => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.Domain)
                : query.OrderBy(s => s.Domain),
            SortFields.DR => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.DR)
                : query.OrderBy(s => s.DR),
            SortFields.Traffic => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.Traffic)
                : query.OrderBy(s => s.Traffic),
            SortFields.Location => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.Location)
                : query.OrderBy(s => s.Location),
            SortFields.PriceUsd => direction == SortingDefaults.Descending
                ? ApplyPriceOptionSorting(query, PriceType.Main, selectedTermKey, descending: true)
                : ApplyPriceOptionSorting(query, PriceType.Main, selectedTermKey, descending: false),
            SortFields.PriceCasino => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, PriceType.Casino, selectedTermKey, descending: true)
                : ApplyServicePriceSorting(query, PriceType.Casino, selectedTermKey, descending: false),
            SortFields.PriceCrypto => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, PriceType.Crypto, selectedTermKey, descending: true)
                : ApplyServicePriceSorting(query, PriceType.Crypto, selectedTermKey, descending: false),
            SortFields.PriceLinkInsert => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, PriceType.LinkInsertion, selectedTermKey, descending: true)
                : ApplyServicePriceSorting(query, PriceType.LinkInsertion, selectedTermKey, descending: false),
            SortFields.PriceLinkInsertCasino => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, PriceType.LinkInsertionCasino, selectedTermKey, descending: true)
                : ApplyServicePriceSorting(query, PriceType.LinkInsertionCasino, selectedTermKey, descending: false),
            SortFields.PriceDating => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, PriceType.Dating, selectedTermKey, descending: true)
                : ApplyServicePriceSorting(query, PriceType.Dating, selectedTermKey, descending: false),
            SortFields.NumberDFLinks => direction == SortingDefaults.Descending
                ? query.OrderBy(s => s.NumberDFLinks == null ? 1 : 0).ThenByDescending(s => s.NumberDFLinks).ThenBy(s => s.Domain)
                : query.OrderBy(s => s.NumberDFLinks == null ? 1 : 0).ThenBy(s => s.NumberDFLinks).ThenBy(s => s.Domain),
            // TODO(term-aware pricing): This remains legacy site-level term sorting for compatibility.
            // It uses Site.TermType/TermValue/TermUnit and does not represent all SitePriceOptions terms.
            SortFields.Term => direction == SortingDefaults.Descending
                ? query
                    .OrderBy(s => s.TermType == TermType.Permanent ? 0 : s.TermType == TermType.Finite && s.TermUnit == TermUnit.Year && s.TermValue != null ? 1 : 2)
                    .ThenByDescending(s => s.TermType == TermType.Finite && s.TermUnit == TermUnit.Year ? s.TermValue : null)
                    .ThenBy(s => s.Domain)
                : query
                    .OrderBy(s => s.TermType == TermType.Finite && s.TermUnit == TermUnit.Year && s.TermValue != null ? 0 : s.TermType == TermType.Permanent ? 1 : 2)
                    .ThenBy(s => s.TermType == TermType.Finite && s.TermUnit == TermUnit.Year ? s.TermValue : null)
                    .ThenBy(s => s.Domain),
            SortFields.CreatedAt => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.CreatedAtUtc)
                : query.OrderBy(s => s.CreatedAtUtc),
            SortFields.UpdatedAt => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.UpdatedAtUtc)
                : query.OrderBy(s => s.UpdatedAtUtc),
            SortFields.LastPublishedDate => direction == SortingDefaults.Descending
                ? query
                    .OrderBy(s => s.LastPublishedDate == null ? 1 : 0)
                    .ThenByDescending(s => s.LastPublishedDate)
                    .ThenBy(s => s.LastPublishedDateIsMonthOnly ? 1 : 0)
                    .ThenBy(s => s.Domain)
                : query
                    .OrderBy(s => s.LastPublishedDate == null ? 1 : 0)
                    .ThenBy(s => s.LastPublishedDate)
                    .ThenBy(s => s.LastPublishedDateIsMonthOnly ? 0 : 1)
                    .ThenBy(s => s.Domain),
            _ => query.OrderBy(s => s.Domain)
        };
    }

    private IQueryable<Site> ApplyPriceOptionSorting(
        IQueryable<Site> query,
        PriceType priceType,
        string? selectedTermKey,
        bool descending)
    {
        var priceOptions = GetRequiredContext().SitePriceOptions;
        var projected = query.Select(site => new
        {
            Site = site,
            Amount = priceOptions
                .Where(priceOption =>
                    priceOption.SiteDomain == site.Domain &&
                    priceOption.PriceType == priceType &&
                    priceOption.AmountUsd > 0 &&
                    (selectedTermKey == null || priceOption.TermKey == selectedTermKey))
                .Select(priceOption => (decimal?)priceOption.AmountUsd)
                .Min()
        });

        return descending
            ? projected
                .OrderBy(row => row.Amount == null ? 1 : 0)
                .ThenByDescending(row => row.Amount)
                .ThenBy(row => row.Site.Domain)
                .Select(row => row.Site)
            : projected
                .OrderBy(row => row.Amount == null ? 1 : 0)
                .ThenBy(row => row.Amount)
                .ThenBy(row => row.Site.Domain)
                .Select(row => row.Site);
    }

    private IQueryable<Site> ApplyServicePriceSorting(
        IQueryable<Site> query,
        PriceType serviceType,
        string? selectedTermKey,
        bool descending)
    {
        var context = GetRequiredContext();
        var priceOptions = context.SitePriceOptions;
        var serviceAvailabilities = context.SiteServiceAvailabilities;
        var projected = query.Select(site => new
        {
            Site = site,
            Amount = priceOptions
                .Where(priceOption =>
                    priceOption.SiteDomain == site.Domain &&
                    priceOption.PriceType == serviceType &&
                    priceOption.AmountUsd > 0 &&
                    (selectedTermKey == null || priceOption.TermKey == selectedTermKey))
                .Select(priceOption => (decimal?)priceOption.AmountUsd)
                .Min(),
            StatusRank = serviceAvailabilities
                .Where(availability =>
                    availability.SiteDomain == site.Domain &&
                    availability.ServiceType == serviceType)
                .Select(availability => (int?)(
                    availability.Status == ServiceAvailabilityStatus.AvailableWithUnknownPrice ? 1 :
                    availability.Status == ServiceAvailabilityStatus.NotAvailable ? 2 :
                    3))
                .Min() ?? 3
        });

        return descending
            ? projected
                .OrderBy(row => row.Amount == null ? row.StatusRank : 0)
                .ThenByDescending(row => row.Amount)
                .ThenBy(row => row.Site.Domain)
                .Select(row => row.Site)
            : projected
                .OrderBy(row => row.Amount == null ? row.StatusRank : 0)
                .ThenBy(row => row.Amount)
                .ThenBy(row => row.Site.Domain)
                .Select(row => row.Site);
    }

    private static string? NormalizeTermKey(string? rawValue)
        => string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();

    private ApplicationDbContext GetRequiredContext()
        => _context ?? throw new InvalidOperationException(
            $"{nameof(SitesQueryBuilder)} requires {nameof(ApplicationDbContext)} for term-aware pricing filters and sorting.");
}
