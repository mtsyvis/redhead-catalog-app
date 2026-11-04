using System.Text.Json;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Constants;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Errors;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Parsing;

internal static class LinkDetectiveQuotaParser
{
    private const string DailyQuotaPeriod = "day";
    private static readonly TimeSpan UnknownQuotaCooldown = TimeSpan.FromMinutes(5);

    public static LinkDetectiveQuota? Parse(JsonElement quota, DateTime now, bool rejectMalformedData = false)
    {
        var row = LinkDetectiveJsonReader.TryRead<QuotaRow>(quota);
        var hasQuota = quota.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
        if (rejectMalformedData && hasQuota && row is null)
        {
            throw LinkDetectiveApiException.IncompleteResponse();
        }

        if (row is null || row.Limit is not > 0 || row.Used is not >= 0 || row.Remaining is not >= 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(row.Period) || row.Period.Length > LinkDetectiveLimits.MaxQuotaPeriodLength)
        {
            return null;
        }

        if (row.Remaining > row.Limit || row.Used > row.Limit)
        {
            return null;
        }

        return new LinkDetectiveQuota(
            Limit: row.Limit.Value,
            Used: row.Used.Value,
            Remaining: row.Remaining.Value,
            Period: row.Period,
            ResetAtUtc: ResolveResetAtUtc(row, now));
    }

    public static DateTime? GetCooldownUntilUtc(JsonElement quota, DateTime now)
    {
        var row = LinkDetectiveJsonReader.TryRead<QuotaRow>(quota);
        // Exhaustion can be reported even when the remaining quota fields are incomplete.
        if (row is null || row.Remaining != 0)
        {
            return null;
        }

        return ResolveResetAtUtc(row, now) ?? now.Add(UnknownQuotaCooldown);
    }

    private static DateTime? ResolveResetAtUtc(QuotaRow row, DateTime now)
    {
        if (row.ResetAt is { } reportedReset)
        {
            return reportedReset.UtcDateTime > now ? reportedReset.UtcDateTime : null;
        }

        return row.Period == DailyQuotaPeriod ? now.Date.AddDays(1) : null;
    }

    private sealed record QuotaRow(long? Limit, long? Used, long? Remaining, string? Period, DateTimeOffset? ResetAt);
}
