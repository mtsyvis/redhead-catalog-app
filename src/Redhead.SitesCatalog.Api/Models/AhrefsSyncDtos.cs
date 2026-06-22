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
    DateTime? DueOccurrenceUtc,
    DateTime LimitsCheckedAt,
    DateTime? UsageResetDate,
    long ApiKeyRemainingUnits,
    long WorkspaceRemainingUnits,
    long AppBudgetRemainingUnits,
    long EffectiveAvailableUnits,
    int SafetyBufferUnits,
    int EligibleSitesCount,
    long FullEstimatedUnits,
    int BatchSize,
    int MaxSitesPerRun,
    string TargetMode,
    string Protocol,
    string VolumeMode,
    Redhead.SitesCatalog.Domain.Entities.AhrefsSyncRun? ActiveRun);
