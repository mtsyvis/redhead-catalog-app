namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;

public sealed record LinkDetectiveBatchPricesResponse(
    IReadOnlyDictionary<string, LinkDetectivePrices> PricesByDomain,
    bool IsTruncated,
    LinkDetectiveQuota? Quota);
