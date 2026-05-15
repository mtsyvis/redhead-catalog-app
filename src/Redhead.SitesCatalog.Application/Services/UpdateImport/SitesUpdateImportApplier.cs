using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Application.Services.UpdateImport;

internal static class SitesUpdateImportApplier
{
    public static void Apply(Site site, SitesUpdateImportRow update)
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
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceUsd))
        {
            site.PriceUsd = update.PriceUsd;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCasino))
        {
            site.PriceCasino = update.PriceCasino;
            site.PriceCasinoStatus = update.PriceCasinoStatus;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCrypto))
        {
            site.PriceCrypto = update.PriceCrypto;
            site.PriceCryptoStatus = update.PriceCryptoStatus;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsert))
        {
            site.PriceLinkInsert = update.PriceLinkInsert;
            site.PriceLinkInsertStatus = update.PriceLinkInsertStatus;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsertCasino))
        {
            site.PriceLinkInsertCasino = update.PriceLinkInsertCasino;
            site.PriceLinkInsertCasinoStatus = update.PriceLinkInsertCasinoStatus;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceDating))
        {
            site.PriceDating = update.PriceDating;
            site.PriceDatingStatus = update.PriceDatingStatus;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.NumberDFLinks))
        {
            site.NumberDFLinks = update.NumberDFLinks;
        }

        if (update.PresentColumns.Contains(ImportConstants.SitesImportColumns.Term))
        {
            site.TermType = update.TermType;
            site.TermValue = update.TermValue;
            site.TermUnit = update.TermUnit;
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
    }

    public static bool TouchesPriceColumns(SitesUpdateImportRow update)
    {
        return update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceUsd)
               || update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCasino)
               || update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCrypto)
               || update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsert)
               || update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsertCasino)
               || update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceDating);
    }

    public static bool WouldKeepAtLeastOneNumericPrice(Site site, SitesUpdateImportRow update)
    {
        return SitePriceValidationHelper.HasAnyNumericPrice(new SitePriceState(
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceUsd)
                ? update.PriceUsd
                : site.PriceUsd,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCasino)
                ? update.PriceCasino
                : site.PriceCasino,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCasino)
                ? update.PriceCasinoStatus
                : site.PriceCasinoStatus,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCrypto)
                ? update.PriceCrypto
                : site.PriceCrypto,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceCrypto)
                ? update.PriceCryptoStatus
                : site.PriceCryptoStatus,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsert)
                ? update.PriceLinkInsert
                : site.PriceLinkInsert,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsert)
                ? update.PriceLinkInsertStatus
                : site.PriceLinkInsertStatus,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsertCasino)
                ? update.PriceLinkInsertCasino
                : site.PriceLinkInsertCasino,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceLinkInsertCasino)
                ? update.PriceLinkInsertCasinoStatus
                : site.PriceLinkInsertCasinoStatus,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceDating)
                ? update.PriceDating
                : site.PriceDating,
            update.PresentColumns.Contains(ImportConstants.SitesImportColumns.PriceDating)
                ? update.PriceDatingStatus
                : site.PriceDatingStatus));
    }
}
