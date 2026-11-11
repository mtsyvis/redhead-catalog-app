using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Audit;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

internal static class QuarantineWebmasterOffersUpdater
{
    // Changes are persisted by the caller together with the site update.
    public static async Task DeactivateAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<string> domains,
        DateTime now,
        string? userEmail,
        bool recordManualHistory,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in domains.Chunk(1000))
        {
            var offers = await context.SiteWebmasterOffers
                .Where(offer => chunk.Contains(offer.SiteDomain) && offer.Status == SiteWebmasterOfferStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var offer in offers)
            {
                offer.Status = SiteWebmasterOfferStatus.Inactive;
                offer.UpdatedAtUtc = now;
                offer.UpdatedBy = AuditUserFormatter.Format(userEmail);

                if (recordManualHistory)
                {
                    EntityChangeHistoryRecorder.RecordUpdate(
                        context,
                        EntityChangeHistoryConstants.WebmasterOfferEntityType,
                        offer.Id.ToString(),
                        new Dictionary<string, string?> { ["Status"] = SiteWebmasterOfferStatus.Active.ToString() },
                        new Dictionary<string, string?> { ["Status"] = SiteWebmasterOfferStatus.Inactive.ToString() },
                        EntityChangeHistoryConstants.ManualSource,
                        userEmail,
                        now);
                }
            }
        }
    }
}
