using System.Net;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.ClientCatalog;

public interface IClientCatalogActivityService
{
    Task<IReadOnlyList<ClientCatalogActivityWindow>> GetActivityAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class ClientCatalogActivityService(ApplicationDbContext db, TimeProvider clock) : IClientCatalogActivityService
{
    public async Task<IReadOnlyList<ClientCatalogActivityWindow>> GetActivityAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var activity = new List<ClientCatalogActivityWindow>();

        foreach (var period in ClientCatalogActivityPeriods.All)
        {
            var startUtc = nowUtc - period.Duration;
            var periodActivity = await GetPeriodActivityAsync(
                userId, period.Label, startUtc, nowUtc, cancellationToken);
            activity.Add(periodActivity);
        }

        return activity;
    }

    private async Task<ClientCatalogActivityWindow> GetPeriodActivityAsync(
        string userId, string label, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var requests = db.ClientCatalogRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId &&
                request.TimestampUtc >= startUtc && request.TimestampUtc <= endUtc);

        var requestCount = await requests.CountAsync(cancellationToken);
        var rateLimitedCount = await requests.CountAsync(
            request => request.StatusCode == (int)HttpStatusCode.TooManyRequests, cancellationToken);
        var uniqueSiteCount = db.Database.IsNpgsql()
            ? await CountUniqueSitesInPostgresAsync(userId, startUtc, endUtc, cancellationToken)
            : await CountUniqueSitesInMemoryAsync(requests, userId, startUtc, endUtc, cancellationToken);

        return new ClientCatalogActivityWindow(label, requestCount, rateLimitedCount, uniqueSiteCount);
    }

    private Task<int> CountUniqueSitesInPostgresAsync(
        string userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        return db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM (
                SELECT unnest("Domains") AS domain FROM "ClientCatalogRequests"
                WHERE "UserId" = {userId} AND "TimestampUtc" >= {startUtc} AND "TimestampUtc" <= {endUtc}
                UNION
                SELECT "Domain" FROM "ExportedDomainAccesses"
                WHERE "UserId" = {userId} AND "ExportedAtUtc" >= {startUtc} AND "ExportedAtUtc" <= {endUtc}
            ) AS domains
            """).SingleAsync(cancellationToken);
    }

    private async Task<int> CountUniqueSitesInMemoryAsync(
        IQueryable<ClientCatalogRequest> requests, string userId,
        DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var viewedDomains = await requests.Select(request => request.Domains).ToListAsync(cancellationToken);
        var exportedDomains = await db.ExportedDomainAccesses
            .Where(access => access.UserId == userId &&
                access.ExportedAtUtc >= startUtc && access.ExportedAtUtc <= endUtc)
            .Select(access => access.Domain)
            .ToListAsync(cancellationToken);

        return viewedDomains
            .SelectMany(domains => domains)
            .Concat(exportedDomains)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }
}
