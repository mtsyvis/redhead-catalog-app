using Redhead.SitesCatalog.Application.Models.Analytics;

namespace Redhead.SitesCatalog.Application.Services.Analytics.ExportAnalytics;

public interface IBusinessDemandAnalyticsService
{
    Task<BusinessDemandAnalyticsDto> GetBusinessDemandAsync(
        BusinessDemandAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnalyticsClientOptionDto>> ListClientOptionsAsync(
        CancellationToken cancellationToken = default);
}
