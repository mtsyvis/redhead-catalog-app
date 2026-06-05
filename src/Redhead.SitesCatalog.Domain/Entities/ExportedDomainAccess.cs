namespace Redhead.SitesCatalog.Domain.Entities;

public class ExportedDomainAccess
{
    public Guid Id { get; set; }
    public Guid ExportLogId { get; set; }
    public ExportLog ExportLog { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime ExportedAtUtc { get; set; }
}
