using Redhead.SitesCatalog.Application.Services.Analytics.MissingDomainsAnalytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Integration;

// Opt-in: isolated databases, never the application's existing database.
public sealed class MissingDomainsAnalyticsPostgresTests : IAsyncLifetime
{
    private const string ConnectionVariable = "REDHEAD_TEST_POSTGRES";
    private readonly string _databaseName = "redhead_analytics_test_" + Guid.NewGuid().ToString("N");
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!) { Pooling = false };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        builder.Database = _databaseName;
        _connectionString = builder.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Users.Add(new ApplicationUser { Id = "user", UserName = "analytics-test", Email = "analytics@example.com" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString == null) return;
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    [PostgresFact]
    public async Task Record_ConcurrentRetry_CommitsOnlyOneSearchAndItsDomains()
    {
        // Arrange
        var gate = new ConcurrentSaveGate();
        await using var first = CreateContext(gate);
        await using var second = CreateContext(gate);
        var requestId = Guid.NewGuid();

        // Act
        await Task.WhenAll(
            new MissingDomainsAnalyticsService(first).RecordAsync("user", AppRoles.Client, requestId, ["missing.com", "another.com"]),
            new MissingDomainsAnalyticsService(second).RecordAsync("user", AppRoles.Client, requestId, ["missing.com", "another.com"]));

        // Assert
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.MultiSearchAnalyticsRequests.CountAsync());
        Assert.Equal(2, await verification.MissingDomainSearches.CountAsync());
        Assert.Equal(0, await first.SaveChangesAsync());
        Assert.Equal(0, await second.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Get_AggregatesAndFiltersOnPostgres_PreservingHistoryAfterCatalogAddition()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new MissingDomainsAnalyticsService(db);
        await service.RecordAsync("user", AppRoles.Client, Guid.NewGuid(), ["missing.com", "added.com"]);
        await service.RecordAsync("user", AppRoles.Lite, Guid.NewGuid(), ["missing.com"]);
        db.Sites.Add(new Site { Domain = "added.com", IsQuarantined = true });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetAsync(new());
        var missing = await service.GetAsync(new() { IsInCatalog = false, Role = AppRoles.Client, Domain = "missing", FromUtc = DateTime.UtcNow.Date, ToUtc = DateTime.UtcNow.Date.AddDays(1) });
        var added = await service.GetAsync(new() { IsInCatalog = true });
        var pastLastPage = await service.GetAsync(new() { IsInCatalog = false, Page = 2, PageSize = 10 });

        // Assert
        Assert.Equal(2, result.UniqueDomains);
        Assert.Equal(3, result.Searches);
        Assert.Equal(1, result.UniqueUsers);
        Assert.Equal("missing.com", result.Items[0].Domain);
        Assert.Equal(2, result.Items[0].Searches);
        Assert.Equal("missing.com", Assert.Single(missing.Items).Domain);
        Assert.Equal(1, missing.Searches);
        Assert.True(Assert.Single(added.Items).IsInCatalog);
        Assert.Equal(1, pastLastPage.Page);
        Assert.Equal("missing.com", Assert.Single(pastLastPage.Items).Domain);
    }

    private ApplicationDbContext CreateContext(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connectionString!);
        if (interceptor != null) builder.AddInterceptors(interceptor);
        return new ApplicationDbContext(builder.Options);
    }

    private sealed class ConcurrentSaveGate : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) >= 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
        }
    }
}
