using Microsoft.Extensions.Caching.Memory;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;

public sealed class AhrefsLimitsProvider : IAhrefsLimitsProvider
{
    private const string CacheKey = "ahrefs-limits-and-usage";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly IAhrefsApiClient _apiClient;
    private readonly IMemoryCache _cache;

    public AhrefsLimitsProvider(
        IAhrefsApiClient apiClient,
        IMemoryCache cache)
    {
        _apiClient = apiClient;
        _cache = cache;
    }

    public async Task<AhrefsLimitsSnapshot> GetAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!forceRefresh &&
            _cache.TryGetValue(CacheKey, out AhrefsLimitsSnapshot? cached) &&
            cached != null)
        {
            return cached;
        }

        var limits = await _apiClient.GetLimitsAndUsageAsync(cancellationToken);
        var snapshot = new AhrefsLimitsSnapshot(limits, DateTime.UtcNow);
        _cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }
}
