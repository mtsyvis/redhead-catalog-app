using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportActivityReasonFormatter
{
    public static string? Format(
        string? blockedReason,
        bool wasTruncated,
        int? exportLimitRows,
        int exportedRows)
    {
        if (!string.IsNullOrWhiteSpace(blockedReason))
        {
            return blockedReason switch
            {
                ExportConstants.DailyUniqueDomainLimitReached => "Daily domain limit",
                ExportConstants.WeeklyUniqueDomainLimitReached => "Weekly domain limit",
                ExportConstants.DailyExportOperationLimitReached => "Daily operation limit",
                ExportConstants.WeeklyExportOperationLimitReached => "Weekly operation limit",
                _ => null
            };
        }

        if (wasTruncated &&
            exportLimitRows.HasValue &&
            exportedRows >= exportLimitRows.Value)
        {
            return "Rows per export limit";
        }

        return null;
    }
}
