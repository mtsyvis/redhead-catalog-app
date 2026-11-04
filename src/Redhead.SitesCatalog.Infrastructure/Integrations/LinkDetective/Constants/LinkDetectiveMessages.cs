namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Constants;

public static class LinkDetectiveMessages
{
    public const string InvalidDomainBatch = "Provide at least one valid domain.";

    public const string IncompletePrices =
        "LinkDetective returned an incomplete price response. Saved prices have been kept.";
    public const string RequestFailed = "LinkDetective could not update prices. Try again later.";
    public const string AccessDenied = "LinkDetective access is unavailable. Contact an administrator.";
    public const string RateLimited = "LinkDetective request limit or quota reached. Try again later.";
    public const string TemporarilyUnavailable = "LinkDetective is temporarily unavailable. Try again later.";
    public const string MissingQuota =
        "LinkDetective did not report a usable quota. Background checks have been postponed.";
    public const string NotConfigured = "LinkDetective is not configured. Contact an administrator.";
    public const string NoMarketPrices = "No market prices found for this domain.";
    public const string PricesPending = "Prices are not available yet. Try again shortly.";
}
