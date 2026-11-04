using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Constants;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Errors;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Persistence;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.LinkDetective;

internal sealed record LinkDetectiveSyncBatch(
    List<LinkDetectiveSyncRunItem> Items, Dictionary<string, LinkDetectivePriceCache> Caches);

internal sealed record LinkDetectiveSyncDelay(string Reason, DateTime RetryAtUtc);

// Processes one bounded batch. The caller commits its result and run status together.
internal sealed class LinkDetectiveSyncBatchProcessor(
    ApplicationDbContext context, ILinkDetectiveApiClient api,
    LinkDetectiveOptions providerOptions, LinkDetectiveSyncOptions options, TimeProvider clock)
{
    private static readonly TimeSpan DomainRefreshCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan QuotaRecheckDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan QuotaPoolMismatchDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan AccessDeniedRetryDelay = TimeSpan.FromHours(1);

    public async Task<LinkDetectiveSyncBatch> PrepareBatchAsync(
        LinkDetectiveSyncRun run, DateTime now, CancellationToken cancellationToken)
    {
        var items = await context.LinkDetectiveSyncRunItems
            .Where(row => row.RunId == run.Id &&
                (row.Status == LinkDetectiveSyncItemStatus.Pending || row.Status == LinkDetectiveSyncItemStatus.InFlight) &&
                (row.NextAttemptAtUtc == null || row.NextAttemptAtUtc <= now))
            .OrderBy(row => row.Attempts)
            .ThenBy(row => row.Domain)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return new([], []);
        }

        var domains = items.Select(row => row.Domain).ToArray();
        var existingDomains = await context.Sites
            .Where(row => domains.Contains(row.Domain))
            .Select(row => row.Domain)
            .ToHashSetAsync(cancellationToken);
        var caches = await context.LinkDetectivePriceCaches
            .Where(row => domains.Contains(row.SiteDomain))
            .ToDictionaryAsync(row => row.SiteDomain, cancellationToken);
        var freshnessCutoff = providerOptions.FreshnessCutoffUtc(now);
        var toRequest = new List<LinkDetectiveSyncRunItem>();
        foreach (var item in items)
        {
            if (!existingDomains.Contains(item.Domain))
            {
                LinkDetectiveSyncProgress.Complete(run, item, LinkDetectiveSyncItemStatus.Skipped);
                continue;
            }
            caches.TryGetValue(item.Domain, out var cache);
            if (cache?.CheckedAtUtc >= freshnessCutoff)
            {
                item.CheckedAtUtc = cache.CheckedAtUtc;
                LinkDetectiveSyncProgress.Complete(run, item, LinkDetectiveSyncItemStatus.Reused);
                continue;
            }
            if (item.Status == LinkDetectiveSyncItemStatus.InFlight && item.Attempts >= options.MaxAttempts)
            {
                item.Error = LinkDetectiveSyncMessages.InterruptedRequest;
                LinkDetectiveSyncProgress.Complete(run, item, LinkDetectiveSyncItemStatus.Failed);
                continue;
            }
            if (cache?.RetryAfterUtc > now)
            {
                item.NextAttemptAtUtc = cache.RetryAfterUtc;
                continue;
            }

            toRequest.Add(item);
        }
        await context.SaveChangesAsync(cancellationToken);
        return new(toRequest, caches);
    }

    public async Task<LinkDetectiveSyncDelay?> RequestAsync(
        LinkDetectiveSyncRun run, LinkDetectiveSyncState state, LinkDetectiveSyncBatch batch,
        DateTime now, CancellationToken cancellationToken)
    {
        var toRequest = batch.Items;
        var caches = batch.Caches;
        try
        {
            // Free preview before every paid batch: restart/timeout/manual traffic may have spent quota.
            var quota = await api.GetQuotaAsync(cancellationToken);
            if (HasDifferentActiveQuotaPool(state, quota, now))
            {
                return new(LinkDetectiveSyncMessages.QuotaPoolMismatch, now.Add(QuotaPoolMismatchDelay));
            }

            LinkDetectiveQuotaPersistence.ApplyObservation(state, quota, clock.GetUtcNow().UtcDateTime);
            var affordable = Math.Max(0, quota.Remaining - options.OnDemandReserve);
            if (affordable == 0)
            {
                var nextQuotaCheck = quota.ResetAtUtc > now ? quota.ResetAtUtc.Value : now.Add(QuotaRecheckDelay);
                return new(LinkDetectiveSyncMessages.ReservedQuota, nextQuotaCheck);
            }

            toRequest = toRequest.Take((int)Math.Min(affordable, options.BatchSize)).ToList();
            now = clock.GetUtcNow().UtcDateTime;
            await SaveBatchInFlightAsync(toRequest, caches, now, cancellationToken);
            var response = await api.GetBatchPricesAsync(toRequest.Select(row => row.Domain).ToArray(), cancellationToken);
            now = clock.GetUtcNow().UtcDateTime;
            ApplyBatchResults(run, toRequest, caches, response, now);
            return ObserveBatchQuota(state, quota, response.Quota, now);
        }
        catch (LinkDetectiveApiException exception)
        {
            return HandleBatchFailure(run, toRequest, caches, exception);
        }
    }

    private static bool HasDifferentActiveQuotaPool(LinkDetectiveSyncState state, LinkDetectiveQuota quota, DateTime now)
    {
        var hasActiveObservation = state.QuotaResetAtUtc > now && state.QuotaPeriod is not null;
        var hasDifferentPool = state.QuotaPeriod != quota.Period || state.QuotaLimit != quota.Limit;
        return hasActiveObservation && hasDifferentPool;
    }

    private static LinkDetectiveSyncDelay? ObserveBatchQuota(
        LinkDetectiveSyncState state, LinkDetectiveQuota previewQuota, LinkDetectiveQuota? batchQuota, DateTime now)
    {
        if (batchQuota is null)
        {
            // The pre-request balance cannot be presented as a post-request observation.
            state.QuotaObservedAtUtc = null;
            return new(LinkDetectiveSyncMessages.MissingBatchQuota, now.Add(QuotaRecheckDelay));
        }

        LinkDetectiveQuotaPersistence.ApplyObservation(state, batchQuota, now);
        if (batchQuota.Period != previewQuota.Period || batchQuota.Limit != previewQuota.Limit)
        {
            return new(LinkDetectiveSyncMessages.QuotaPoolMismatch, now.Add(QuotaPoolMismatchDelay));
        }

        return null;
    }

    private async Task SaveBatchInFlightAsync(
        List<LinkDetectiveSyncRunItem> items, Dictionary<string, LinkDetectivePriceCache> caches,
        DateTime now, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            item.Status = LinkDetectiveSyncItemStatus.InFlight;
            item.Attempts++;
            item.AttemptedAtUtc = now;
            item.NextAttemptAtUtc = now.AddMinutes(options.RetryDelayMinutes);
            if (!caches.TryGetValue(item.Domain, out var cache))
            {
                cache = new() { SiteDomain = item.Domain };
                context.LinkDetectivePriceCaches.Add(cache);
                caches[item.Domain] = cache;
            }
            cache.RetryAfterUtc = now.Add(DomainRefreshCooldown);
        }
        // Persist an uncertain in-flight attempt BEFORE the paid API, for safe restart recovery.
        await context.SaveChangesAsync(cancellationToken);
    }

    private void ApplyBatchResults(
        LinkDetectiveSyncRun run, List<LinkDetectiveSyncRunItem> items,
        Dictionary<string, LinkDetectivePriceCache> caches, LinkDetectiveBatchPricesResponse response, DateTime now)
    {
        foreach (var item in items)
        {
            var cache = caches[item.Domain];
            if (!response.PricesByDomain.TryGetValue(item.Domain, out var prices))
            {
                LinkDetectiveSyncProgress.RetryOrFail(run, item, LinkDetectiveMessages.IncompletePrices, now, options);
                cache.LastError = item.Error;
                cache.RetryAfterUtc = item.NextAttemptAtUtc ?? now.Add(DomainRefreshCooldown);
                continue;
            }

            cache.MinimumUsd = prices.MinimumUsd;
            cache.AverageUsd = prices.AverageUsd;
            cache.MedianUsd = prices.MedianUsd;
            cache.CheckedAtUtc = now;
            cache.RetryAfterUtc = now.Add(DomainRefreshCooldown);
            cache.LastError = null;
            item.CheckedAtUtc = now;
            var status = prices.MinimumUsd is null
                ? LinkDetectiveSyncItemStatus.NotFound
                : LinkDetectiveSyncItemStatus.PricesFound;
            LinkDetectiveSyncProgress.Complete(run, item, status);
        }
    }

    private LinkDetectiveSyncDelay HandleBatchFailure(
        LinkDetectiveSyncRun run, List<LinkDetectiveSyncRunItem> items,
        Dictionary<string, LinkDetectivePriceCache> caches, LinkDetectiveApiException exception)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var item in items.Where(row => row.Status == LinkDetectiveSyncItemStatus.InFlight))
        {
            if (exception.Kind == LinkDetectiveFailureKind.Transient)
            {
                LinkDetectiveSyncProgress.RetryOrFail(run, item, exception.Message, now, options);
            }
            else
            {
                item.Status = LinkDetectiveSyncItemStatus.Pending;
                item.NextAttemptAtUtc = exception.RetryAfterUtc ?? now.Add(DomainRefreshCooldown);
            }
            if (caches.TryGetValue(item.Domain, out var cache))
            {
                cache.LastError = exception.Message;
                cache.RetryAfterUtc = item.NextAttemptAtUtc ?? now.Add(DomainRefreshCooldown);
            }
        }
        var retryAt = exception.Kind == LinkDetectiveFailureKind.AccessDenied
            ? now.Add(AccessDeniedRetryDelay)
            : now.AddMinutes(options.RetryDelayMinutes);
        if (exception.RetryAfterUtc > retryAt)
        {
            retryAt = exception.RetryAfterUtc.Value;
        }

        return new(exception.Message, retryAt);
    }
}
