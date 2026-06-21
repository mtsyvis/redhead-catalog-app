namespace Redhead.SitesCatalog.Application.Ahrefs;

public interface IAhrefsSyncService
{
    Task<AhrefsSyncDryRunResult> DryRunAsync(
        int? maxSitesOverride,
        CancellationToken cancellationToken);

    Task<AhrefsSyncRunResult> RunAsync(
        AhrefsSyncRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Domain.Entities.AhrefsSyncRun>> ListRunsAsync(
        int take,
        CancellationToken cancellationToken);

    Task<AhrefsSyncRunDetails?> GetRunAsync(
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
