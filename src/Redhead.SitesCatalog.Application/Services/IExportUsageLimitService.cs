using Redhead.SitesCatalog.Application.Models.Exports;

namespace Redhead.SitesCatalog.Application.Services;

public interface IExportUsageLimitService
{
    Task<ExportUsageSummary> GetUsageAsync(
        string userId,
        string userRole,
        EffectiveExportPolicy policy,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<ExportUsageLimitEvaluation> EvaluateAsync(
        string userId,
        string userRole,
        EffectiveExportPolicy policy,
        IReadOnlyList<string> candidateDomains,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
