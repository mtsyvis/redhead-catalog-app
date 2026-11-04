using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Redhead.SitesCatalog.Application.LinkDetective;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Concurrency;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Coordination;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Integration;

// Opt-in, following the existing integration suites: only synthetic data in an isolated database.
public sealed class LinkDetectiveSyncPostgresTests : IAsyncLifetime
{
    private const string ConnectionVariable = "REDHEAD_TEST_POSTGRES";
    private readonly string _database = "redhead_ld_test_" + Guid.NewGuid().ToString("N");
    private string? _connection;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        builder.Database = _database;
        _connection = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [PostgresFact]
    public async Task Worker_UsesRealSelectionSqlAndPersistsCacheWithoutChangingCatalog()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        context.Sites.AddRange(new Site { Domain = "pending.example", CreatedAtUtc = now, UpdatedAtUtc = now, IsQuarantined = true },
            new Site { Domain = "cached.example", CreatedAtUtc = now, UpdatedAtUtc = now });
        context.LinkDetectivePriceCaches.Add(new() { SiteDomain = "cached.example", CheckedAtUtc = now });
        await context.SaveChangesAsync();
        var api = new Mock<ILinkDetectiveApiClient>(MockBehavior.Strict);
        var quota = new LinkDetectiveQuota(50000, 0, 50000, "day", now.Date.AddDays(1));
        api.Setup(value => value.GetQuotaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(quota);
        api.Setup(value => value.GetBatchPricesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkDetectiveBatchPricesResponse(new Dictionary<string, LinkDetectivePrices> { ["pending.example"] = new(10, 20, 15) }, false, quota with { Used = 1, Remaining = 49999 }));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);
        var service = new LinkDetectiveSyncService(context, api.Object, new PostgresLinkDetectiveSyncLock(context), coordinator,
            Options.Create(new LinkDetectiveOptions { ApiToken = "fake-token" }), Options.Create(new LinkDetectiveSyncOptions { Enabled = true }), TimeProvider.System);

        // Act
        await service.TickAsync(CancellationToken.None);
        var monitoring = new LinkDetectiveSyncMonitoring(context, Options.Create(new LinkDetectiveOptions()), Options.Create(new LinkDetectiveSyncOptions()), TimeProvider.System);
        var status = await monitoring.GetStatusAsync(CancellationToken.None);
        var run = await context.LinkDetectiveSyncRuns.AsNoTracking().SingleAsync();
        var details = await monitoring.GetRunAsync(run.Id, 1, 25, "PENDING", LinkDetectiveSyncItemStatus.PricesFound, CancellationToken.None);

        // Assert
        Assert.Equal(1, run.SelectedCount);
        Assert.Equal(LinkDetectiveSyncRunStatus.Completed, run.Status);
        Assert.Equal(2, status.FreshCount);
        Assert.Equal(10, (await context.LinkDetectivePriceCaches.SingleAsync(row => row.SiteDomain == "pending.example")).MinimumUsd);
        Assert.Equal("pending.example", Assert.Single(details!.Results.Items).Domain);
        Assert.All(await context.Sites.ToListAsync(), site => Assert.Equal(now, site.UpdatedAtUtc));
    }

    [PostgresFact]
    public async Task Worker_SecondDatabaseSessionCannotProcessWhileLockIsHeld()
    {
        // Arrange
        await using var first = CreateContext();
        await first.Database.MigrateAsync();
        await using var held = await new PostgresLinkDetectiveSyncLock(first).TryAcquireAsync(CancellationToken.None);
        await using var second = CreateContext();
        var api = new Mock<ILinkDetectiveApiClient>(MockBehavior.Strict);
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);
        var worker = new LinkDetectiveSyncService(second, api.Object, new PostgresLinkDetectiveSyncLock(second), coordinator,
            Options.Create(new LinkDetectiveOptions { ApiToken = "fake-token" }), Options.Create(new LinkDetectiveSyncOptions { Enabled = true }), TimeProvider.System);

        // Act
        await worker.TickAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(held);
        Assert.Empty(api.Invocations);
        Assert.Empty(await second.LinkDetectiveSyncRuns.ToListAsync());
    }

    [PostgresFact]
    public async Task Migration_RollbackPreservesExistingSharedCache()
    {
        // Arrange
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.Sites.Add(new Site { Domain = "retained.example" });
        context.LinkDetectivePriceCaches.Add(new() { SiteDomain = "retained.example", MinimumUsd = 10, AverageUsd = 20, MedianUsd = 15, CheckedAtUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Act
        await context.GetService<IMigrator>().MigrateAsync("20261026134302_RemoveWhlWebmasterOffers");

        // Assert
        context.ChangeTracker.Clear();
        Assert.Equal(10, (await context.LinkDetectivePriceCaches.SingleAsync()).MinimumUsd);
        Assert.DoesNotContain(await context.Database.GetAppliedMigrationsAsync(), migration => migration.EndsWith("AddLinkDetectiveBackgroundSync", StringComparison.Ordinal));
    }

    private ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connection).Options);
    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
            }
        }
    }
}
