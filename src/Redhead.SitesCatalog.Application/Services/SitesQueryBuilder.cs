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
        sitesQuery = ApplyRangeFilters(sitesQuery, query);

        sitesQuery = ApplyLocationFilter(sitesQuery, query);

        sitesQuery = ApplyLanguageFilter(sitesQuery, query.Languages);

        var nicheTokens = NormalizeNicheFilter(query.Niches);
        if (nicheTokens.Length > 0)
        {
            sitesQuery = sitesQuery.Where(BuildNichePredicate(nicheTokens));
        }

        sitesQuery = ApplyCategorySearchFilter(sitesQuery, query.CategorySearchTerms);

        sitesQuery = ApplyAvailabilityFilters(sitesQuery, query);

        // Apply quarantine filter
        sitesQuery = ApplyQuarantineFilter(sitesQuery, query.Quarantine);

        // Apply last published date filter
        sitesQuery = ApplyLastPublishedDateFilter(sitesQuery, query);

        // Apply sorting
        sitesQuery = ApplySorting(sitesQuery, query.SortBy, query.SortDir);

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

    private static Expression<Func<Site, bool>> BuildPostgresCategorySearchPredicate(IReadOnlyList<string> terms)
    {
        var site = Expression.Parameter(typeof(Site), "site");
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

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    // Unit tests only. The in-memory provider does not support translating the ILike method, so we build a predicate using string.IndexOf for case-insensitive search.
    private static Expression<Func<Site, bool>> BuildInMemoryCategorySearchPredicate(IReadOnlyList<string> terms)
    {
        var site = Expression.Parameter(typeof(Site), "site");
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

        return Expression.Lambda<Func<Site, bool>>(body, site);
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

        return Expression.Lambda<Func<Site, bool>>(body, site);
    }

    private static IQueryable<Site> ApplyRangeFilters(IQueryable<Site> query, SitesQuery filters)
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
            query = query.Where(s => s.PriceUsd != null);
        }
        if (filters.PriceMin.HasValue)
        {
            query = query.Where(s => s.PriceUsd >= filters.PriceMin.Value);
        }
        if (filters.PriceMax.HasValue)
        {
            query = query.Where(s => s.PriceUsd <= filters.PriceMax.Value);
        }

        return query;
    }

    private static IQueryable<Site> ApplyAvailabilityFilters(IQueryable<Site> query, SitesQuery filters)
    {
        query = ApplyAvailabilityFilter(query, filters.CasinoAvailability, s => s.PriceCasinoStatus);
        query = ApplyAvailabilityFilter(query, filters.CryptoAvailability, s => s.PriceCryptoStatus);
        query = ApplyAvailabilityFilter(query, filters.LinkInsertAvailability, s => s.PriceLinkInsertStatus);
        query = ApplyAvailabilityFilter(query, filters.LinkInsertCasinoAvailability, s => s.PriceLinkInsertCasinoStatus);
        query = ApplyAvailabilityFilter(query, filters.DatingAvailability, s => s.PriceDatingStatus);

        return query;
    }

    private static IQueryable<Site> ApplyAvailabilityFilter(
        IQueryable<Site> query,
        IReadOnlyCollection<ServiceAvailabilityStatus>? filters,
        Expression<Func<Site, ServiceAvailabilityStatus>> statusSelector)
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

        var contains = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [typeof(ServiceAvailabilityStatus)],
            Expression.Constant(statuses),
            statusSelector.Body);

        return query.Where(Expression.Lambda<Func<Site, bool>>(contains, statusSelector.Parameters));
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

    private static IQueryable<Site> ApplySorting(IQueryable<Site> query, string sortBy, string sortDir)
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
                ? query.OrderBy(s => s.PriceUsd == null ? 1 : 0).ThenByDescending(s => s.PriceUsd)
                : query.OrderBy(s => s.PriceUsd == null ? 1 : 0).ThenBy(s => s.PriceUsd),
            SortFields.PriceCasino => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, nameof(Site.PriceCasino), nameof(Site.PriceCasinoStatus), descending: true)
                : ApplyServicePriceSorting(query, nameof(Site.PriceCasino), nameof(Site.PriceCasinoStatus), descending: false),
            SortFields.PriceCrypto => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, nameof(Site.PriceCrypto), nameof(Site.PriceCryptoStatus), descending: true)
                : ApplyServicePriceSorting(query, nameof(Site.PriceCrypto), nameof(Site.PriceCryptoStatus), descending: false),
            SortFields.PriceLinkInsert => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, nameof(Site.PriceLinkInsert), nameof(Site.PriceLinkInsertStatus), descending: true)
                : ApplyServicePriceSorting(query, nameof(Site.PriceLinkInsert), nameof(Site.PriceLinkInsertStatus), descending: false),
            SortFields.PriceLinkInsertCasino => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, nameof(Site.PriceLinkInsertCasino), nameof(Site.PriceLinkInsertCasinoStatus), descending: true)
                : ApplyServicePriceSorting(query, nameof(Site.PriceLinkInsertCasino), nameof(Site.PriceLinkInsertCasinoStatus), descending: false),
            SortFields.PriceDating => direction == SortingDefaults.Descending
                ? ApplyServicePriceSorting(query, nameof(Site.PriceDating), nameof(Site.PriceDatingStatus), descending: true)
                : ApplyServicePriceSorting(query, nameof(Site.PriceDating), nameof(Site.PriceDatingStatus), descending: false),
            SortFields.NumberDFLinks => direction == SortingDefaults.Descending
                ? query.OrderBy(s => s.NumberDFLinks == null ? 1 : 0).ThenByDescending(s => s.NumberDFLinks).ThenBy(s => s.Domain)
                : query.OrderBy(s => s.NumberDFLinks == null ? 1 : 0).ThenBy(s => s.NumberDFLinks).ThenBy(s => s.Domain),
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

    private static IOrderedQueryable<Site> ApplyServicePriceSorting(
        IQueryable<Site> query,
        string pricePropertyName,
        string statusPropertyName,
        bool descending)
    {
        var ordered = query.OrderBy(s =>
            EF.Property<ServiceAvailabilityStatus>(s, statusPropertyName) == ServiceAvailabilityStatus.Available ? 0 :
            EF.Property<ServiceAvailabilityStatus>(s, statusPropertyName) == ServiceAvailabilityStatus.AvailableWithUnknownPrice ? 1 :
            EF.Property<ServiceAvailabilityStatus>(s, statusPropertyName) == ServiceAvailabilityStatus.NotAvailable ? 2 :
            3);

        return descending
            ? ordered
                .ThenByDescending(s => EF.Property<decimal?>(s, pricePropertyName))
                .ThenBy(s => s.Domain)
            : ordered
                .ThenBy(s => EF.Property<decimal?>(s, pricePropertyName))
                .ThenBy(s => s.Domain);
    }
}
