namespace Redhead.SitesCatalog.Domain.Constants;

/// <summary>
/// Constants for import operations
/// </summary>
public static class ImportConstants
{
    /// <summary>
    /// ImportLog Type value for sites import
    /// </summary>
    public const string ImportTypeSites = "Sites";

    /// <summary>
    /// Slug used in generated invalid-rows download filenames for Sites import.
    /// </summary>
    public const string ImportArtifactSlugSites = "sites";

    /// <summary>
    /// ImportLog Type value for quarantine import (Commit 11)
    /// </summary>
    public const string ImportTypeQuarantine = "Quarantine";

    /// <summary>
    /// Slug used in generated invalid-rows download filenames for Quarantine import.
    /// </summary>
    public const string ImportArtifactSlugQuarantine = "quarantine";

    /// <summary>
    /// ImportLog Type value for Last Published Date import (Commit 15)
    /// </summary>
    public const string ImportTypeLastPublished = "LastPublished";

    /// <summary>
    /// Slug used in generated invalid-rows download filenames for Last Published import.
    /// </summary>
    public const string ImportArtifactSlugLastPublished = "last-published";

    /// <summary>
    /// ImportLog Type value for sites mass-update import (update existing sites by Domain).
    /// </summary>
    public const string ImportTypeSitesUpdate = "SitesUpdate";

    /// <summary>
    /// Slug used in generated invalid-rows download filenames for Sites update import.
    /// </summary>
    public const string ImportArtifactSlugSitesUpdate = "sites-update";

    public const string ImportTypeWebmasterOffers = "WebmasterOffers";

    public const string ImportArtifactSlugWebmasterOffers = "webmaster-offers";

    /// <summary>
    /// Default batch size for site inserts
    /// </summary>
    public const int SitesImportBatchSize = 1000;

    /// <summary>
    /// Maximum number of error details to keep in memory and return in import result (avoids huge API responses for 60k+ rows).
    /// </summary>
    public const int SitesImportMaxDetailErrors = 200;

    /// <summary>
    /// Maximum number of duplicate domain details to keep in memory and return in import result (avoids huge API responses for 60k+ rows).
    /// </summary>
    public const int SitesImportMaxDetailDuplicates = 200;

    /// <summary>
    /// Maximum number of unique duplicate domains included in preview fields.
    /// </summary>
    public const int DuplicateDomainsPreviewLimit = 100;

    /// <summary>
    /// CSV file extension
    /// </summary>
    public const string CsvExtension = ".csv";

    /// <summary>
    /// Content type for CSV
    /// </summary>
    public const string CsvContentType = "text/csv";

    /// <summary>
    /// Maximum file size in bytes for sites import (50 MB)
    /// </summary>
    public const long MaxSitesImportFileSizeBytes = 50L * 1024 * 1024;

    /// <summary>
    /// User-facing message when import file exceeds max size. Derived from MaxSitesImportFileSizeBytes.
    /// </summary>
    public static readonly string FileTooLargeMessage =
        $"File is too large. Maximum size is {MaxSitesImportFileSizeBytes / (1024 * 1024)} MB.";

    /// <summary>
    /// Expected column headers for sites import (CSV). Parsers match case-insensitively.
    /// </summary>
    public static class SitesImportColumns
    {
        public const string Domain = "Domain";
        public const string DR = "DR";
        public const string Traffic = "Traffic";
        public const string Location = "Location";
        public const string PriceUsd = "PriceUsd";
        public const string PriceCasino = "PriceCasino";
        public const string PriceCrypto = "PriceCrypto";
        public const string PriceLinkInsert = "PriceLinkInsert";
        public const string PriceLinkInsertCasino = "PriceLinkInsertCasino";
        public const string PriceDating = "PriceDating";
        public const string Niche = "Niche";
        public const string Categories = "Categories";
        public const string NumberDFLinks = "NumberDFLinks";
        public const string SponsoredTag = "SponsoredTag";
        public const string Term = "Term";
        public const string Language = "Language";
        public const string TrafficValueUsd = "TrafficValueUsd";
        public const string PagesCount = "PagesCount";
    }

    public static class WebmasterOffersImportColumns
    {
        public const string Domain = "Domain";
        public const string ContactRawText = "ContactRawText";
        public const string OutreachSenderRawText = "OutreachSenderRawText";
        public const string LinkbuilderMailboxRawText = "LinkbuilderMailboxRawText";
        public const string Term = "Term";
        public const string MainWebmasterPriceUsd = "MainWebmasterPriceUsd";
        public const string MainWebmasterPriceDetails = "MainWebmasterPriceDetails";
        public const string CasinoWebmasterPriceUsd = "CasinoWebmasterPriceUsd";
        public const string CasinoWebmasterPriceDetails = "CasinoWebmasterPriceDetails";
        public const string CryptoWebmasterPriceUsd = "CryptoWebmasterPriceUsd";
        public const string CryptoWebmasterPriceDetails = "CryptoWebmasterPriceDetails";
        public const string DatingWebmasterPriceUsd = "DatingWebmasterPriceUsd";
        public const string DatingWebmasterPriceDetails = "DatingWebmasterPriceDetails";
        public const string LinkInsertionWebmasterPriceUsd = "LinkInsertionWebmasterPriceUsd";
        public const string LinkInsertionWebmasterPriceDetails = "LinkInsertionWebmasterPriceDetails";
        public const string LinkInsertion18PlusWebmasterPriceUsd = "LinkInsertion18PlusWebmasterPriceUsd";
        public const string LinkInsertion18PlusWebmasterPriceDetails = "LinkInsertion18PlusWebmasterPriceDetails";
        public const string BannerWebmasterPriceUsd = "BannerWebmasterPriceUsd";
        public const string BannerWebmasterPriceDetails = "BannerWebmasterPriceDetails";
        public const string Banner18PlusWebmasterPriceUsd = "Banner18PlusWebmasterPriceUsd";
        public const string Banner18PlusWebmasterPriceDetails = "Banner18PlusWebmasterPriceDetails";
        public const string HomepageTextLinkWebmasterPriceUsd = "HomepageTextLinkWebmasterPriceUsd";
        public const string HomepageTextLinkWebmasterPriceDetails = "HomepageTextLinkWebmasterPriceDetails";
        public const string HomepageTextLink18PlusWebmasterPriceUsd = "HomepageTextLink18PlusWebmasterPriceUsd";
        public const string HomepageTextLink18PlusWebmasterPriceDetails = "HomepageTextLink18PlusWebmasterPriceDetails";
        public const string LinkPolicyText = "LinkPolicyText";
        public const string CommentText = "CommentText";
        public const string ClientRawText = "ClientRawText";
    }

    public static readonly string[] WebmasterOffersImportColumnsInOrder =
    [
        WebmasterOffersImportColumns.Domain,
        WebmasterOffersImportColumns.MainWebmasterPriceDetails,
        WebmasterOffersImportColumns.MainWebmasterPriceUsd,
        WebmasterOffersImportColumns.CasinoWebmasterPriceDetails,
        WebmasterOffersImportColumns.CasinoWebmasterPriceUsd,
        WebmasterOffersImportColumns.CryptoWebmasterPriceDetails,
        WebmasterOffersImportColumns.CryptoWebmasterPriceUsd,
        WebmasterOffersImportColumns.DatingWebmasterPriceDetails,
        WebmasterOffersImportColumns.DatingWebmasterPriceUsd,
        WebmasterOffersImportColumns.LinkInsertionWebmasterPriceDetails,
        WebmasterOffersImportColumns.LinkInsertionWebmasterPriceUsd,
        WebmasterOffersImportColumns.LinkInsertion18PlusWebmasterPriceDetails,
        WebmasterOffersImportColumns.LinkInsertion18PlusWebmasterPriceUsd,
        WebmasterOffersImportColumns.BannerWebmasterPriceDetails,
        WebmasterOffersImportColumns.BannerWebmasterPriceUsd,
        WebmasterOffersImportColumns.Banner18PlusWebmasterPriceDetails,
        WebmasterOffersImportColumns.Banner18PlusWebmasterPriceUsd,
        WebmasterOffersImportColumns.HomepageTextLinkWebmasterPriceDetails,
        WebmasterOffersImportColumns.HomepageTextLinkWebmasterPriceUsd,
        WebmasterOffersImportColumns.HomepageTextLink18PlusWebmasterPriceDetails,
        WebmasterOffersImportColumns.HomepageTextLink18PlusWebmasterPriceUsd,
        WebmasterOffersImportColumns.LinkPolicyText,
        WebmasterOffersImportColumns.Term,
        WebmasterOffersImportColumns.LinkbuilderMailboxRawText,
        WebmasterOffersImportColumns.OutreachSenderRawText,
        WebmasterOffersImportColumns.ContactRawText,
        WebmasterOffersImportColumns.CommentText,
        WebmasterOffersImportColumns.ClientRawText
    ];

    /// <summary>
    /// Required base column names for insert sites import. Header order is flexible.
    /// </summary>
    public static readonly string[] SitesImportRequiredColumns =
    {
        SitesImportColumns.Domain,
        SitesImportColumns.DR,
        SitesImportColumns.Traffic,
        SitesImportColumns.Location,
    };

    /// <summary>
    /// Optional non-pricing column names for insert sites import. Header order is flexible.
    /// </summary>
    public static readonly string[] SitesImportOptionalColumns =
    {
        SitesImportColumns.Niche,
        SitesImportColumns.Categories,
        SitesImportColumns.NumberDFLinks,
        SitesImportColumns.SponsoredTag,
        SitesImportColumns.Language,
    };

    /// <summary>
    /// Non-pricing columns accepted by sites update import. Dynamic term-aware pricing columns are parsed separately.
    /// </summary>
    public static readonly string[] SitesUpdateImportBaseColumns =
    {
        SitesImportColumns.Domain,
        SitesImportColumns.DR,
        SitesImportColumns.Traffic,
        SitesImportColumns.Location,
        SitesImportColumns.Niche,
        SitesImportColumns.Categories,
        SitesImportColumns.NumberDFLinks,
        SitesImportColumns.SponsoredTag,
        SitesImportColumns.Language,
        SitesImportColumns.TrafficValueUsd,
        SitesImportColumns.PagesCount,
    };

}
