namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class ClientCatalogAutoBan
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; }
    public int UniqueSites { get; set; }
    public int Threshold { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
    public DateTime? NextEmailAttemptAtUtc { get; set; }
}
