namespace Redhead.SitesCatalog.Infrastructure.Concurrency;

public interface IAhrefsSyncLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);
}
