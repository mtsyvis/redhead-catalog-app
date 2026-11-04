namespace Redhead.SitesCatalog.Application.LinkDetective;

internal static class LinkDetectiveSyncMessages
{
    public const string InterruptedRequest =
        "The last request was interrupted; its quota charge is unknown. Saved prices have been kept.";
    public const string QuotaPoolMismatch =
        "Preview and domain quota pools differ. Background checks are postponed; verify the provider quota.";
    public const string ReservedQuota = "The remaining provider quota is reserved for on-demand requests.";
    public const string MissingBatchQuota =
        "The provider did not report the domain batch quota. Background checks are postponed.";
    public const string WaitingForRetry = "Waiting for the next permitted retry.";
}
