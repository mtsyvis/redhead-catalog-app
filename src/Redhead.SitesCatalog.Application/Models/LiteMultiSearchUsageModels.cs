namespace Redhead.SitesCatalog.Application.Models;

public enum LiteMultiSearchUsageStatus
{
    Allowed,
    RequestLimitExceeded,
    MonthlyLimitExceeded
}

public sealed record LiteMultiSearchUsageResult(
    LiteMultiSearchUsageStatus Status,
    int DomainsRequested,
    int DomainsUsed,
    int MonthlyLimit,
    int RemainingAfterRequest)
{
    public bool Allowed => Status == LiteMultiSearchUsageStatus.Allowed;
}
