using Redhead.SitesCatalog.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Integration;

// Opt-in: migrate and remove only a newly generated test database.
public sealed class ClientCatalogPostgresTests : IAsyncLifetime
{
    private const string ConnectionVariable = "REDHEAD_TEST_POSTGRES";
    private readonly string _databaseName = "redhead_client_test_" + Guid.NewGuid().ToString("N");
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!) { Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
        await command.ExecuteNonQueryAsync();
        builder.Database = _databaseName;
        _connectionString = builder.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        if (builder.Database != _databaseName || !_databaseName.StartsWith("redhead_client_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete a non-test database.");
        }

        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    [PostgresFact]
    public async Task SelectionLimit_PersistsAndResets_ActivityDeduplicatesAcrossSearchAndExportWindows()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(AppRoles.Client))).Succeeded);
        var user = new ApplicationUser { UserName = "client@example.com", Email = "client@example.com" };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AppRoles.Client)).Succeeded);
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        db.ClientCatalogRequests.AddRange(
            Request(user.Id, now.AddMinutes(-5), 200, "a.example", "b.example", "a.example"),
            Request(user.Id, now.AddMinutes(-4), 429),
            Request(user.Id, now.AddHours(-2), 200, "c.example"),
            Request(user.Id, now.AddDays(-2), 200, "d.example"),
            Request(user.Id, now.AddDays(-8), 200, "expired.example"));
        var export = new ExportLog { Id = Guid.NewGuid(), UserId = user.Id, TimestampUtc = now.AddMinutes(-3) };
        export.ExportedDomainAccesses = new[] { "b.example", "export.example" }
            .Select(domain => new ExportedDomainAccess { Id = Guid.NewGuid(), UserId = user.Id, Domain = domain, ExportedAtUtc = export.TimestampUtc }).ToList();
        db.ExportLogs.Add(export);
        await db.SaveChangesAsync();
        var sut = new ClientSelectionLimitController(users, new ClientCatalogActivityService(db, new FixedClock(now)));

        // Act
        var update = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(300));
        await using var verification = CreateContext();
        var storedLimit = await verification.Users.Where(x => x.Id == user.Id).Select(x => x.ClientSelectionLimitOverride).SingleAsync();
        var response = await sut.Get(user.Id, CancellationToken.None);
        var reset = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(null));
        var resetLimit = await verification.Users.Where(x => x.Id == user.Id).Select(x => x.ClientSelectionLimitOverride).SingleAsync();

        // Assert
        Assert.IsType<NoContentResult>(update);
        Assert.Equal(300, storedLimit);
        var data = Assert.IsType<ClientSelectionLimitController.LimitResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(300, data.EffectiveRows);
        Assert.Collection(data.Activity,
            hour => Assert.Equal((2, 1, 3), (hour.Requests, hour.RateLimitedRequests, hour.UniqueSites)),
            day => Assert.Equal((3, 1, 4), (day.Requests, day.RateLimitedRequests, day.UniqueSites)),
            week => Assert.Equal((4, 1, 5), (week.Requests, week.RateLimitedRequests, week.UniqueSites)));
        Assert.IsType<NoContentResult>(reset);
        Assert.Null(resetLimit);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connectionString).Options);

    private static ClientCatalogRequest Request(string userId, DateTime timestamp, int status, params string[] domains)
        => new() { UserId = userId, TimestampUtc = timestamp, Endpoint = "/api/sites/search", StatusCode = status, Domains = domains };

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Set {ConnectionVariable} to a disposable PostgreSQL server connection string.";
            }
        }
    }
}
