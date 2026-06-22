namespace Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;

public interface IAhrefsApiClient
{
    Task<AhrefsLimitsAndUsage> GetLimitsAndUsageAsync(CancellationToken cancellationToken);

    Task<AhrefsBatchResult> RunBatchAnalysisAsync(
        IReadOnlyList<AhrefsBatchTarget> targets,
        string volumeMode,
        CancellationToken cancellationToken);
}

public interface IAhrefsLimitsProvider
{
    Task<AhrefsLimitsSnapshot> GetAsync(
        bool forceRefresh,
        CancellationToken cancellationToken);
}
