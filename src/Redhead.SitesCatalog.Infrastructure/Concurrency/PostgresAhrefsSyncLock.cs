using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Infrastructure.Concurrency;

public sealed class PostgresAhrefsSyncLock : IAhrefsSyncLock
{
    private const long LockId = 724_346_737_473;
    private readonly ApplicationDbContext _context;

    public PostgresAhrefsSyncLock(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@lock_id)";
        AddParameter(command, LockId);
        var acquired = (bool?)await command.ExecuteScalarAsync(cancellationToken) == true;
        return acquired ? new Handle(connection) : null;
    }

    private static void AddParameter(DbCommand command, long value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "lock_id";
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed class Handle : IAsyncDisposable
    {
        private readonly DbConnection _connection;

        public Handle(DbConnection connection)
        {
            _connection = connection;
        }

        public async ValueTask DisposeAsync()
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@lock_id)";
            AddParameter(command, LockId);
            await command.ExecuteScalarAsync();
        }
    }
}
