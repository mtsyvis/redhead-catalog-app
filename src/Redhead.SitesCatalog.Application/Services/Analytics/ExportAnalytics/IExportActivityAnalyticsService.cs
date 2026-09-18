using Redhead.SitesCatalog.Application.Models.Analytics;

namespace Redhead.SitesCatalog.Application.Services.Analytics.ExportAnalytics;

public interface IExportActivityAnalyticsService
{
    Task<ExportActivityAnalyticsDto> GetExportActivityAsync(
        ExportActivityAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<ExportLogDetailsDto?> GetExportLogDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
