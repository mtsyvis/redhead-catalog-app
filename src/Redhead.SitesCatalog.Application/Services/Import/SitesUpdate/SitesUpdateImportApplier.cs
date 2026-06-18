using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.Import.SitesUpdate;

internal static class SitesUpdateImportApplier
{
    public static void Apply(Site site, SitesUpdateImportRow update, DateTime now)
    {
        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.DR))
        {
            site.DR = update.DR;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.Traffic))
        {
            site.Traffic = update.Traffic;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.Location))
        {
            site.Location = update.Location ?? string.Empty;
            site.LocationKey = update.LocationKey;
            site.ImportedLocationRaw = update.ImportedLocationRaw;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.NumberDFLinks))
        {
            site.NumberDFLinks = update.NumberDFLinks;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.Language))
        {
            site.Language = update.Language;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.Niche))
        {
            site.Niche = update.Niche;
            site.NicheTokens = NicheNormalizer.NormalizeTokens(update.Niche);
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.Categories))
        {
            site.Categories = update.Categories;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.SponsoredTag))
        {
            site.SponsoredTag = update.SponsoredTag;
        }

        foreach (var availabilityOperation in update.AvailabilityOperations)
        {
            RemovePrice(site, availabilityOperation.ServiceType, availabilityOperation.TermKey);
            var resolvedStatus = site.PriceOptions.Any(price => price.PriceType == availabilityOperation.ServiceType)
                ? ServiceAvailabilityStatus.Available
                : availabilityOperation.Status;
            UpsertAvailability(site, availabilityOperation.ServiceType, resolvedStatus, now);
        }

        foreach (var priceOperation in update.PriceOperations)
        {
            if (priceOperation.AmountUsd is null)
            {
                RemovePrice(site, priceOperation.PriceType, priceOperation.TermKey);
                continue;
            }

            UpsertPrice(site, priceOperation, priceOperation.AmountUsd.Value, now);
            if (priceOperation.PriceType != PriceType.Main)
            {
                UpsertAvailability(site, priceOperation.PriceType, ServiceAvailabilityStatus.Available, now);
            }
        }
    }

    private static void UpsertPrice(Site site, SitesUpdatePriceOperation operation, decimal amountUsd, DateTime now)
    {
        var existing = site.PriceOptions.SingleOrDefault(price =>
            price.PriceType == operation.PriceType
            && string.Equals(price.TermKey, operation.TermKey, StringComparison.Ordinal));

        if (existing is null)
        {
            site.PriceOptions.Add(new SitePriceOption
            {
                SiteDomain = site.Domain,
                PriceType = operation.PriceType,
                TermKey = operation.TermKey,
                TermType = operation.TermType,
                TermValue = operation.TermValue,
                TermUnit = operation.TermUnit,
                AmountUsd = amountUsd,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            return;
        }

        existing.TermType = operation.TermType;
        existing.TermValue = operation.TermValue;
        existing.TermUnit = operation.TermUnit;
        existing.AmountUsd = amountUsd;
        existing.UpdatedAtUtc = now;
    }

    private static void RemovePrice(Site site, PriceType priceType, string termKey)
    {
        var existing = site.PriceOptions.SingleOrDefault(price =>
            price.PriceType == priceType
            && string.Equals(price.TermKey, termKey, StringComparison.Ordinal));
        if (existing is not null)
        {
            site.PriceOptions.Remove(existing);
        }
    }

    private static void UpsertAvailability(
        Site site,
        PriceType serviceType,
        ServiceAvailabilityStatus status,
        DateTime now)
    {
        var existing = site.ServiceAvailabilities.SingleOrDefault(availability => availability.ServiceType == serviceType);
        if (existing is null)
        {
            site.ServiceAvailabilities.Add(new SiteServiceAvailability
            {
                SiteDomain = site.Domain,
                ServiceType = serviceType,
                Status = status,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            return;
        }

        existing.Status = status;
        existing.UpdatedAtUtc = now;
    }
}
