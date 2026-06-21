using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

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
    AhrefsSyncRun? Run)
{
    public static AhrefsSyncRunResult AlreadyRunning()
        => new(true, "An Ahrefs sync is already running.", null);

    public static AhrefsSyncRunResult Completed(AhrefsSyncRun run)
        => new(false, null, run);
}

public sealed record AhrefsSyncRunDetails(
    AhrefsSyncRun Run,
    IReadOnlyList<AhrefsSyncRunItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
