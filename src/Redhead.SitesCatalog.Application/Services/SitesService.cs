using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
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
                LinkType = s.LinkType,
                SponsoredTag = s.SponsoredTag,
                PriceUsd = s.PriceUsd,
                PriceCasino = s.PriceCasino,
                PriceCasinoStatus = s.PriceCasinoStatus,
                PriceCrypto = s.PriceCrypto,
                PriceCryptoStatus = s.PriceCryptoStatus,
                PriceLinkInsert = s.PriceLinkInsert,
                PriceLinkInsertStatus = s.PriceLinkInsertStatus,
                Niche = s.Niche,
                Categories = s.Categories,
                IsQuarantined = s.IsQuarantined,
                QuarantineReason = s.QuarantineReason,
                QuarantineUpdatedAtUtc = s.QuarantineUpdatedAtUtc,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                LastPublishedDate = s.LastPublishedDate,
                LastPublishedDateIsMonthOnly = s.LastPublishedDateIsMonthOnly
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
                LinkType = s.LinkType,
                SponsoredTag = s.SponsoredTag,
                PriceUsd = s.PriceUsd,
                PriceCasino = s.PriceCasino,
                PriceCasinoStatus = s.PriceCasinoStatus,
                PriceCrypto = s.PriceCrypto,
                PriceCryptoStatus = s.PriceCryptoStatus,
                PriceLinkInsert = s.PriceLinkInsert,
                PriceLinkInsertStatus = s.PriceLinkInsertStatus,
                Niche = s.Niche,
                Categories = s.Categories,
                IsQuarantined = s.IsQuarantined,
                QuarantineReason = s.QuarantineReason,
                QuarantineUpdatedAtUtc = s.QuarantineUpdatedAtUtc,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                LastPublishedDate = s.LastPublishedDate,
                LastPublishedDateIsMonthOnly = s.LastPublishedDateIsMonthOnly
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

    public async Task<SiteDto?> UpdateSiteAsync(string domain, UpdateSiteRequest request, CancellationToken cancellationToken = default)
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
        site.DR = request.DR;
        site.Traffic = request.Traffic;
        site.Location = request.Location.Trim();
        site.LinkType = string.IsNullOrWhiteSpace(request.LinkType) ? null : request.LinkType.Trim();
        site.SponsoredTag = string.IsNullOrWhiteSpace(request.SponsoredTag) ? null : request.SponsoredTag.Trim();
        site.PriceUsd = request.PriceUsd;
        site.PriceCasino = request.PriceCasino;
        site.PriceCasinoStatus = request.PriceCasinoStatus;
        site.PriceCrypto = request.PriceCrypto;
        site.PriceCryptoStatus = request.PriceCryptoStatus;
        site.PriceLinkInsert = request.PriceLinkInsert;
        site.PriceLinkInsertStatus = request.PriceLinkInsertStatus;
        site.Niche = string.IsNullOrWhiteSpace(request.Niche) ? null : request.Niche.Trim();
        site.Categories = string.IsNullOrWhiteSpace(request.Categories) ? null : request.Categories.Trim();
        site.IsQuarantined = request.IsQuarantined;
        site.QuarantineReason = request.IsQuarantined ? (string.IsNullOrWhiteSpace(request.QuarantineReason) ? null : request.QuarantineReason.Trim()) : null;
        site.QuarantineUpdatedAtUtc = request.IsQuarantined ? now : null;
        site.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new SiteDto
        {
            Domain = site.Domain,
            DR = site.DR,
            Traffic = site.Traffic,
            Location = site.Location,
            LinkType = site.LinkType,
            SponsoredTag = site.SponsoredTag,
            PriceUsd = site.PriceUsd,
            PriceCasino = site.PriceCasino,
            PriceCasinoStatus = site.PriceCasinoStatus,
            PriceCrypto = site.PriceCrypto,
            PriceCryptoStatus = site.PriceCryptoStatus,
            PriceLinkInsert = site.PriceLinkInsert,
            PriceLinkInsertStatus = site.PriceLinkInsertStatus,
            Niche = site.Niche,
            Categories = site.Categories,
            IsQuarantined = site.IsQuarantined,
            QuarantineReason = site.QuarantineReason,
            QuarantineUpdatedAtUtc = site.QuarantineUpdatedAtUtc,
            CreatedAtUtc = site.CreatedAtUtc,
            UpdatedAtUtc = site.UpdatedAtUtc,
            LastPublishedDate = site.LastPublishedDate,
            LastPublishedDateIsMonthOnly = site.LastPublishedDateIsMonthOnly
        };
    }
}
