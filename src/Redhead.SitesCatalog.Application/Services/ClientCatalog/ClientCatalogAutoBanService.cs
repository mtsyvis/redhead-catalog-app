using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.ClientCatalog;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Services.ClientCatalog;

public sealed class ClientCatalogAutoBanService(
    ApplicationDbContext db,
    IOptions<ClientCatalogOptions> options,
    IClientCatalogAlertEmailSender emailSender,
    TimeProvider clock)
{
    public static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EmailRetryInterval = TimeSpan.FromMinutes(5);
    private const int EmailBatchSize = 20;

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await DetectAutoBansAsync(cancellationToken);
        await SendPendingEmailsAsync(cancellationToken);
    }

    private async Task DetectAutoBansAsync(CancellationToken cancellationToken)
    {
        var settings = await db.ClientCatalogProtectionSettings.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == ClientCatalogProtectionSettings.SingletonId, cancellationToken);
        if (settings?.AutoBanEnabled != true)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var counts = db.Database.IsNpgsql()
            ? await GetEligibleCountsInPostgresAsync(now, settings.AutoBanUniqueSitesPer24Hours, cancellationToken)
            : await GetEligibleCountsInMemoryAsync(now, settings.AutoBanUniqueSitesPer24Hours, cancellationToken);
        if (counts.Count == 0)
        {
            return;
        }

        var userIds = counts.Keys.ToArray();
        var clientRoleIds = db.Roles.Where(role => role.Name == AppRoles.Client).Select(role => role.Id);
        var users = await db.Users.Where(user =>
                userIds.Contains(user.Id) &&
                user.IsActive &&
                !user.IsTrustedClient &&
                db.UserRoles.Any(role => role.UserId == user.Id && clientRoleIds.Contains(role.RoleId)))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var usersWithOpenBan = (await db.ClientCatalogAutoBans.AsNoTracking()
            .Where(autoBan => userIds.Contains(autoBan.UserId) && autoBan.ReviewedAtUtc == null)
            .Select(autoBan => autoBan.UserId)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (userId, uniqueSites) in counts)
        {
            if (!users.TryGetValue(userId, out var user) || usersWithOpenBan.Contains(userId))
            {
                continue;
            }

            user.IsActive = false;
            user.DisabledReason = UserDisabledReasons.ClientCatalogAutoBan;
            user.DisabledAtUtc = now;
            db.ClientCatalogAutoBans.Add(new ClientCatalogAutoBan
            {
                UserId = userId,
                DetectedAtUtc = now,
                UniqueSites = uniqueSites,
                Threshold = settings.AutoBanUniqueSitesPer24Hours
            });
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
        var pending = await db.ClientCatalogAutoBans
            .Where(autoBan => autoBan.ReviewedAtUtc == null && autoBan.EmailSentAtUtc == null &&
                (autoBan.NextEmailAttemptAtUtc == null || autoBan.NextEmailAttemptAtUtc <= now))
            .OrderBy(autoBan => autoBan.DetectedAtUtc)
            .Take(EmailBatchSize)
            .ToListAsync(cancellationToken);

        foreach (var autoBan in pending)
        {
            var userEmail = await db.Users.Where(user => user.Id == autoBan.UserId)
                .Select(user => user.Email)
                .SingleAsync(cancellationToken);
            var sent = await emailSender.SendAutoBanAsync(
                autoBan,
                userEmail ?? autoBan.UserId,
                cancellationToken);
            var attemptedAt = clock.GetUtcNow().UtcDateTime;
            if (sent)
            {
                autoBan.EmailSentAtUtc = attemptedAt;
            }
            autoBan.NextEmailAttemptAtUtc = attemptedAt + EmailRetryInterval;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<string, int>> GetEligibleCountsInPostgresAsync(
        DateTime now,
        int threshold,
        CancellationToken cancellationToken)
    {
        var start = now - ClientCatalogAutoBanWindows.Rolling24Hours;
        return await db.Database.SqlQuery<DailyCount>($"""
            SELECT eligible."UserId", count(DISTINCT issued.domain)::int AS "UniqueSites"
            FROM (
                SELECT users."Id" AS "UserId",
                    GREATEST({start}, COALESCE(users."ClientCatalogAutoBanResetAtUtc", {start})) AS "WindowStart"
                FROM "AspNetUsers" AS users
                WHERE users."IsActive"
                    AND NOT users."IsTrustedClient"
                    AND EXISTS (
                        SELECT 1
                        FROM "AspNetUserRoles" AS user_roles
                        INNER JOIN "AspNetRoles" AS roles ON roles."Id" = user_roles."RoleId"
                        WHERE user_roles."UserId" = users."Id" AND roles."Name" = {AppRoles.Client})
            ) AS eligible
            CROSS JOIN LATERAL (
                SELECT unnest(requests."Domains") AS domain
                FROM "ClientCatalogRequests" AS requests
                WHERE requests."UserId" = eligible."UserId"
                    AND requests."TimestampUtc" > eligible."WindowStart"
                    AND requests."TimestampUtc" <= {now}
                UNION ALL
                SELECT exports."Domain" AS domain
                FROM "ExportedDomainAccesses" AS exports
                WHERE exports."UserId" = eligible."UserId"
                    AND exports."ExportedAtUtc" > eligible."WindowStart"
                    AND exports."ExportedAtUtc" <= {now}
            ) AS issued
            GROUP BY eligible."UserId"
            HAVING count(DISTINCT issued.domain) >= {threshold}
            """).ToDictionaryAsync(row => row.UserId, row => row.UniqueSites, cancellationToken);
    }

    private async Task<Dictionary<string, int>> GetEligibleCountsInMemoryAsync(
        DateTime now,
        int threshold,
        CancellationToken cancellationToken)
    {
        var clientRoleIds = db.Roles.Where(role => role.Name == AppRoles.Client).Select(role => role.Id);
        var users = await db.Users.AsNoTracking()
            .Where(user => user.IsActive &&
                !user.IsTrustedClient &&
                db.UserRoles.Any(role => role.UserId == user.Id && clientRoleIds.Contains(role.RoleId)))
            .Select(user => new { user.Id, user.ClientCatalogAutoBanResetAtUtc })
            .ToListAsync(cancellationToken);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var user in users)
        {
            var start = now - ClientCatalogAutoBanWindows.Rolling24Hours;
            if (user.ClientCatalogAutoBanResetAtUtc > start)
            {
                start = user.ClientCatalogAutoBanResetAtUtc.Value;
            }
            var requestDomains = await db.ClientCatalogRequests.AsNoTracking()
                .Where(row => row.UserId == user.Id && row.TimestampUtc > start && row.TimestampUtc <= now)
                .Select(row => row.Domains)
                .ToListAsync(cancellationToken);
            var exportDomains = await db.ExportedDomainAccesses.AsNoTracking()
                .Where(row => row.UserId == user.Id && row.ExportedAtUtc > start && row.ExportedAtUtc <= now)
                .Select(row => row.Domain)
                .ToListAsync(cancellationToken);
            var uniqueSites = requestDomains.SelectMany(domains => domains)
                .Concat(exportDomains)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (uniqueSites >= threshold)
            {
                counts[user.Id] = uniqueSites;
            }
        }
        return counts;
    }

    private sealed class DailyCount
    {
        public string UserId { get; set; } = string.Empty;
        public int UniqueSites { get; set; }
    }
}
