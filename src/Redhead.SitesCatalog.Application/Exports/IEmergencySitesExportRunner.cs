namespace Redhead.SitesCatalog.Application.Exports;

public interface IEmergencySitesExportRunner
{
    Task<EmergencySitesExportRunResult> RunOnceAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default);
}
