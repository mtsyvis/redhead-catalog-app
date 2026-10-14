using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.Analytics.CatalogCoverageAnalytics;

public sealed class CatalogCoverageAnalyticsService(ApplicationDbContext context) : ICatalogCoverageAnalyticsService
{
    public async Task<CatalogCoverageAnalyticsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // Aggregate catalog domains rather than joining offers, which would count a site more than once.
        var sites = await context.Sites.AsNoTracking()
            .Select(site => new
            {
                site.IsQuarantined,
                HasOffers = context.SiteWebmasterOffers.Any(offer => offer.SiteDomain == site.Domain),
                HasOnlyInactiveOffers = context.SiteWebmasterOffers.Any(offer => offer.SiteDomain == site.Domain)
                    && !context.SiteWebmasterOffers.Any(offer => offer.SiteDomain == site.Domain
                        && offer.Status != SiteWebmasterOfferStatus.Inactive)
            })
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.LongCount(),
                Covered = group.LongCount(site => site.HasOffers),
                OnlyInactive = group.LongCount(site => site.HasOnlyInactiveOffers),
                Quarantined = group.LongCount(site => site.IsQuarantined),
                QuarantinedWithoutOffers = group.LongCount(site => site.IsQuarantined && !site.HasOffers)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var offers = await context.SiteWebmasterOffers.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.LongCount(),
                Active = group.LongCount(offer => offer.Status == SiteWebmasterOfferStatus.Active),
                Inactive = group.LongCount(offer => offer.Status == SiteWebmasterOfferStatus.Inactive)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalSites = sites?.Total ?? 0;
        var coveredSites = sites?.Covered ?? 0;
        return new CatalogCoverageAnalyticsDto(
            totalSites, coveredSites, totalSites - coveredSites,
            totalSites == 0 ? 0 : decimal.Round(100m * coveredSites / totalSites, 2),
            offers?.Total ?? 0, offers?.Active ?? 0, offers?.Inactive ?? 0,
            sites?.OnlyInactive ?? 0, sites?.Quarantined ?? 0, sites?.QuarantinedWithoutOffers ?? 0,
            DateTime.UtcNow);
    }

    public async Task<MemoryStream> ExportDomainsWithoutOffersAsync(CancellationToken cancellationToken = default)
    {
        var domains = await SitesWithoutOffers()
            .Select(site => site.Domain)
            .Distinct()
            .OrderBy(domain => domain)
            .ToListAsync(cancellationToken);

        return XlsxWorkbookWriter.CreateWorkbook(
        [
            new XlsxSheet("Domains without offers", ["Domain"],
                domains.Select(domain => (IReadOnlyList<XlsxCell>)[XlsxCell.Text(domain)]).ToList(), [48d])
        ]);
    }

    private IQueryable<Site> SitesWithoutOffers() => context.Sites.AsNoTracking()
        .Where(site => !context.SiteWebmasterOffers.Any(offer => offer.SiteDomain == site.Domain));
}
