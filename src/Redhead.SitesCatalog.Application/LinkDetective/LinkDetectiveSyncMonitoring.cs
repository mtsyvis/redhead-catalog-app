using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.LinkDetective;

public sealed record LinkDetectiveSyncPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record LinkDetectiveSyncStatus(
    bool Enabled, bool Configured, int CacheLifetimeDays, string DailySelectionTimeUtc,
    int BatchSize, int OnDemandReserve, int MaxAttempts, int RetryDelayMinutes,
    DateTime? NextSelectionAtUtc, int CatalogCount, int FreshCount, int OutdatedCount, int NeverCheckedCount,
    LinkDetectiveSyncState? ProviderState, LinkDetectiveSyncRun? ActiveRun);
public sealed record LinkDetectiveSyncDetails(LinkDetectiveSyncRun Run, LinkDetectiveSyncPage<LinkDetectiveSyncRunItem> Results);

public sealed class LinkDetectiveSyncMonitoring(
    ApplicationDbContext context, IOptions<LinkDetectiveOptions> providerOptions,
    IOptions<LinkDetectiveSyncOptions> options, TimeProvider clock)
{
    public async Task<LinkDetectiveSyncStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var cutoff = providerOptions.Value.FreshnessCutoffUtc(clock.GetUtcNow().UtcDateTime);
        var total = await context.Sites.CountAsync(cancellationToken);
        var fresh = await context.LinkDetectivePriceCaches.CountAsync(row => row.CheckedAtUtc >= cutoff, cancellationToken);
        var checkedCount = await context.LinkDetectivePriceCaches.CountAsync(row => row.CheckedAtUtc != null, cancellationToken);
        var state = await context.LinkDetectiveSyncStates.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var run = await context.LinkDetectiveSyncRuns.AsNoTracking().SingleOrDefaultAsync(row =>
            row.Status == LinkDetectiveSyncRunStatus.Running || row.Status == LinkDetectiveSyncRunStatus.Waiting, cancellationToken);
        var config = options.Value;
        return new(config.Enabled, !string.IsNullOrWhiteSpace(providerOptions.Value.ApiToken), providerOptions.Value.CacheLifetimeDays,
            config.DailySelectionTimeUtc, config.BatchSize, config.OnDemandReserve, config.MaxAttempts, config.RetryDelayMinutes,
            state?.NextSelectionAtUtc, total, fresh, checkedCount - fresh, total - checkedCount, state, run);
    }

    public async Task<LinkDetectiveSyncPage<LinkDetectiveSyncRun>> ListRunsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.LinkDetectiveSyncRuns.AsNoTracking();
        return new(await query.OrderByDescending(row => row.StartedAtUtc).ThenBy(row => row.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken),
            page, pageSize, await query.CountAsync(cancellationToken));
    }

    public async Task<LinkDetectiveSyncDetails?> GetRunAsync(Guid id, int page, int pageSize, string? domain,
        LinkDetectiveSyncItemStatus? status, CancellationToken cancellationToken)
    {
        var run = await context.LinkDetectiveSyncRuns.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var query = context.LinkDetectiveSyncRunItems.AsNoTracking().Where(row => row.RunId == id);
        if (!string.IsNullOrWhiteSpace(domain))
        {
            var search = domain.Trim().ToLowerInvariant();
            query = query.Where(row => row.Domain.Contains(search));
        }
        if (status is not null)
        {
            query = query.Where(row => row.Status == status);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(row => row.Domain).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(run, new(items, page, pageSize, count));
    }
}
