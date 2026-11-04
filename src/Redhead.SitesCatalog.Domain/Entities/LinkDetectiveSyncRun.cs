namespace Redhead.SitesCatalog.Domain.Entities;

// Numeric values are part of the monitoring API contract, even though persistence uses names.
public enum LinkDetectiveSyncRunStatus { Running = 0, Waiting = 1, Completed = 2, CompletedWithErrors = 3 }
public enum LinkDetectiveSyncItemStatus { Pending = 0, InFlight = 1, PricesFound = 2, NotFound = 3, Failed = 4, Reused = 5, Skipped = 6 }

// TODO(TD-001): Add retention for completed runs/items; preserve active work and the shared price cache.
// See docs/technical-debt.md. Cleanup is deferred for the current delivery.
public sealed class LinkDetectiveSyncRun
{
    public Guid Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public LinkDetectiveSyncRunStatus Status { get; set; }
    public int SelectedCount { get; set; }
    public int ProcessedCount { get; set; }
    public int PricesFoundCount { get; set; }
    public int NotFoundCount { get; set; }
    public int FailedCount { get; set; }
    public int ReusedCount { get; set; }
    public int SkippedCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? WaitingReason { get; set; }
}

// Durable per-domain work while the run is active, then run diagnostics. Contains no price values.
public sealed class LinkDetectiveSyncRunItem
{
    public Guid RunId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public LinkDetectiveSyncItemStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime? AttemptedAtUtc { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? Error { get; set; }
}

// One row records the scheduler and last observed provider quota, including on-demand calls.
public sealed class LinkDetectiveSyncState
{
    public int Id { get; set; } = 1;
    public DateTime? NextSelectionAtUtc { get; set; }
    public long? QuotaLimit { get; set; }
    public long? QuotaUsed { get; set; }
    public long? QuotaRemaining { get; set; }
    public string? QuotaPeriod { get; set; }
    public DateTime? QuotaResetAtUtc { get; set; }
    public DateTime? QuotaObservedAtUtc { get; set; }
}
