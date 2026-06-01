namespace Redhead.SitesCatalog.Application.Exports;

public interface IEmergencySitesExportService
{
    Task<EmergencySitesExportResult> GenerateAsync(CancellationToken cancellationToken = default);
}
