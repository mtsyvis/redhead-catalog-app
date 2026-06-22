namespace Redhead.SitesCatalog.Api.Models;

public sealed record AhrefsSyncDryRunRequest(int? MaxSitesOverride);

public sealed record AhrefsSyncRunRequest(
    int? MaxSitesOverride,
    bool? SaveSnapshots,
    bool Force = false);

public sealed record AhrefsSyncStatusResponse(
    bool SchedulerEnabled,
    string Cron,
    DateTime? NextScheduledRunUtc,
    bool IsDueNow,
    bool IsWaitingForUsageReset,
    DateTime? DueOccurrenceUtc,
    DateTime LimitsCheckedAt,
    DateTime? UsageResetDate,
    long ApiKeyRemainingUnits,
    long WorkspaceRemainingUnits,
    long AppBudgetRemainingUnits,
    long EffectiveAvailableUnits,
    int SafetyBufferUnits,
    long SpendableUnits,
    int EligibleSitesCount,
    long FullEstimatedUnits,
    int AffordableSitesCount,
    int PlannedSitesCount,
    long PlannedEstimatedUnits,
    bool CanStartRun,
    bool FullCatalogFitsBudget,
    long FullCatalogShortfallUnits,
    bool ConfiguredRunLimitedByBudget,
    bool ConfiguredRunLimitedByMaxSites,
    int BatchSize,
    int MaxSitesPerRun,
    string TargetMode,
    string Protocol,
    string VolumeMode,
    bool HasCompletedMonthlyRun,
    Redhead.SitesCatalog.Domain.Entities.AhrefsSyncRun? ActiveRun);
