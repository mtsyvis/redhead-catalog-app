using System.Globalization;

namespace Redhead.SitesCatalog.Application.SystemJobs;

public static class SystemJobPeriodKey
{
    public static string FromUtcWeek(DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind == DateTimeKind.Local
            ? utcDateTime.ToUniversalTime()
            : utcDateTime;

        var year = ISOWeek.GetYear(utc);
        var week = ISOWeek.GetWeekOfYear(utc);

        return $"{year:D4}-W{week:D2}";
    }
}
