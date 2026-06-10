using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing sites operations
/// </summary>
public class SitesService : ISitesService
{
    private readonly ApplicationDbContext _context;
    private readonly ISitesQueryBuilder _queryBuilder;
    private readonly INicheFilterOptionsCache _nicheFilterOptionsCache;
    private readonly ILocationNormalizer _locationNormalizer;

    public SitesService(
        ApplicationDbContext context,
        ISitesQueryBuilder queryBuilder,
        INicheFilterOptionsCache nicheFilterOptionsCache,
        ILocationNormalizer locationNormalizer)
    {
        _context = context;
        _queryBuilder = queryBuilder;
        _nicheFilterOptionsCache = nicheFilterOptionsCache;
        _locationNormalizer = locationNormalizer;
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
            .Include(s => s.CanonicalLocation)
            .Skip(skip)
            .Take(pageSize)
            .Select(s => new SiteDto
            {
                Domain = s.Domain,
                DR = s.DR,
                Traffic = s.Traffic,
                Location = s.LocationKey == null
                    ? LocationDisplayFormatter.OtherDisplayName
                    : s.CanonicalLocation != null
                        ? s.CanonicalLocation.DisplayName
                        : s.LocationKey == LocationConstants.UnknownLocationKey
                            ? LocationDisplayFormatter.UnknownDisplayName
                            : s.Location,
                ImportedLocationRaw = s.ImportedLocationRaw,
                Language = s.Language,
                SponsoredTag = s.SponsoredTag,
                PriceUsd = s.PriceUsd,
                PriceCasino = s.PriceCasino,
                PriceCasinoStatus = s.PriceCasinoStatus,
                PriceCrypto = s.PriceCrypto,
                PriceCryptoStatus = s.PriceCryptoStatus,
                PriceLinkInsert = s.PriceLinkInsert,
                PriceLinkInsertStatus = s.PriceLinkInsertStatus,
                PriceLinkInsertCasino = s.PriceLinkInsertCasino,
                PriceLinkInsertCasinoStatus = s.PriceLinkInsertCasinoStatus,
                PriceDating = s.PriceDating,
                PriceDatingStatus = s.PriceDatingStatus,
                NumberDFLinks = s.NumberDFLinks,
                TermType = s.TermType,
                TermValue = s.TermValue,
                TermUnit = s.TermUnit,
                Niche = s.Niche,
                NicheTokens = s.NicheTokens,
                Categories = s.Categories,
                IsQuarantined = s.IsQuarantined,
                QuarantineReason = s.QuarantineReason,
                QuarantineUpdatedAtUtc = s.QuarantineUpdatedAtUtc,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                CreatedBy = s.CreatedBy,
                UpdatedBy = s.UpdatedBy,
                LastPublishedDate = s.LastPublishedDate,
                LastPublishedDateIsMonthOnly = s.LastPublishedDateIsMonthOnly
            })
            .ToListAsync(cancellationToken);

        await AttachPricingAsync(sites, cancellationToken);

        return new SitesListResult
        {
            Items = sites,
            Total = total
        };
    }

    public async Task<List<string>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CanonicalLocations
            .AsNoTracking()
            .Where(location => location.IsActive)
            .OrderBy(location => location.SortOrder)
            .ThenBy(location => location.DisplayName)
            .Select(location => location.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationFilterOptionsDto> GetLocationFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var groupEntities = await _context.LocationGroups
            .AsNoTracking()
            .Include(group => group.Items)
            .ThenInclude(item => item.Location)
            .OrderBy(group => group.Kind)
            .ThenBy(group => group.SortOrder)
            .ThenBy(group => group.DisplayName)
            .ToListAsync(cancellationToken);

        var groups = groupEntities
            .Select(group =>
            {
                var groupLocations = group.Items
                    .Select(item => item.Location)
                    .Where(location => location is { IsActive: true })
                    .OrderBy(location => location!.SortOrder)
                    .ThenBy(location => location!.DisplayName)
                    .Select(location => new LocationFilterOptionDto
                    {
                        Key = location!.Key,
                        DisplayName = location.DisplayName
                    })
                    .ToList();

                return new LocationGroupFilterOptionDto
                {
                    Key = group.Key,
                    DisplayName = group.DisplayName,
                    GroupType = group.Kind,
                    LocationCount = groupLocations.Count,
                    Locations = groupLocations
                };
            })
            .ToList();

        var locations = await _context.CanonicalLocations
            .AsNoTracking()
            .Where(location => location.IsActive)
            .OrderBy(location => location.SortOrder)
            .ThenBy(location => location.DisplayName)
            .Select(location => new LocationFilterOptionDto
            {
                Key = location.Key,
                DisplayName = location.DisplayName
            })
            .ToListAsync(cancellationToken);

        var unknown = locations.SingleOrDefault(location =>
            string.Equals(location.Key, LocationConstants.UnknownLocationKey, StringComparison.Ordinal))
            ?? new LocationFilterOptionDto
            {
                Key = LocationConstants.UnknownLocationKey,
                DisplayName = LocationDisplayFormatter.UnknownDisplayName
            };

        var hasOtherLocations = await _context.Sites
            .AsNoTracking()
            .AnyAsync(site => site.LocationKey == null, cancellationToken);

        return new LocationFilterOptionsDto
        {
            Groups = groups,
            Locations = locations,
            Special = new LocationSpecialFilterOptionsDto
            {
                Unknown = unknown,
                Other = hasOtherLocations
                    ? new LocationFilterOptionDto
                {
                    Key = LocationDisplayFormatter.OtherPseudoKey,
                    DisplayName = LocationDisplayFormatter.OtherDisplayName
                }
                    : null
            }
        };
    }

    public async Task<List<FilterOptionDto>> GetNicheOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _nicheFilterOptionsCache.GetOptionsAsync(cancellationToken);
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
            .Include(s => s.CanonicalLocation)
            .Where(s => normalizedDomains.Contains(s.Domain))
            .Select(s => new SiteDto
            {
                Domain = s.Domain,
                DR = s.DR,
                Traffic = s.Traffic,
                Location = s.LocationKey == null
                    ? LocationDisplayFormatter.OtherDisplayName
                    : s.CanonicalLocation != null
                        ? s.CanonicalLocation.DisplayName
                        : s.LocationKey == LocationConstants.UnknownLocationKey
                            ? LocationDisplayFormatter.UnknownDisplayName
                            : s.Location,
                ImportedLocationRaw = s.ImportedLocationRaw,
                Language = s.Language,
                SponsoredTag = s.SponsoredTag,
                PriceUsd = s.PriceUsd,
                PriceCasino = s.PriceCasino,
                PriceCasinoStatus = s.PriceCasinoStatus,
                PriceCrypto = s.PriceCrypto,
                PriceCryptoStatus = s.PriceCryptoStatus,
                PriceLinkInsert = s.PriceLinkInsert,
                PriceLinkInsertStatus = s.PriceLinkInsertStatus,
                PriceLinkInsertCasino = s.PriceLinkInsertCasino,
                PriceLinkInsertCasinoStatus = s.PriceLinkInsertCasinoStatus,
                PriceDating = s.PriceDating,
                PriceDatingStatus = s.PriceDatingStatus,
                NumberDFLinks = s.NumberDFLinks,
                TermType = s.TermType,
                TermValue = s.TermValue,
                TermUnit = s.TermUnit,
                Niche = s.Niche,
                NicheTokens = s.NicheTokens,
                Categories = s.Categories,
                IsQuarantined = s.IsQuarantined,
                QuarantineReason = s.QuarantineReason,
                QuarantineUpdatedAtUtc = s.QuarantineUpdatedAtUtc,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                CreatedBy = s.CreatedBy,
                UpdatedBy = s.UpdatedBy,
                LastPublishedDate = s.LastPublishedDate,
                LastPublishedDateIsMonthOnly = s.LastPublishedDateIsMonthOnly
            })
            .ToListAsync(cancellationToken);

        await AttachPricingAsync(found, cancellationToken);

        var inputOrder = normalizedDomains
            .Select((domain, index) => new { domain, index })
            .ToDictionary(item => item.domain, item => item.index, StringComparer.Ordinal);
        found = found
            .OrderBy(site => inputOrder.GetValueOrDefault(site.Domain, int.MaxValue))
            .ToList();

        var foundDomains = new HashSet<string>(found.Select(s => s.Domain), StringComparer.Ordinal);
        var notFound = normalizedDomains.Where(d => !foundDomains.Contains(d)).ToList();

        return new MultiSearchSitesResult
        {
            Found = found,
            NotFound = notFound,
            Duplicates = duplicates.ToList()
        };
    }

    public async Task<SiteDto?> UpdateSiteAsync(
        string domain,
        UpdateSiteRequest request,
        string? userEmail,
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
        var location = _locationNormalizer.Normalize(request.Location);
        site.DR = request.DR;
        site.Traffic = request.Traffic;
        site.Location = location.RawValue ?? string.Empty;
        site.LocationKey = location.LocationKey;
        site.ImportedLocationRaw = location.RawValue;
        site.Language = request.Language;
        site.SponsoredTag = request.SponsoredTag;
        site.PriceUsd = request.PriceUsd;
        site.PriceCasino = request.PriceCasino;
        site.PriceCasinoStatus = request.PriceCasinoStatus;
        site.PriceCrypto = request.PriceCrypto;
        site.PriceCryptoStatus = request.PriceCryptoStatus;
        site.PriceLinkInsert = request.PriceLinkInsert;
        site.PriceLinkInsertStatus = request.PriceLinkInsertStatus;
        site.PriceLinkInsertCasino = request.PriceLinkInsertCasino;
        site.PriceLinkInsertCasinoStatus = request.PriceLinkInsertCasinoStatus;
        site.PriceDating = request.PriceDating;
        site.PriceDatingStatus = request.PriceDatingStatus;
        site.NumberDFLinks = request.NumberDFLinks;
        site.TermType = request.TermType;
        site.TermValue = request.TermValue;
        site.TermUnit = request.TermUnit;
        site.Niche = request.Niche;
        site.NicheTokens = NicheNormalizer.NormalizeTokens(request.Niche);
        site.Categories = request.Categories;
        site.IsQuarantined = request.IsQuarantined;
        site.QuarantineReason = request.IsQuarantined ? request.QuarantineReason : null;
        site.QuarantineUpdatedAtUtc = request.IsQuarantined ? now : null;
        site.UpdatedAtUtc = now;
        site.UpdatedBy = AuditUserFormatter.Format(userEmail);
        await _context.SaveChangesAsync(cancellationToken);
        _nicheFilterOptionsCache.Invalidate();

        var dto = new SiteDto
        {
            Domain = site.Domain,
            DR = site.DR,
            Traffic = site.Traffic,
            Location = LocationDisplayFormatter.Format(site.LocationKey, site.CanonicalLocation?.DisplayName, site.Location),
            ImportedLocationRaw = site.ImportedLocationRaw,
            Language = site.Language,
            SponsoredTag = site.SponsoredTag,
            PriceUsd = site.PriceUsd,
            PriceCasino = site.PriceCasino,
            PriceCasinoStatus = site.PriceCasinoStatus,
            PriceCrypto = site.PriceCrypto,
            PriceCryptoStatus = site.PriceCryptoStatus,
            PriceLinkInsert = site.PriceLinkInsert,
            PriceLinkInsertStatus = site.PriceLinkInsertStatus,
            PriceLinkInsertCasino = site.PriceLinkInsertCasino,
            PriceLinkInsertCasinoStatus = site.PriceLinkInsertCasinoStatus,
            PriceDating = site.PriceDating,
            PriceDatingStatus = site.PriceDatingStatus,
            NumberDFLinks = site.NumberDFLinks,
            TermType = site.TermType,
            TermValue = site.TermValue,
            TermUnit = site.TermUnit,
            Niche = site.Niche,
            NicheTokens = site.NicheTokens,
            Categories = site.Categories,
            IsQuarantined = site.IsQuarantined,
            QuarantineReason = site.QuarantineReason,
            QuarantineUpdatedAtUtc = site.QuarantineUpdatedAtUtc,
            CreatedAtUtc = site.CreatedAtUtc,
            UpdatedAtUtc = site.UpdatedAtUtc,
            CreatedBy = site.CreatedBy,
            UpdatedBy = site.UpdatedBy,
            LastPublishedDate = site.LastPublishedDate,
            LastPublishedDateIsMonthOnly = site.LastPublishedDateIsMonthOnly
        };

        await AttachPricingAsync(new List<SiteDto> { dto }, cancellationToken);

        return dto;
    }

    private async Task AttachPricingAsync(IReadOnlyList<SiteDto> sites, CancellationToken cancellationToken)
    {
        if (sites.Count == 0)
        {
            return;
        }

        var domains = sites
            .Select(site => site.Domain)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var priceOptions = await _context.SitePriceOptions
            .AsNoTracking()
            .Where(priceOption => domains.Contains(priceOption.SiteDomain))
            .ToListAsync(cancellationToken);

        var serviceAvailabilities = await _context.SiteServiceAvailabilities
            .AsNoTracking()
            .Where(availability => domains.Contains(availability.SiteDomain))
            .ToListAsync(cancellationToken);

        var priceOptionsByDomain = priceOptions
            .OrderBy(priceOption => priceOption.PriceType)
            .ThenBy(GetTermSortOrder)
            .ThenBy(priceOption => priceOption.TermValue)
            .ThenBy(priceOption => priceOption.AmountUsd)
            .ToLookup(priceOption => priceOption.SiteDomain, StringComparer.Ordinal);

        var serviceAvailabilitiesByDomain = serviceAvailabilities
            .OrderBy(availability => availability.ServiceType)
            .ToLookup(availability => availability.SiteDomain, StringComparer.Ordinal);

        foreach (var site in sites)
        {
            site.Pricing = new SitePricingDto
            {
                Prices = priceOptionsByDomain[site.Domain]
                    .Select(MapPriceOptionDto)
                    .ToList(),
                ServiceAvailabilities = serviceAvailabilitiesByDomain[site.Domain]
                    .Select(MapServiceAvailabilityDto)
                    .ToList()
            };
        }
    }

    private static SitePriceOptionDto MapPriceOptionDto(SitePriceOption priceOption)
    {
        return new SitePriceOptionDto
        {
            PriceType = priceOption.PriceType,
            TermKey = priceOption.TermKey,
            TermType = priceOption.TermType,
            TermValue = priceOption.TermValue,
            TermUnit = priceOption.TermUnit,
            TermLabel = PricingTerm.FormatLabel(
                priceOption.TermKey,
                priceOption.TermType,
                priceOption.TermValue,
                priceOption.TermUnit),
            AmountUsd = priceOption.AmountUsd
        };
    }

    private static SiteServiceAvailabilityDto MapServiceAvailabilityDto(SiteServiceAvailability availability)
    {
        return new SiteServiceAvailabilityDto
        {
            ServiceType = availability.ServiceType,
            Status = availability.Status
        };
    }

    private static int GetTermSortOrder(SitePriceOption priceOption)
        => priceOption.TermKey switch
        {
            PricingTerm.UnknownKey => 0,
            _ when priceOption.TermType == TermType.Finite => 1,
            PricingTerm.PermanentKey => 2,
            _ => 3
        };
}
