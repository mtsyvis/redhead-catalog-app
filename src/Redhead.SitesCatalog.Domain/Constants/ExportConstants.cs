namespace Redhead.SitesCatalog.Domain.Constants;

/// <summary>
/// Constants for export operations
/// </summary>
public static class ExportConstants
{
    /// <summary>
    /// Excel file name for sites export
    /// </summary>
    public const string SitesFileName = "sites.xlsx";

    /// <summary>
    /// Content type for Excel workbook files
    /// </summary>
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Error message when export is disabled for user's role
    /// </summary>
    public const string ExportDisabledMessage = "Export is disabled for your role";

    /// <summary>
    /// When ExportLimitRows is 0, export is disabled
    /// </summary>
    public const int DisabledLimit = 0;
}
