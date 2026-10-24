using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public sealed record LinkDetectivePricesResult(
    string Domain, decimal? MinimumUsd, decimal? AverageUsd, decimal? MedianUsd,
    DateTime? CheckedAtUtc, bool IsStale, string? Message, DateTime? RetryAfterUtc);

public interface ILinkDetectivePricesService
{
    Task<LinkDetectivePricesResult?> GetAsync(string domain, bool forceRefresh, CancellationToken cancellationToken);
}

public sealed class LinkDetectivePricesService(
    ApplicationDbContext context,
    ILinkDetectiveApiClient api,
    IOptions<LinkDetectiveOptions> options,
    LinkDetectiveRequestCoordinator coordinator,
    TimeProvider clock) : ILinkDetectivePricesService
{
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(1);

    public async Task<LinkDetectivePricesResult?> GetAsync(string domain, bool forceRefresh, CancellationToken cancellationToken)
    {
        domain = DomainNormalizer.Normalize(domain) ?? string.Empty;
        if (!await context.Sites.AsNoTracking().AnyAsync(site => site.Domain == domain, cancellationToken))
        {
            return null;
        }

        var cache = await context.LinkDetectivePriceCaches.AsNoTracking()
            .SingleOrDefaultAsync(row => row.SiteDomain == domain, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (string.IsNullOrWhiteSpace(options.Value.ApiToken))
        {
            return Result(domain, cache, now, "LinkDetective is not configured. Contact an administrator.");
        }

        if ((cache?.CheckedAtUtc is not null || cache?.LastError is not null) && CanUseCache(cache, forceRefresh, now))
        {
            return Result(domain, cache, now);
        }

        using var refresh = await coordinator.BeginRefreshAsync(cancellationToken);
        // Another request may have refreshed the domain while this request was waiting.
        cache = await context.LinkDetectivePriceCaches.SingleOrDefaultAsync(row => row.SiteDomain == domain, cancellationToken);
        now = clock.GetUtcNow().UtcDateTime;
        if (CanUseCache(cache, forceRefresh, now))
        {
            return Result(domain, cache, now);
        }

        if (cache is null)
        {
            cache = new LinkDetectivePriceCache { SiteDomain = domain };
            context.LinkDetectivePriceCaches.Add(cache);
        }
        // Persist a short cooldown before calling the paid API, including for cancelled requests.
        cache.RetryAfterUtc = now.Add(RefreshCooldown);
        await context.SaveChangesAsync(cancellationToken);
        try
        {
            var prices = await api.GetPricesAsync(domain, cancellationToken);
            cache.MinimumUsd = prices.MinimumUsd;
            cache.AverageUsd = prices.AverageUsd;
            cache.MedianUsd = prices.MedianUsd;
            cache.CheckedAtUtc = clock.GetUtcNow().UtcDateTime;
            cache.RetryAfterUtc = cache.CheckedAtUtc.Value.Add(RefreshCooldown);
            cache.LastError = null;
        }
        catch (LinkDetectiveApiException exception)
        {
            cache.LastError = exception.Message;
            var minimumRetryAt = clock.GetUtcNow().UtcDateTime.Add(RefreshCooldown);
            cache.RetryAfterUtc = exception.RetryAfterUtc > minimumRetryAt
                ? exception.RetryAfterUtc : minimumRetryAt;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result(domain, cache, clock.GetUtcNow().UtcDateTime);
    }

    private bool CanUseCache(LinkDetectivePriceCache? cache, bool forceRefresh, DateTime now)
        => cache?.RetryAfterUtc > now ||
            !forceRefresh && cache?.CheckedAtUtc is { } checkedAt && !IsStale(checkedAt, now);

    private bool IsStale(DateTime checkedAt, DateTime now)
        => (now - checkedAt).TotalDays > options.Value.CacheLifetimeDays;

    private LinkDetectivePricesResult Result(string domain, LinkDetectivePriceCache? cache, DateTime now, string? message = null)
        => new(domain, cache?.MinimumUsd, cache?.AverageUsd, cache?.MedianUsd, cache?.CheckedAtUtc,
            cache?.CheckedAtUtc is { } checkedAt && IsStale(checkedAt, now),
            message ?? cache?.LastError ?? (cache?.CheckedAtUtc is not null && cache.MinimumUsd is null
                ? "No market prices found for this domain."
                : cache is { CheckedAtUtc: null, RetryAfterUtc: not null } ? "Prices are not available yet. Try again shortly." : null),
            cache?.RetryAfterUtc > now ? cache.RetryAfterUtc : null);
}
