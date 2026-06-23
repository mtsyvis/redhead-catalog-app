using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;

namespace Redhead.SitesCatalog.Application.Ahrefs;

public sealed record AhrefsSyncDryRunResult(
    int EligibleSitesCount,
    int SelectedSitesCount,
    int CostPerSite,
    long FullEstimatedUnits,
    long SelectedEstimatedUnits,
    long AvailableUnits,
    int SafetyBufferUnits,
    bool CanRun,
    string? ReasonIfCannotRun,
    string TargetMode,
    string Protocol,
    string VolumeMode,
    DateOnly SnapshotMonth,
    DateTime? UsageResetDate,
    bool WasLimitedByBudget);

public sealed record AhrefsSyncRequest(
    AhrefsSyncRunKind RunKind,
    string? TriggeredByUserId,
    int? MaxSitesOverride,
    bool SaveSnapshots,
    bool Force);

public sealed record AhrefsSyncRunResult(
    bool Conflict,
    string? ConflictMessage,
    AhrefsSyncRun? Run,
    bool WaitingForUsageReset,
    DateTime? UsageResetDate)
{
    public static AhrefsSyncRunResult AlreadyRunning()
        => new(true, "An Ahrefs sync is already running.", null, false, null);

    public static AhrefsSyncRunResult Completed(AhrefsSyncRun run)
        => new(false, null, run, false, run.UsageResetDate);

    public static AhrefsSyncRunResult WaitForUsageReset(DateTime? usageResetDate)
        => new(
            false,
            "Ahrefs usage has not reset for the new period yet.",
            null,
            true,
            usageResetDate);
}

public sealed record AhrefsSyncRunDetails(
    AhrefsSyncRun Run,
    IReadOnlyList<AhrefsSyncRunItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AhrefsSyncRunsPage(
    IReadOnlyList<AhrefsSyncRun> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AhrefsSyncMonitoringData(
    AhrefsLimitsAndUsage Limits,
    DateTime LimitsCheckedAt,
    AhrefsSyncRun? ActiveRun,
    bool HasCompletedMonthlyRunForSnapshotMonth,
    bool IsWaitingForUsageReset,
    DateOnly SnapshotMonth,
    int EligibleSitesCount,
    long FullEstimatedUnits,
    long ApiKeyRemainingUnits,
    long WorkspaceRemainingUnits,
    long AppBudgetRemainingUnits,
    long EffectiveAvailableUnits,
    int SafetyBufferUnits,
    int BatchSize,
    int MaxSitesPerRun,
    string TargetMode,
    string Protocol,
    string VolumeMode);
