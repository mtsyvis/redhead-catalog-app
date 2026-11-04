using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;

public interface ILinkDetectiveApiClient
{
    Task<LinkDetectivePricesResponse> GetPricesAsync(string domain, CancellationToken cancellationToken);
    Task<LinkDetectiveBatchPricesResponse> GetBatchPricesAsync(IReadOnlyCollection<string> domains, CancellationToken cancellationToken);
    Task<LinkDetectiveQuota> GetQuotaAsync(CancellationToken cancellationToken);
}
