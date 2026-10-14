using Redhead.SitesCatalog.Application.Models.Analytics;

namespace Redhead.SitesCatalog.Application.Services.Analytics.CatalogCoverageAnalytics;

public interface ICatalogCoverageAnalyticsService
{
    Task<CatalogCoverageAnalyticsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<MemoryStream> ExportDomainsWithoutOffersAsync(CancellationToken cancellationToken = default);
}
