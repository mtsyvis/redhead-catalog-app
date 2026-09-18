using Redhead.SitesCatalog.Application.Models.Analytics;

namespace Redhead.SitesCatalog.Application.Services.Analytics.MissingDomainsAnalytics;

public interface IMissingDomainsAnalyticsService
{
    /// <summary>Records already-normalized missing keys returned by Multi-search.</summary>
    Task RecordAsync(string userId, string role, Guid requestId,
        IReadOnlyList<string> missingDomains, CancellationToken cancellationToken = default);

    Task<MissingDomainsAnalyticsDto> GetAsync(MissingDomainsAnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
