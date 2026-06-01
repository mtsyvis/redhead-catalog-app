using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class SystemJobRun
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string PeriodKey { get; set; } = string.Empty;
    public SystemJobRunStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<SystemJobArtifact> Artifacts { get; set; } = [];
}
