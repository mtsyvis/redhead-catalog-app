using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Domain.Exceptions;

public class ExportUsageLimitExceededException : Exception
{
    public string Reason { get; }

    public ExportUsageLimitExceededException(string reason)
        : base(CreateMessage(reason))
    {
        Reason = reason;
    }

    private static string CreateMessage(string reason)
        => reason switch
        {
            ExportConstants.DailyUniqueDomainLimitReached => "Daily unique exported domain limit reached.",
            ExportConstants.WeeklyUniqueDomainLimitReached => "Weekly unique exported domain limit reached.",
            ExportConstants.DailyExportOperationLimitReached => "Daily export operation limit reached.",
            ExportConstants.WeeklyExportOperationLimitReached => "Weekly export operation limit reached.",
            _ => "Export usage limit reached."
        };
}
