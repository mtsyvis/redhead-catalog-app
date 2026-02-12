using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing sites operations
/// </summary>
public class SitesService : ISitesService
{
    private readonly ApplicationDbContext _context;

    public SitesService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SitesListResult> GetSitesAsync(SitesQuery query, CancellationToken cancellationToken = default)
    {
        // Start with base query
        IQueryable<Site> sitesQuery = _context.Sites;

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

        // Get total count before pagination
        var total = await sitesQuery.CountAsync(cancellationToken);

        // Apply sorting
        sitesQuery = ApplySorting(sitesQuery, query.SortBy, query.SortDir);

        // Apply pagination
        var pageSize = Math.Clamp(query.PageSize, 1, PaginationDefaults.MaxPageSize);
        var page = Math.Max(PaginationDefaults.DefaultPage, query.Page);
        var skip = (page - 1) * pageSize;

        // Execute query and map to DTOs
        var sites = await sitesQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(s => new SiteDto
            {
                Domain = s.Domain,
                DR = s.DR,
                Traffic = s.Traffic,
                Location = s.Location,
                PriceUsd = s.PriceUsd,
                PriceCasino = s.PriceCasino,
                PriceCrypto = s.PriceCrypto,
                PriceLinkInsert = s.PriceLinkInsert,
                Niche = s.Niche,
                Categories = s.Categories,
                IsQuarantined = s.IsQuarantined,
                QuarantineReason = s.QuarantineReason,
                QuarantineUpdatedAtUtc = s.QuarantineUpdatedAtUtc,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new SitesListResult
        {
            Items = sites,
            Total = total
        };
    }

    public async Task<List<string>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sites
            .Select(s => s.Location)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(cancellationToken);
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
        if (filters.CasinoAllowed == true)
        {
            query = query.Where(s => s.PriceCasino != null);
        }
        if (filters.CryptoAllowed == true)
        {
            query = query.Where(s => s.PriceCrypto != null);
        }
        if (filters.LinkInsertAllowed == true)
        {
            query = query.Where(s => s.PriceLinkInsert != null);
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
                ? query.OrderByDescending(s => s.PriceUsd)
                : query.OrderBy(s => s.PriceUsd),
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
            _ => query.OrderBy(s => s.Domain)
        };
    }
}
