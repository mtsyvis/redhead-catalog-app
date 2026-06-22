namespace Redhead.SitesCatalog.Application.Ahrefs;

public interface IAhrefsSyncService
{
    Task<AhrefsSyncDryRunResult> DryRunAsync(
        int? maxSitesOverride,
        CancellationToken cancellationToken);

    Task<AhrefsSyncRunResult> RunAsync(
        AhrefsSyncRequest request,
        CancellationToken cancellationToken);

    Task<AhrefsSyncRunsPage> ListRunsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AhrefsSyncRunDetails?> GetRunAsync(
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AhrefsSyncMonitoringData> GetMonitoringDataAsync(
        bool refreshLimits,
        CancellationToken cancellationToken);
}
