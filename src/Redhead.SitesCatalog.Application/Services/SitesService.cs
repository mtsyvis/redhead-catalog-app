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
    private readonly ISitesQueryBuilder _queryBuilder;

    public SitesService(ApplicationDbContext context, ISitesQueryBuilder queryBuilder)
    {
        _context = context;
        _queryBuilder = queryBuilder;
    }

    public async Task<SitesListResult> GetSitesAsync(SitesQuery query, CancellationToken cancellationToken = default)
    {
        // Build filtered and sorted query using the query builder
        var sitesQuery = _queryBuilder.BuildQuery(_context.Sites, query);

        // Get total count before pagination
        var total = await sitesQuery.CountAsync(cancellationToken);

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

    public async Task<MultiSearchSitesResult> MultiSearchSitesAsync(
        IReadOnlyList<string> normalizedDomains,
        IReadOnlyList<string> duplicates,
        CancellationToken cancellationToken = default)
    {
        if (normalizedDomains.Count == 0)
        {
            return new MultiSearchSitesResult
            {
                Found = [],
                NotFound = [],
                Duplicates = duplicates.ToList()
            };
        }

        var found = await _context.Sites
            .Where(s => normalizedDomains.Contains(s.Domain))
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

        var foundDomains = new HashSet<string>(found.Select(s => s.Domain), StringComparer.Ordinal);
        var notFound = normalizedDomains.Where(d => !foundDomains.Contains(d)).ToList();

        return new MultiSearchSitesResult
        {
            Found = found,
            NotFound = notFound,
            Duplicates = duplicates.ToList()
        };
    }

    public async Task<SiteDto?> UpdateQuarantineAsync(
        string domain,
        bool isQuarantined,
        string? quarantineReason,
        CancellationToken cancellationToken = default)
    {
        var normalized = DomainNormalizer.Normalize(domain);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        var site = await _context.Sites.FirstOrDefaultAsync(s => s.Domain == normalized, cancellationToken);
        if (site == null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        site.IsQuarantined = isQuarantined;
        site.QuarantineReason = isQuarantined ? (string.IsNullOrWhiteSpace(quarantineReason) ? null : quarantineReason.Trim()) : null;
        site.QuarantineUpdatedAtUtc = isQuarantined ? now : null;
        site.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new SiteDto
        {
            Domain = site.Domain,
            DR = site.DR,
            Traffic = site.Traffic,
            Location = site.Location,
            PriceUsd = site.PriceUsd,
            PriceCasino = site.PriceCasino,
            PriceCrypto = site.PriceCrypto,
            PriceLinkInsert = site.PriceLinkInsert,
            Niche = site.Niche,
            Categories = site.Categories,
            IsQuarantined = site.IsQuarantined,
            QuarantineReason = site.QuarantineReason,
            QuarantineUpdatedAtUtc = site.QuarantineUpdatedAtUtc,
            CreatedAtUtc = site.CreatedAtUtc,
            UpdatedAtUtc = site.UpdatedAtUtc
        };
    }
}
