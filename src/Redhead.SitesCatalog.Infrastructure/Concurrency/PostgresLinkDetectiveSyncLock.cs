using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Infrastructure.Concurrency;

public interface ILinkDetectiveSyncLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);
}

public sealed class PostgresLinkDetectiveSyncLock(ApplicationDbContext context) : ILinkDetectiveSyncLock
{
    // All app instances use the same ID to coordinate the LinkDetective worker.
    private const long LockId = 724_346_737_474;

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        // The lock belongs to this PostgreSQL session. Keep its connection open
        // so the caller can release the lock on the same session after the batch.
        await context.Database.OpenConnectionAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT pg_try_advisory_lock({LockId})";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        var lockAcquired = result is true;
        if (!lockAcquired)
        {
            // Another worker holds the lock. Return immediately without waiting.
            return null;
        }

        // The caller uses await using to release the lock when batch processing ends.
        return new LockHandle(context);
    }

    private sealed class LockHandle(ApplicationDbContext context) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            var connection = context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT pg_advisory_unlock({LockId})";
            await command.ExecuteScalarAsync();
        }
    }
}
