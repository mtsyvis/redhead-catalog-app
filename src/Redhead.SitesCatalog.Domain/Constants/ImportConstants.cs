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
        public const string Niche = "Niche";
        public const string Categories = "Categories";
    }

    /// <summary>
    /// Required column names for sites import, in exact order. All columns must be present;.
    /// </summary>
    public static readonly string[] SitesImportRequiredColumnOrder =
    {
        SitesImportColumns.Domain,
        SitesImportColumns.DR,
        SitesImportColumns.Traffic,
        SitesImportColumns.Location,
        SitesImportColumns.PriceUsd,
        SitesImportColumns.PriceCasino,
        SitesImportColumns.PriceCrypto,
        SitesImportColumns.PriceLinkInsert,
        SitesImportColumns.Niche,
        SitesImportColumns.Categories,
    };
}
