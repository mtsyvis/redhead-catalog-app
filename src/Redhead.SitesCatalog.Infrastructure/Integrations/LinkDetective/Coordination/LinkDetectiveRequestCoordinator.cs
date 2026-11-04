using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Constants;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Errors;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Coordination;

// Shared by requests in the single app instance used by the Docker Compose deployment.
public sealed class LinkDetectiveRequestCoordinator(TimeProvider clock) : IDisposable
{
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim _refreshLock = new(1);
    private readonly SemaphoreSlim _apiLock = new(1);
    private DateTime _nextRequestUtc;
    private DateTime _blockedUntilUtc;

    public async Task<IDisposable> BeginRefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        return new ReleaseHandle(_refreshLock);
    }

    public async Task<IDisposable> BeginRequestAsync(CancellationToken cancellationToken)
    {
        await _apiLock.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow().UtcDateTime;
            if (_blockedUntilUtc > now)
            {
                throw new LinkDetectiveApiException(LinkDetectiveMessages.TemporarilyUnavailable, _blockedUntilUtc);
            }

            var delay = _nextRequestUtc - now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, clock, cancellationToken);
            }

            _nextRequestUtc = clock.GetUtcNow().UtcDateTime.Add(MinimumRequestInterval);
            return new ReleaseHandle(_apiLock);
        }
        catch
        {
            _apiLock.Release();
            throw;
        }
    }

    // Called while holding the API lock. A provider cooldown applies to every domain.
    public void BlockUntil(DateTime retryAfterUtc) => _blockedUntilUtc = retryAfterUtc;

    public void Dispose()
    {
        _apiLock.Dispose();
        _refreshLock.Dispose();
    }

    private sealed class ReleaseHandle(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
