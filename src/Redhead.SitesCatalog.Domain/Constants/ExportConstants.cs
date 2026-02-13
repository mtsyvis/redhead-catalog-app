namespace Redhead.SitesCatalog.Domain.Constants;

/// <summary>
/// Constants for export operations
/// </summary>
public static class ExportConstants
{
    /// <summary>
    /// CSV file name for sites export
    /// </summary>
    public const string SitesFileName = "sites.csv";

    /// <summary>
    /// Content type for CSV files
    /// </summary>
    public const string CsvContentType = "text/csv";

    /// <summary>
    /// Error message when export is disabled for user's role
    /// </summary>
    public const string ExportDisabledMessage = "Export is disabled for your role";

    /// <summary>
    /// When ExportLimitRows is 0, export is disabled
    /// </summary>
    public const int DisabledLimit = 0;
}
