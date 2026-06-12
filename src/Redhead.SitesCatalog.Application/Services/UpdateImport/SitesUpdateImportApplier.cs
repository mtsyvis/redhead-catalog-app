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
            site.LocationKey = update.LocationKey;
            site.ImportedLocationRaw = update.ImportedLocationRaw;
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
}
