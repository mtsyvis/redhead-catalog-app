using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for building IQueryable for sites with filters and sorting
/// </summary>
public class SitesQueryBuilder : ISitesQueryBuilder
{
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

        // Apply range filters
        sitesQuery = ApplyRangeFilters(sitesQuery, query);

        // Apply location filter
        if (query.Locations != null && query.Locations.Count > 0)
        {
            sitesQuery = sitesQuery.Where(s => query.Locations.Contains(s.Location));
        }

        // Apply allowed flags filters
        sitesQuery = ApplyAllowedFilters(sitesQuery, query);

        // Apply quarantine filter
        sitesQuery = ApplyQuarantineFilter(sitesQuery, query.Quarantine);

        // Apply last published date filter
        sitesQuery = ApplyLastPublishedDateFilter(sitesQuery, query);

        // Apply sorting
        sitesQuery = ApplySorting(sitesQuery, query.SortBy, query.SortDir);

        return sitesQuery;
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

    private static IQueryable<Site> ApplyAllowedFilters(IQueryable<Site> query, SitesQuery filters)
    {
        // New explicit availability filters take precedence over legacy boolean flags.
        if (filters.CasinoAvailability.HasValue)
        {
            query = filters.CasinoAvailability.Value switch
            {
                ServiceAvailabilityFilter.All => query,
                ServiceAvailabilityFilter.Available => query.Where(s => s.PriceCasinoStatus == ServiceAvailabilityStatus.Available),
                ServiceAvailabilityFilter.NotAvailable => query.Where(s => s.PriceCasinoStatus == ServiceAvailabilityStatus.NotAvailable),
                ServiceAvailabilityFilter.Unknown => query.Where(s => s.PriceCasinoStatus == ServiceAvailabilityStatus.Unknown),
                _ => query
            };
        }
        else if (filters.CasinoAllowed == true)
        {
            query = query.Where(s => s.PriceCasinoStatus == ServiceAvailabilityStatus.Available);
        }

        if (filters.CryptoAvailability.HasValue)
        {
            query = filters.CryptoAvailability.Value switch
            {
                ServiceAvailabilityFilter.All => query,
                ServiceAvailabilityFilter.Available => query.Where(s => s.PriceCryptoStatus == ServiceAvailabilityStatus.Available),
                ServiceAvailabilityFilter.NotAvailable => query.Where(s => s.PriceCryptoStatus == ServiceAvailabilityStatus.NotAvailable),
                ServiceAvailabilityFilter.Unknown => query.Where(s => s.PriceCryptoStatus == ServiceAvailabilityStatus.Unknown),
                _ => query
            };
        }
        else if (filters.CryptoAllowed == true)
        {
            query = query.Where(s => s.PriceCryptoStatus == ServiceAvailabilityStatus.Available);
        }

        if (filters.LinkInsertAvailability.HasValue)
        {
            query = filters.LinkInsertAvailability.Value switch
            {
                ServiceAvailabilityFilter.All => query,
                ServiceAvailabilityFilter.Available => query.Where(s => s.PriceLinkInsertStatus == ServiceAvailabilityStatus.Available),
                ServiceAvailabilityFilter.NotAvailable => query.Where(s => s.PriceLinkInsertStatus == ServiceAvailabilityStatus.NotAvailable),
                ServiceAvailabilityFilter.Unknown => query.Where(s => s.PriceLinkInsertStatus == ServiceAvailabilityStatus.Unknown),
                _ => query
            };
        }
        else if (filters.LinkInsertAllowed == true)
        {
            query = query.Where(s => s.PriceLinkInsertStatus == ServiceAvailabilityStatus.Available);
        }

        return query;
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
                ? query.OrderByDescending(s => s.PriceCasino)
                : query.OrderBy(s => s.PriceCasino),
            SortFields.PriceCrypto => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.PriceCrypto)
                : query.OrderBy(s => s.PriceCrypto),
            SortFields.PriceLinkInsert => direction == SortingDefaults.Descending
                ? query.OrderByDescending(s => s.PriceLinkInsert)
                : query.OrderBy(s => s.PriceLinkInsert),
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
}
