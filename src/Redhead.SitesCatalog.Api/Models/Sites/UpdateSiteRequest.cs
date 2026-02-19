namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Request to update site quarantine status (Commit 11). When IsQuarantined is false, reason is cleared.
/// </summary>
public class UpdateSiteRequest
{
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
}
