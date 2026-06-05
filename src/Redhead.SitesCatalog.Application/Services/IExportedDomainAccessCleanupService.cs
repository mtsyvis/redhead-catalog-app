namespace Redhead.SitesCatalog.Application.Services;

public interface IExportedDomainAccessCleanupService
{
    Task<int> DeleteOldAccessesAsync(
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
}
