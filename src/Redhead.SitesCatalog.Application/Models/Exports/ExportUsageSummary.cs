namespace Redhead.SitesCatalog.Application.Models.Exports;

public sealed record ExportUsageSummary(
    int? DailyUniqueExportedDomainsUsed,
    int? DailyUniqueExportedDomainsLimit,
    int? WeeklyUniqueExportedDomainsUsed,
    int? WeeklyUniqueExportedDomainsLimit,
    int? DailyExportOperationsUsed,
    int? DailyExportOperationsLimit,
    int? WeeklyExportOperationsUsed,
    int? WeeklyExportOperationsLimit);
