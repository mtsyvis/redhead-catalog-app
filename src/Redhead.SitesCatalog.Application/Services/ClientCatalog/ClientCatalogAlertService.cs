using Redhead.SitesCatalog.Domain.ClientCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Services.ClientCatalog;

public sealed class ClientCatalogAlertService(ApplicationDbContext db, IOptions<ClientCatalogOptions> options,
    IClientCatalogAlertEmailSender emailSender, TimeProvider clock)
{
    public static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ReviewCooldown = TimeSpan.FromHours(1);
    private static readonly TimeSpan ActivityWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan EmailRetryInterval = TimeSpan.FromMinutes(5);
    private const int EmailBatchSize = 20;

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await DetectActivityAsync(cancellationToken);
        await SendPendingEmailsAsync(cancellationToken);
    }

    private async Task DetectActivityAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var threshold = options.Value.AlertUniqueSitesPerHour;
        var counts = await GetHourlyCountsAsync(now, cancellationToken);
        var clientRoleIds = db.Roles.Where(role => role.Name == AppRoles.Client).Select(role => role.Id);
        var clientIds = await db.Users.AsNoTracking().Where(user => user.IsActive && db.UserRoles.Any(role =>
            role.UserId == user.Id && clientRoleIds.Contains(role.RoleId))).Select(user => user.Id).ToListAsync(cancellationToken);
        var activeAlerts = await db.ClientCatalogAlerts.Where(alert => alert.ReviewedAtUtc == null)
            .ToDictionaryAsync(alert => alert.UserId, cancellationToken);
        // Use persisted review times so the notification cooldown survives restarts.
        var reviewCutoff = now - ReviewCooldown;
        var recentlyReviewedUsers = (await db.ClientCatalogAlerts.AsNoTracking()
            .Where(alert => alert.ReviewedAtUtc > reviewCutoff)
            .Select(alert => alert.UserId).Distinct().ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        foreach (var userId in clientIds)
        {
            var uniqueSites = counts.GetValueOrDefault(userId);
            if (uniqueSites < threshold)
            {
                continue;
            }
            if (activeAlerts.TryGetValue(userId, out var active))
            {
                active.UniqueSites = Math.Max(active.UniqueSites, uniqueSites);
            }
            else if (!recentlyReviewedUsers.Contains(userId))
            {
                db.ClientCatalogAlerts.Add(new ClientCatalogAlert
                {
                    UserId = userId, DetectedAtUtc = now, UniqueSites = uniqueSites, Threshold = threshold
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendPendingEmailsAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Recipients.Length == 0)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var pending = await db.ClientCatalogAlerts.Where(alert => alert.ReviewedAtUtc == null &&
                alert.EmailSentAtUtc == null && (alert.NextEmailAttemptAtUtc == null || alert.NextEmailAttemptAtUtc <= now))
            .OrderBy(alert => alert.DetectedAtUtc).Take(EmailBatchSize).ToListAsync(cancellationToken);
        foreach (var alert in pending)
        {
            var userEmail = await db.Users.Where(user => user.Id == alert.UserId).Select(user => user.Email).SingleAsync(cancellationToken);
            var sent = await emailSender.SendAsync(alert, userEmail ?? alert.UserId, cancellationToken);
            if (sent) alert.EmailSentAtUtc = clock.GetUtcNow().UtcDateTime;
            alert.NextEmailAttemptAtUtc = clock.GetUtcNow().UtcDateTime + EmailRetryInterval;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<string, int>> GetHourlyCountsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var start = now - ActivityWindow;
        if (db.Database.IsNpgsql())
        {
            return await db.Database.SqlQuery<HourlyCount>($"""
                SELECT "UserId", count(DISTINCT domain)::int AS "UniqueSites" FROM (
                    SELECT "UserId", unnest("Domains") AS domain FROM "ClientCatalogRequests"
                    WHERE "TimestampUtc" > {start} AND "TimestampUtc" <= {now}
                    UNION ALL
                    SELECT "UserId", "Domain" AS domain FROM "ExportedDomainAccesses"
                    WHERE "ExportedAtUtc" > {start} AND "ExportedAtUtc" <= {now}
                ) AS issued GROUP BY "UserId"
                """).ToDictionaryAsync(row => row.UserId, row => row.UniqueSites, cancellationToken);
        }
        var requests = await db.ClientCatalogRequests.AsNoTracking().Where(row => row.TimestampUtc > start && row.TimestampUtc <= now)
            .Select(row => new { row.UserId, row.Domains }).ToListAsync(cancellationToken);
        var exports = await db.ExportedDomainAccesses.AsNoTracking().Where(row => row.ExportedAtUtc > start && row.ExportedAtUtc <= now)
            .Select(row => new { row.UserId, row.Domain }).ToListAsync(cancellationToken);
        return requests.SelectMany(row => row.Domains.Select(domain => (row.UserId, Domain: domain)))
            .Concat(exports.Select(row => (row.UserId, row.Domain))).GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Domain).Distinct(StringComparer.Ordinal).Count());
    }

    private sealed class HourlyCount
    {
        public string UserId { get; set; } = string.Empty;
        public int UniqueSites { get; set; }
    }
}
