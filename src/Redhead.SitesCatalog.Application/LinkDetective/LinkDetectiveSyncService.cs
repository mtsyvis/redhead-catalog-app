using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Infrastructure.Concurrency;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Coordination;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Persistence;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.LinkDetective;

public sealed class LinkDetectiveSyncService(
    ApplicationDbContext context,
    ILinkDetectiveApiClient api,
    ILinkDetectiveSyncLock syncLock,
    LinkDetectiveRequestCoordinator coordinator,
    IOptions<LinkDetectiveOptions> providerOptions,
    IOptions<LinkDetectiveSyncOptions> syncOptions,
    TimeProvider clock)
{
    private static readonly TimeSpan IdleRetryDelay = TimeSpan.FromMinutes(1);

    private readonly LinkDetectiveSyncOptions _options = syncOptions.Value;
    private readonly LinkDetectiveSyncRunScheduler _scheduler = new(context, providerOptions.Value, syncOptions.Value);
    private readonly LinkDetectiveSyncBatchProcessor _batches = new(context, api, providerOptions.Value, syncOptions.Value, clock);

    // One bounded batch per scope. No database/global refresh lock is held while waiting for quota.
    public async Task<DateTime> TickAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!_options.Enabled || string.IsNullOrWhiteSpace(providerOptions.Value.ApiToken))
        {
            return now.Add(IdleRetryDelay);
        }

        await using var workerLock = await syncLock.TryAcquireAsync(cancellationToken);
        if (workerLock is null)
        {
            return now.Add(IdleRetryDelay);
        }

        using var refresh = await coordinator.BeginRefreshAsync(cancellationToken);
        now = clock.GetUtcNow().UtcDateTime;
        var state = await LinkDetectiveQuotaPersistence.GetOrCreateStateAsync(context, cancellationToken);
        var run = await _scheduler.GetOrStartScheduledRunAsync(state, now, cancellationToken);
        if (run is null)
        {
            return state.NextSelectionAtUtc!.Value;
        }
        if (run.NextAttemptAtUtc > now)
        {
            return run.NextAttemptAtUtc.Value;
        }

        var batch = await _batches.PrepareBatchAsync(run, now, cancellationToken);
        if (batch.Items.Count == 0)
        {
            return await _scheduler.FinishOrWaitAsync(run, now, cancellationToken);
        }

        var delay = await _batches.RequestAsync(run, state, batch, now, cancellationToken);
        now = clock.GetUtcNow().UtcDateTime;
        // Commit confirmed cache results, item progress, quota and run status together.
        return delay is not null
            ? await _scheduler.WaitOrCompleteAsync(run, delay, now, cancellationToken)
            : await _scheduler.FinishOrWaitAsync(run, now, cancellationToken);
    }
}
