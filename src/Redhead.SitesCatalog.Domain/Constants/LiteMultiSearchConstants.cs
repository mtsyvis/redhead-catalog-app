namespace Redhead.SitesCatalog.Domain.Constants;

public static class LiteMultiSearchConstants
{
    public const int MaxDomainsPerRequest = 50;
    public const int MonthlyDomainLimit = 1000;

    public const string PerRequestLimitMessage =
        "Lite users can check at most 50 unique domains per request.";

    public const string MonthlyLimitMessage =
        "Monthly check limit reached.";
}
