namespace Redhead.SitesCatalog.Domain.Constants;

/// <summary>
/// Constants for export operations
/// </summary>
public static class ExportConstants
{
    public const int TrustedClientDailyUniqueExportedDomainsLimit = 1000;
    public const int TrustedClientWeeklyUniqueExportedDomainsLimit = 3000;
    public const int TrustedClientDailyExportOperationsLimit = 20;
    public const int TrustedClientWeeklyExportOperationsLimit = 60;

    public const string DestinationDownload = "Download";
    public const string DestinationGoogleDrive = "GoogleDrive";

    public const string ExportModeSites = "Sites";
    public const string ExportModeMultiSearch = "MultiSearch";

    public const string DailyUniqueDomainLimitReached = "DailyUniqueDomainLimitReached";
    public const string WeeklyUniqueDomainLimitReached = "WeeklyUniqueDomainLimitReached";
    public const string DailyExportOperationLimitReached = "DailyExportOperationLimitReached";
    public const string WeeklyExportOperationLimitReached = "WeeklyExportOperationLimitReached";

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
