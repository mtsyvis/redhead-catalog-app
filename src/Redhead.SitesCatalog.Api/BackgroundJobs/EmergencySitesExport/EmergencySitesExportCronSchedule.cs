using Cronos;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.EmergencySitesExport;

public sealed class EmergencySitesExportCronSchedule
{
    private readonly CronExpression _expression;

    private EmergencySitesExportCronSchedule(CronExpression expression)
    {
        _expression = expression;
    }

    public static bool TryParse(string? cron, out EmergencySitesExportCronSchedule schedule)
    {
        schedule = default!;
        if (string.IsNullOrWhiteSpace(cron))
        {
            return false;
        }

        if (!CronExpression.TryParse(cron, CronFormat.Standard, out var expression))
        {
            return false;
        }

        schedule = new EmergencySitesExportCronSchedule(expression);
        return true;
    }

    public DateTime? GetDueOccurrenceUtc(DateTime utcNow)
    {
        var normalizedNow = EnsureUtc(utcNow);
        var weekStartUtc = GetIsoWeekStartUtc(normalizedNow);
        DateTime? lastOccurrence = null;

        foreach (var occurrence in _expression.GetOccurrences(
            weekStartUtc,
            normalizedNow,
            fromInclusive: true,
            toInclusive: true))
        {
            lastOccurrence = occurrence;
        }

        return lastOccurrence;
    }

    public DateTime? GetNextOccurrenceUtc(DateTime utcNow)
        => _expression.GetNextOccurrence(EnsureUtc(utcNow), inclusive: false);

    private static DateTime EnsureUtc(DateTime dateTime)
        => dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

    private static DateTime GetIsoWeekStartUtc(DateTime utcNow)
    {
        var date = DateOnly.FromDateTime(utcNow);
        var monday = date.AddDays(-GetIsoDayOffset(date.DayOfWeek));

        return DateTime.SpecifyKind(
            monday.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);
    }

    private static int GetIsoDayOffset(DayOfWeek dayOfWeek)
        => dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
}
