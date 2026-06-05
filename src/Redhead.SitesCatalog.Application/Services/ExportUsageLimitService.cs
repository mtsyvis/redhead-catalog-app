using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class ExportUsageLimitService : IExportUsageLimitService
{
    private readonly ApplicationDbContext _context;

    public ExportUsageLimitService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExportUsageSummary> GetUsageAsync(
        string userId,
        string userRole,
        EffectiveExportPolicy policy,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!AppliesToClient(userRole))
        {
            return EmptyUsage();
        }

        var windows = RollingExportWindows.From(nowUtc);
        var successfulExports = SuccessfulExportsForUser(userId);

        var dailyOperationsUsed = await CountOperationsAsync(
            successfulExports,
            windows.DailyStartUtc,
            cancellationToken);
        var weeklyOperationsUsed = await CountOperationsAsync(
            successfulExports,
            windows.WeeklyStartUtc,
            cancellationToken);
        var dailyUniqueDomainsUsed = await CountUniqueDomainsAsync(
            userId,
            windows.DailyStartUtc,
            cancellationToken);
        var weeklyUniqueDomainsUsed = await CountUniqueDomainsAsync(
            userId,
            windows.WeeklyStartUtc,
            cancellationToken);

        return new ExportUsageSummary(
            UsedWhenLimited(policy.DailyUniqueExportedDomainsLimit, dailyUniqueDomainsUsed),
            policy.DailyUniqueExportedDomainsLimit,
            UsedWhenLimited(policy.WeeklyUniqueExportedDomainsLimit, weeklyUniqueDomainsUsed),
            policy.WeeklyUniqueExportedDomainsLimit,
            UsedWhenLimited(policy.DailyExportOperationsLimit, dailyOperationsUsed),
            policy.DailyExportOperationsLimit,
            UsedWhenLimited(policy.WeeklyExportOperationsLimit, weeklyOperationsUsed),
            policy.WeeklyExportOperationsLimit);
    }

    public async Task<ExportUsageLimitEvaluation> EvaluateAsync(
        string userId,
        string userRole,
        EffectiveExportPolicy policy,
        IReadOnlyList<string> candidateDomains,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!AppliesToClient(userRole))
        {
            return AllowedForInternalRole(candidateDomains);
        }

        var windows = RollingExportWindows.From(nowUtc);
        var usage = await GetUsageAsync(userId, userRole, policy, nowUtc, cancellationToken);
        var operationLimitReason = GetReachedOperationLimitReason(usage);
        if (operationLimitReason != null)
        {
            return Blocked(usage, operationLimitReason);
        }

        var dailyDomains = await GetDistinctDomainsAsync(userId, windows.DailyStartUtc, cancellationToken);
        var weeklyDomains = await GetDistinctDomainsAsync(userId, windows.WeeklyStartUtc, cancellationToken);

        return EvaluateUniqueDomainLimits(candidateDomains, usage, dailyDomains, weeklyDomains);
    }

    private IQueryable<ExportLog> SuccessfulExportsForUser(string userId)
        => _context.ExportLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.BlockedReason == null);

    private async Task<int> CountOperationsAsync(
        IQueryable<ExportLog> successfulExports,
        DateTime startUtc,
        CancellationToken cancellationToken)
        => await successfulExports.CountAsync(
            log => log.TimestampUtc >= startUtc,
            cancellationToken);

    private async Task<int> CountUniqueDomainsAsync(
        string userId,
        DateTime startUtc,
        CancellationToken cancellationToken)
        => await _context.ExportedDomainAccesses
            .AsNoTracking()
            .Where(access => access.UserId == userId && access.ExportedAtUtc >= startUtc)
            .Select(access => access.Domain)
            .Distinct()
            .CountAsync(cancellationToken);

    private async Task<HashSet<string>> GetDistinctDomainsAsync(
        string userId,
        DateTime startUtc,
        CancellationToken cancellationToken)
    {
        var domains = await _context.ExportedDomainAccesses
            .AsNoTracking()
            .Where(access => access.UserId == userId && access.ExportedAtUtc >= startUtc)
            .Select(access => access.Domain)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(domains, StringComparer.Ordinal);
    }

    private static ExportUsageLimitEvaluation EvaluateUniqueDomainLimits(
        IReadOnlyList<string> candidateDomains,
        ExportUsageSummary usage,
        HashSet<string> dailyDomains,
        HashSet<string> weeklyDomains)
    {
        var allowedDomains = new List<string>(candidateDomains.Count);
        var dailyNewDomainsUsed = 0;
        var weeklyNewDomainsUsed = 0;
        string? firstSkippedReason = null;

        foreach (var domain in candidateDomains)
        {
            var requiresDailySlot = !dailyDomains.Contains(domain);
            var requiresWeeklySlot = !weeklyDomains.Contains(domain);
            var skippedReason = GetUniqueDomainSkippedReason(
                usage,
                requiresDailySlot,
                dailyNewDomainsUsed,
                requiresWeeklySlot,
                weeklyNewDomainsUsed);

            if (skippedReason != null)
            {
                firstSkippedReason ??= skippedReason;
                continue;
            }

            allowedDomains.Add(domain);

            if (requiresDailySlot)
            {
                dailyNewDomainsUsed++;
            }

            if (requiresWeeklySlot)
            {
                weeklyNewDomainsUsed++;
            }
        }

        var blocked = candidateDomains.Count > 0 &&
                      allowedDomains.Count == 0 &&
                      firstSkippedReason != null;

        return new ExportUsageLimitEvaluation(
            Applies: true,
            IsBlocked: blocked,
            BlockedReason: blocked ? firstSkippedReason : null,
            TruncationReason: allowedDomains.Count < candidateDomains.Count ? firstSkippedReason : null,
            AllowedDomains: allowedDomains,
            Usage: usage);
    }

    private static string? GetUniqueDomainSkippedReason(
        ExportUsageSummary usage,
        bool requiresDailySlot,
        int pendingDailyDomains,
        bool requiresWeeklySlot,
        int pendingWeeklyDomains)
    {
        if (requiresDailySlot &&
            !HasRemainingSlot(
                usage.DailyUniqueExportedDomainsUsed,
                usage.DailyUniqueExportedDomainsLimit,
                pendingDailyDomains))
        {
            return ExportConstants.DailyUniqueDomainLimitReached;
        }

        if (requiresWeeklySlot &&
            !HasRemainingSlot(
                usage.WeeklyUniqueExportedDomainsUsed,
                usage.WeeklyUniqueExportedDomainsLimit,
                pendingWeeklyDomains))
        {
            return ExportConstants.WeeklyUniqueDomainLimitReached;
        }

        return null;
    }

    private static string? GetReachedOperationLimitReason(ExportUsageSummary usage)
    {
        if (IsLimitReached(usage.DailyExportOperationsUsed, usage.DailyExportOperationsLimit))
        {
            return ExportConstants.DailyExportOperationLimitReached;
        }

        if (IsLimitReached(usage.WeeklyExportOperationsUsed, usage.WeeklyExportOperationsLimit))
        {
            return ExportConstants.WeeklyExportOperationLimitReached;
        }

        return null;
    }

    private static ExportUsageLimitEvaluation AllowedForInternalRole(IReadOnlyList<string> candidateDomains)
        => new(
            Applies: false,
            IsBlocked: false,
            BlockedReason: null,
            TruncationReason: null,
            AllowedDomains: candidateDomains,
            Usage: EmptyUsage());

    private static ExportUsageLimitEvaluation Blocked(ExportUsageSummary usage, string reason)
        => new(
            Applies: true,
            IsBlocked: true,
            BlockedReason: reason,
            TruncationReason: null,
            AllowedDomains: [],
            Usage: usage);

    private static bool AppliesToClient(string userRole)
        => string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal);

    private static bool IsLimitReached(int? used, int? limit)
        => used.HasValue && limit.HasValue && used.Value >= limit.Value;

    private static bool HasRemainingSlot(int? used, int? limit, int pendingNewDomains)
        => !used.HasValue ||
           !limit.HasValue ||
           used.Value + pendingNewDomains < limit.Value;

    private static int? UsedWhenLimited(int? limit, int used)
        => limit.HasValue ? used : null;

    private static ExportUsageSummary EmptyUsage()
        => new(
            DailyUniqueExportedDomainsUsed: null,
            DailyUniqueExportedDomainsLimit: null,
            WeeklyUniqueExportedDomainsUsed: null,
            WeeklyUniqueExportedDomainsLimit: null,
            DailyExportOperationsUsed: null,
            DailyExportOperationsLimit: null,
            WeeklyExportOperationsUsed: null,
            WeeklyExportOperationsLimit: null);

    private sealed record RollingExportWindows(DateTime DailyStartUtc, DateTime WeeklyStartUtc)
    {
        public static RollingExportWindows From(DateTime nowUtc)
            => new(
                DailyStartUtc: nowUtc.AddHours(-24),
                WeeklyStartUtc: nowUtc.AddDays(-7));
    }
}
