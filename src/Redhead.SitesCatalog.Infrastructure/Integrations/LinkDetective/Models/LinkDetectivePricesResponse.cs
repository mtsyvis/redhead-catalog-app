namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;

public sealed record LinkDetectivePricesResponse(LinkDetectivePrices Prices, LinkDetectiveQuota? Quota = null);
