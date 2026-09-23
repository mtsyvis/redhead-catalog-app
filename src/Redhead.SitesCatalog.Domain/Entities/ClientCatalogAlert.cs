namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class ClientCatalogAlert
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

    public void CloseForTrustedClient(DateTime closedAtUtc)
    {
        ReviewedAtUtc = closedAtUtc;
        // A null reviewer distinguishes this system closure from a manual review,
        // so removing trust does not inherit the manual-review cooldown.
        ReviewedByUserId = null;
        NextEmailAttemptAtUtc = null;
    }
}
