namespace Redhead.SitesCatalog.Domain.Entities;

public class ExportLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public int RowsReturned { get; set; }
    public string? FilterSummaryJson { get; set; }
}
