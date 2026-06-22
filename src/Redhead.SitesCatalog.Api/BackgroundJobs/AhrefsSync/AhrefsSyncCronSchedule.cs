using Cronos;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.AhrefsSync;

public sealed class AhrefsSyncCronSchedule
{
    private readonly CronExpression _expression;

    private AhrefsSyncCronSchedule(CronExpression expression)
    {
        _expression = expression;
    }

    public static bool TryParse(string? cron, out AhrefsSyncCronSchedule schedule)
    {
        schedule = default!;
        if (string.IsNullOrWhiteSpace(cron) ||
            !CronExpression.TryParse(cron, CronFormat.Standard, out var expression))
        {
            return false;
        }

        schedule = new AhrefsSyncCronSchedule(expression);
        return true;
    }

    public DateTime? GetDueOccurrenceUtc(DateTime utcNow)
    {
        var normalizedNow = EnsureUtc(utcNow);
        var monthStart = new DateTime(
            normalizedNow.Year,
            normalizedNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        DateTime? occurrence = null;
        foreach (var candidate in _expression.GetOccurrences(monthStart, normalizedNow, true, true))
        {
            occurrence = candidate;
        }

        return occurrence;
    }

    public DateTime? GetNextOccurrenceUtc(DateTime utcNow)
        => _expression.GetNextOccurrence(EnsureUtc(utcNow), inclusive: false);

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
