using Redhead.SitesCatalog.Domain.Enums;
using System.Text.Json.Serialization;

namespace Redhead.SitesCatalog.Domain.Entities;

public class AhrefsSyncRun
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public AhrefsSyncRunStatus Status { get; set; }
    public AhrefsSyncRunKind RunKind { get; set; }
    public string? TriggeredByUserId { get; set; }
    public bool Force { get; set; }
    public bool IsFullCoverage { get; set; }
    public bool WasLimitedByBudget { get; set; }
    public DateOnly SnapshotMonth { get; set; }
    public DateTime? UsageResetDate { get; set; }
    public int EligibleSitesCount { get; set; }
    public int SelectedSitesCount { get; set; }
    public int ProcessedSitesCount { get; set; }
    public int UpdatedSitesCount { get; set; }
    public int FailedSitesCount { get; set; }
    public int SkippedSitesCount { get; set; }
    public int CostPerSite { get; set; }
    public long FullEstimatedUnits { get; set; }
    public long SelectedEstimatedUnits { get; set; }
    public long ActualUnits { get; set; }
    public long AvailableUnitsBefore { get; set; }
    public long? AvailableUnitsAfter { get; set; }
    public int SafetyBufferUnits { get; set; }
    public int StopIfRemainingUnitsBelow { get; set; }
    public int BatchSize { get; set; }
    public int MaxSitesPerRun { get; set; }
    public string TargetMode { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string VolumeMode { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    [JsonIgnore]
    public ICollection<AhrefsSyncRunItem> Items { get; set; } = [];

    [JsonIgnore]
    public ICollection<SiteMetricSnapshot> Snapshots { get; set; } = [];
}
