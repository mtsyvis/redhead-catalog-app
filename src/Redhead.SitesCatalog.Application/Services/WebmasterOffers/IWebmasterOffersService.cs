using Redhead.SitesCatalog.Application.Models.WebmasterOffers;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public interface IWebmasterOffersService
{
    Task<WebmasterOffersSearchResult> GetByDomainAsync(string? domain, CancellationToken cancellationToken = default);
}
