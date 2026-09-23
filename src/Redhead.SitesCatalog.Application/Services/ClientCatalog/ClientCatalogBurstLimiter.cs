using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.ClientCatalog;

// Shared by all requests in this app instance. Check and reservation are atomic:
// recording activity after the response cannot protect against concurrent requests.
public sealed class ClientCatalogBurstLimiter
{
    private readonly TimeProvider _clock;
    private readonly int _uniqueSitesLimit;
    private readonly object _sync = new();
    private readonly Dictionary<string, Dictionary<string, DateTimeOffset>> _users = new(StringComparer.Ordinal);
    private DateTimeOffset _nextCleanupUtc = DateTimeOffset.MinValue;

    public ClientCatalogBurstLimiter(TimeProvider clock, int uniqueSitesLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(uniqueSitesLimit);
        _clock = clock;
        _uniqueSitesLimit = uniqueSitesLimit;
    }

    public void EnsureAllowed(string userId, IReadOnlyCollection<string> domains, int selectionLimit, bool consume = true)
    {
        var requestedDomains = domains.ToHashSet(StringComparer.Ordinal);
        // An explicitly granted selection must fit in a single request.
        var limit = Math.Max(_uniqueSitesLimit, selectionLimit);
        if (requestedDomains.Count > limit)
        {
            throw new RequestValidationException("This selection exceeds your five-minute catalog limit. Narrow your filters and try again.");
        }

        lock (_sync)
        {
            var nowUtc = _clock.GetUtcNow();
            var cutoffUtc = nowUtc - ClientCatalogLimits.BurstWindow;
            CleanupExpiredUsers(nowUtc, cutoffUtc);
            var issued = _users.GetValueOrDefault(userId) ?? new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            foreach (var domain in issued.Where(item => item.Value <= cutoffUtc).Select(item => item.Key).ToArray())
            {
                issued.Remove(domain);
            }

            var newSites = requestedDomains.Count(domain => !issued.ContainsKey(domain));
            var excessSites = issued.Count + newSites - limit;
            if (excessSites > 0)
            {
                // Only domains outside this selection free capacity when they expire.
                var retryAt = issued.Where(item => !requestedDomains.Contains(item.Key))
                    .Select(item => item.Value).Order().ElementAt(excessSites - 1)
                    + ClientCatalogLimits.BurstWindow;
                throw new ClientCatalogBurstLimitExceededException(retryAt, nowUtc);
            }

            if (!consume)
            {
                return;
            }
            foreach (var domain in requestedDomains)
            {
                issued[domain] = nowUtc;
            }
            _users[userId] = issued;
        }
    }

    public void Reset(string userId)
    {
        lock (_sync)
        {
            _users.Remove(userId);
        }
    }

    private void CleanupExpiredUsers(DateTimeOffset nowUtc, DateTimeOffset cutoffUtc)
    {
        if (nowUtc < _nextCleanupUtc)
        {
            return;
        }
        foreach (var userId in _users.Where(user => user.Value.Values.All(timestamp => timestamp <= cutoffUtc))
                     .Select(user => user.Key).ToArray())
        {
            _users.Remove(userId);
        }
        _nextCleanupUtc = nowUtc + ClientCatalogLimits.BurstWindow;
    }
}
