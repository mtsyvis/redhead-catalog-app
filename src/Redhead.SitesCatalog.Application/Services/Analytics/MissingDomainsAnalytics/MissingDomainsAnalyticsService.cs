using Microsoft.EntityFrameworkCore;
using Npgsql;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.Analytics.MissingDomainsAnalytics;

public sealed class MissingDomainsAnalyticsService(ApplicationDbContext context) : IMissingDomainsAnalyticsService
{
    public async Task RecordAsync(string userId, string role, Guid requestId,
        IReadOnlyList<string> missingDomains, CancellationToken cancellationToken = default)
    {
        if (role is not (AppRoles.Client or AppRoles.Lite))
        {
            return;
        }

        if (await context.MultiSearchAnalyticsRequests.AnyAsync(
            x => x.UserId == userId && x.RequestId == requestId, cancellationToken))
        {
            return;
        }

        // Keep the request even when nothing was missing, so retrying it later cannot add new events.
        var search = new MultiSearchAnalyticsRequest
        {
            Id = Guid.NewGuid(), RequestId = requestId, UserId = userId, Role = role,
            SearchedAtUtc = DateTime.UtcNow,
            // These keys have already been normalized by the search parser. Preserve exact matching.
            MissingDomains = missingDomains.Where(DomainValidator.IsValidDnsDomain).Distinct(StringComparer.Ordinal)
                .Select(domain => new MissingDomainSearch { Domain = domain }).ToList()
        };
        context.MultiSearchAnalyticsRequests.Add(search);
        try
        {
            // EF saves the request and all its domains in one transaction.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_MultiSearchAnalyticsRequests_UserId_RequestId"
        })
        {
            // Another concurrent delivery of this logical search already won.
            foreach (var domain in search.MissingDomains.ToArray())
            {
                context.Entry(domain).State = EntityState.Detached;
            }
            context.Entry(search).State = EntityState.Detached;
        }
    }

    public async Task<MissingDomainsAnalyticsDto> GetAsync(MissingDomainsAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = context.MissingDomainSearches.AsNoTracking().AsQueryable();
        if (query.FromUtc is { } from)
        {
            rows = rows.Where(x => x.Search.SearchedAtUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            rows = rows.Where(x => x.Search.SearchedAtUtc < to);
        }

        if (query.Role is { } role)
        {
            rows = rows.Where(x => x.Search.Role == role);
        }

        if (query.Domain is { Length: > 0 } domain)
        {
            rows = rows.Where(x => x.Domain.Contains(domain));
        }

        if (query.IsInCatalog is { } inCatalog)
        {
            rows = rows.Where(x => context.Sites.Any(site => site.Domain == x.Domain) == inCatalog);
        }

        var totalDomains = await rows.Select(x => x.Domain).Distinct().CountAsync(cancellationToken);
        // Catalog status can change between visits; keep pagination within the remaining selection.
        var lastPage = totalDomains == 0 ? 1 : (totalDomains - 1) / query.PageSize + 1;
        var pageNumber = Math.Min(query.Page, lastPage);
        var searches = await rows.LongCountAsync(cancellationToken);
        var users = await rows.Select(x => x.Search.UserId).Distinct().CountAsync(cancellationToken);
        var page = await rows.GroupBy(x => x.Domain).Select(group => new
            {
                Domain = group.Key, Searches = group.LongCount(),
                UniqueUsers = group.Select(x => x.Search.UserId).Distinct().Count(),
                First = group.Min(x => x.Search.SearchedAtUtc), Last = group.Max(x => x.Search.SearchedAtUtc)
            })
            .OrderByDescending(x => x.Searches).ThenBy(x => x.Domain)
            .Skip((pageNumber - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var pageDomains = page.Select(x => x.Domain).ToArray();
        var present = (await context.Sites.AsNoTracking().Where(x => pageDomains.Contains(x.Domain))
            .Select(x => x.Domain).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        return new MissingDomainsAnalyticsDto(totalDomains, searches, users,
            page.Select(x => new MissingDomainAnalyticsRow(x.Domain, x.Searches, x.UniqueUsers,
                x.First, x.Last, present.Contains(x.Domain))).ToArray(), pageNumber, query.PageSize);
    }
}
