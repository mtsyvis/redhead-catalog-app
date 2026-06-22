using Redhead.SitesCatalog.Domain.Enums;
using System.Text.Json.Serialization;

namespace Redhead.SitesCatalog.Domain.Entities;

public class AhrefsSyncRunItem
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public AhrefsSyncRunItemStatus Status { get; set; }
    public long OldTraffic { get; set; }
    public long? NewTraffic { get; set; }
    public double OldDomainRating { get; set; }
    public double? NewDomainRating { get; set; }
    public DateOnly SnapshotMonth { get; set; }
    public bool SnapshotSaved { get; set; }
    public int? AhrefsIndex { get; set; }
    public string? ErrorMessage { get; set; }
    [JsonIgnore]
    public AhrefsSyncRun Run { get; set; } = null!;
}
