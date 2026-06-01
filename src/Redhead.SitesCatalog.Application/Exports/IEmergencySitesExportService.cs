namespace Redhead.SitesCatalog.Application.Exports;

public interface IEmergencySitesExportService
{
    Task<EmergencySitesExportResult> GenerateAsync(CancellationToken cancellationToken = default);

    Task<EmergencySitesExportResult> GenerateAsync(
        string filePrefix,
        CancellationToken cancellationToken = default);
}
