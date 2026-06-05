namespace Redhead.SitesCatalog.Domain.Entities;

public class ExportLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public int RowsReturned { get; set; }
    public int RequestedRowsCount { get; set; }
    public int ExportedRowsCount { get; set; }
    public bool WasTruncated { get; set; }
    public int? ExportLimitRows { get; set; }
    public int? DailyUniqueExportedDomainsLimit { get; set; }
    public int? WeeklyUniqueExportedDomainsLimit { get; set; }
    public int? DailyExportOperationsLimit { get; set; }
    public int? WeeklyExportOperationsLimit { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string ExportMode { get; set; } = string.Empty;
    public string? BlockedReason { get; set; }
    public ExportAnalyticsSnapshot? AnalyticsSnapshot { get; set; }
    public ICollection<ExportedDomainAccess> ExportedDomainAccesses { get; set; } = [];
}
