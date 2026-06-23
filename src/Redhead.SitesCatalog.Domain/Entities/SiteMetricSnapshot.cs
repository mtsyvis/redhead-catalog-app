using System.Text.Json.Serialization;

namespace Redhead.SitesCatalog.Domain.Entities;

public class SiteMetricSnapshot
{
    public Guid Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public DateOnly SnapshotDate { get; set; }
    public long Traffic { get; set; }
    public double DomainRating { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? AhrefsSyncRunId { get; set; }
    public DateTime FetchedAt { get; set; }
    [JsonIgnore]
    public AhrefsSyncRun? AhrefsSyncRun { get; set; }
}
