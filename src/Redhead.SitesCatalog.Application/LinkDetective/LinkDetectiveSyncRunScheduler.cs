using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.LinkDetective;

// Owns durable run selection and the next scheduler wake-up.
internal sealed class LinkDetectiveSyncRunScheduler(
    ApplicationDbContext context, LinkDetectiveOptions providerOptions, LinkDetectiveSyncOptions options)
{
    private static readonly TimeSpan NextBatchDelay = TimeSpan.FromSeconds(2);

    public async Task<LinkDetectiveSyncRun?> GetOrStartScheduledRunAsync(
        LinkDetectiveSyncState state, DateTime now, CancellationToken cancellationToken)
    {
        var run = await context.LinkDetectiveSyncRuns.SingleOrDefaultAsync(
            row => row.Status == LinkDetectiveSyncRunStatus.Running || row.Status == LinkDetectiveSyncRunStatus.Waiting, cancellationToken);
        if (run is null)
        {
            if (state.NextSelectionAtUtc is { } scheduled && options.SelectionOn(scheduled) != scheduled)
            {
                // A configuration change must also update the durable future occurrence.
                state.NextSelectionAtUtc = options.SelectionOn(scheduled);
                await context.SaveChangesAsync(cancellationToken);
            }
            if (state.NextSelectionAtUtc > now)
            {
                return null;
            }

            state.NextSelectionAtUtc = options.NextSelection(now);
            run = await CreateRunForDueDomainsAsync(now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return run;
    }

    private async Task<LinkDetectiveSyncRun?> CreateRunForDueDomainsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var cutoff = providerOptions.FreshnessCutoffUtc(now);
        var due = context.Sites.Where(site => !context.LinkDetectivePriceCaches.Any(cache =>
            cache.SiteDomain == site.Domain && cache.CheckedAtUtc >= cutoff));
        if (!await due.AnyAsync(cancellationToken))
        {
            return null;
        }

        await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        var run = new LinkDetectiveSyncRun { Id = Guid.NewGuid(), StartedAtUtc = now };
        context.LinkDetectiveSyncRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);
        if (context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "LinkDetectiveSyncRunItems" ("RunId", "Domain", "Status", "Attempts")
                SELECT {run.Id}, s."Domain", 'Pending', 0 FROM "Sites" s
                WHERE NOT EXISTS (SELECT 1 FROM "LinkDetectivePriceCaches" c
                    WHERE c."SiteDomain" = s."Domain" AND c."CheckedAtUtc" >= {cutoff})
                """, cancellationToken);
        }
        else
        {
            context.LinkDetectiveSyncRunItems.AddRange((await due.Select(site => site.Domain).ToListAsync(cancellationToken))
                .Select(domain => new LinkDetectiveSyncRunItem { RunId = run.Id, Domain = domain }));
        }

        await context.SaveChangesAsync(cancellationToken);
        run.SelectedCount = await context.LinkDetectiveSyncRunItems.CountAsync(row => row.RunId == run.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return run;
    }

    public async Task<DateTime> FinishOrWaitAsync(LinkDetectiveSyncRun run, DateTime now, CancellationToken cancellationToken)
    {
        run.WaitingReason = null;
        run.NextAttemptAtUtc = null;
        run.Status = LinkDetectiveSyncRunStatus.Running;
        var nextAttempt = now.Add(NextBatchDelay);
        if (run.ProcessedCount == run.SelectedCount)
        {
            run.Status = run.FailedCount == 0 ? LinkDetectiveSyncRunStatus.Completed : LinkDetectiveSyncRunStatus.CompletedWithErrors;
            run.FinishedAtUtc = now;
        }
        else
        {
            // Persist item results before querying the remaining work.
            await context.SaveChangesAsync(cancellationToken);
            var next = await context.LinkDetectiveSyncRunItems.Where(row => row.RunId == run.Id &&
                (row.Status == LinkDetectiveSyncItemStatus.Pending || row.Status == LinkDetectiveSyncItemStatus.InFlight))
                .Select(row => row.NextAttemptAtUtc ?? now).MinAsync(cancellationToken);
            if (next > now)
            {
                SetWaiting(run, LinkDetectiveSyncMessages.WaitingForRetry, next);
                nextAttempt = next;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return nextAttempt;
    }

    public async Task<DateTime> WaitOrCompleteAsync(
        LinkDetectiveSyncRun run, LinkDetectiveSyncDelay delay, DateTime now, CancellationToken cancellationToken)
    {
        if (run.ProcessedCount == run.SelectedCount)
        {
            return await FinishOrWaitAsync(run, now, cancellationToken);
        }

        SetWaiting(run, delay.Reason, delay.RetryAtUtc);
        await context.SaveChangesAsync(cancellationToken);
        return delay.RetryAtUtc;
    }

    private static void SetWaiting(LinkDetectiveSyncRun run, string reason, DateTime retryAt)
    {
        run.Status = LinkDetectiveSyncRunStatus.Waiting;
        run.WaitingReason = reason;
        run.NextAttemptAtUtc = retryAt;
    }
}
