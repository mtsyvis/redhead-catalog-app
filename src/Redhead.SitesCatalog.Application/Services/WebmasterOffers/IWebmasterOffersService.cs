using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Models.ChangeHistory;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public interface IWebmasterOffersService
{
    Task<WebmasterOffersSearchResult> GetByDomainAsync(string? domain, CancellationToken cancellationToken = default);
    Task<WebmasterOfferEditDto?> GetForEditAsync(Guid offerId, CancellationToken cancellationToken = default);
    Task<WebmasterOfferUpdateResult> UpdateAsync(
        Guid offerId,
        UpdateWebmasterOfferRequest request,
        string? userEmail,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntityChangeHistoryDto>> GetHistoryAsync(
        Guid offerId,
        CancellationToken cancellationToken = default);
}
