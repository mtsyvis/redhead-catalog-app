using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

// Only the purchase basis of this bulk patch. Existing USD quotes are never reconverted.
internal static class BulkOfferPurchaseMinima
{
    internal sealed record Key(string Domain, PriceType Service, PricingTerm Term);

    internal static HashSet<Key> AffectedKeys(IEnumerable<BulkOfferPricePreviewRow> ready,
        IReadOnlyDictionary<Guid, SiteWebmasterOffer> byId, PricingTerm term)
    {
        var keys = new HashSet<Key>();
        foreach (var row in ready)
        {
            var offer = byId[row.OfferId!.Value];
            var stateChanged = row.CurrentTerm != row.ProposedTerm || row.CurrentStatus != row.ProposedStatus;
            foreach (var service in Enum.GetValues<PriceType>())
            {
                var offerType = CatalogPriceMapping.ToOfferType(service);
                var price = offer.Prices.SingleOrDefault(price => price.PriceType == offerType);
                if (!stateChanged && !row.Prices.Any(change => change.PriceType == offerType && change.Changed))
                {
                    continue;
                }

                if (price is not null && BulkOfferPricePreviewService.GetTerm(price.TermType, price.TermValue, price.TermUnit) is { } oldTerm)
                {
                    keys.Add(new(offer.SiteDomain, service, oldTerm));
                }

                if (price is not null || row.Prices.Any(change => change.PriceType == offerType))
                {
                    keys.Add(new(offer.SiteDomain, service, term));
                }
            }
        }
        return keys;
    }

    internal static decimal? Minimum(IEnumerable<SiteWebmasterOffer> suppliers, Key key)
        => suppliers.Where(offer => offer.Status == SiteWebmasterOfferStatus.Active)
            .SelectMany(offer => offer.Prices).Where(price => price.PriceType == CatalogPriceMapping.ToOfferType(key.Service)
                && price.AvailabilityStatus == ServiceAvailabilityStatus.Available && price.WebmasterPriceUsd > 0
                && BulkOfferPricePreviewService.GetTerm(price.TermType, price.TermValue, price.TermUnit) == key.Term)
            .Select(price => price.WebmasterPriceUsd).DefaultIfEmpty(null).Min();

    internal static BulkOfferPricePreviewResult Attach(BulkOfferPricePreviewResult result, IReadOnlyList<BulkOfferPurchaseMinimum> minima)
    {
        var byDomain = minima.ToLookup(key => key.Domain);
        return result with { Rows = result.Rows.Select(row => row with { PurchaseMinima = byDomain[row.Domain].ToArray() }).ToArray() };
    }

    internal static IReadOnlyList<BulkOfferPurchaseMinimum> Preview(IReadOnlyList<BulkOfferPricePreviewRow> rows,
        IReadOnlyList<SiteWebmasterOffer> suppliers, PricingTerm term)
    {
        var ready = rows.Where(row => row.Outcome == BulkOfferPricePreviewOutcomes.Ready).ToArray();
        var byId = suppliers.ToDictionary(offer => offer.Id);
        var keys = AffectedKeys(ready, byId, term);
        var before = suppliers.ToLookup(offer => offer.SiteDomain);
        var changes = ready.ToDictionary(row => row.OfferId!.Value);
        // Lightweight projection; never mutate tracked entities or use the conversion service again.
        var after = suppliers.Select(offer =>
        {
            if (!changes.TryGetValue(offer.Id, out var row))
            {
                return offer;
            }

            var prices = offer.Prices.Select(price => new WebmasterOfferPrice
            {
                PriceType = price.PriceType, AvailabilityStatus = price.AvailabilityStatus, WebmasterPriceUsd = price.WebmasterPriceUsd,
                TermType = row.CurrentTerm != row.ProposedTerm ? term.TermType : price.TermType,
                TermValue = row.CurrentTerm != row.ProposedTerm ? term.TermValue : price.TermValue,
                TermUnit = row.CurrentTerm != row.ProposedTerm ? term.TermUnit : price.TermUnit
            }).ToList();
            foreach (var change in row.Prices.Where(change => change.Changed))
            {
                var price = prices.SingleOrDefault(price => price.PriceType == change.PriceType);
                if (price is null)
                {
                    price = new() { PriceType = change.PriceType, TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit };
                    prices.Add(price);
                }
                price.AvailabilityStatus = change.Proposed.AvailabilityStatus;
                price.WebmasterPriceUsd = change.Proposed.WebmasterPriceUsd;
            }
            return new SiteWebmasterOffer { SiteDomain = offer.SiteDomain, Status = row.ProposedStatus!.Value, Prices = prices };
        }).ToLookup(offer => offer.SiteDomain);
        return keys.OrderBy(key => key.Domain, StringComparer.Ordinal).ThenBy(key => key.Service).ThenBy(key => key.Term.TermKey, StringComparer.Ordinal)
            .Select(key => new BulkOfferPurchaseMinimum(key.Domain, key.Service, key.Term,
                Minimum(before[key.Domain], key), Minimum(after[key.Domain], key), before[key.Domain].First().Site.IsQuarantined)).ToArray();
    }
}
