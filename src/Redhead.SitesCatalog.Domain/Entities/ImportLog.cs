namespace Redhead.SitesCatalog.Domain.Entities;

public class ImportLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Sites" or "Quarantine"
    public DateTime TimestampUtc { get; set; }
    public int Inserted { get; set; }
    public int Duplicates { get; set; }
    public int Matched { get; set; }
    public int Unmatched { get; set; }
    public int ErrorsCount { get; set; }
}
