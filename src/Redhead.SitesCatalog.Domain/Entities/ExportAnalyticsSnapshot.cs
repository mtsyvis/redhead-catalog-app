namespace Redhead.SitesCatalog.Domain.Entities;

public class ExportAnalyticsSnapshot
{
    public Guid Id { get; set; }
    public Guid ExportLogId { get; set; }
    public ExportLog ExportLog { get; set; } = null!;
    public int SnapshotVersion { get; set; }
    public string FiltersSnapshotJson { get; set; } = string.Empty;
    public string SortSnapshotJson { get; set; } = string.Empty;
    public string? SearchSnapshotJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
