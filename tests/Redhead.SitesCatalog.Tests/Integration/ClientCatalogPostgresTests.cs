using Redhead.SitesCatalog.Domain.ClientCatalog;
using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Moq;

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
        var sut = new ClientSelectionLimitController(users, new ClientCatalogActivityService(db, new FixedClock(now)),
            new ClientCatalogBurstLimiter(new FixedClock(now), 1000), new FixedClock(now));

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

    [PostgresFact]
    public Task CatalogUsage_DoesNotConflictWithIdentitySelectionUpdate()
        => ClientCatalogSelectionConcurrencyTests.VerifySelectionUpdateAsync(options => options.UseNpgsql(_connectionString), 50);

    private ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_connectionString).Options);

    [PostgresFact]
    public async Task AlertsPersistAcrossContexts_WhileBurstBudgetResetsOnRestart()
    {
        // Arrange
        await using var db = CreateContext();
        var now = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FixedClock(now);
        var role = new IdentityRole(AppRoles.Client);
        var user = new ApplicationUser { UserName = "alert-client@example.com", Email = "alert-client@example.com" };
        db.Roles.Add(role);
        db.Users.Add(user);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
        db.ClientCatalogRequests.Add(Request(user.Id, now.AddMinutes(-1), 200, "a.example", "b.example", "a.example"));
        var export = new ExportLog { Id = Guid.NewGuid(), UserId = user.Id, TimestampUtc = now };
        export.ExportedDomainAccesses = new[] { "b.example", "c.example" }.Select(domain => new ExportedDomainAccess
        {
            Id = Guid.NewGuid(), UserId = user.Id, Domain = domain, ExportedAtUtc = now
        }).ToList();
        db.ExportLogs.Add(export);
        await db.SaveChangesAsync();
        var options = Options.Create(new ClientCatalogOptions { AlertUniqueSitesPerHour = 3 });
        var sender = Moq.Mock.Of<IClientCatalogAlertEmailSender>();

        // Act
        await new ClientCatalogAlertService(db, options, sender, clock).ProcessAsync(CancellationToken.None);
        var catalog = new ClientCatalogService(db, new ClientCatalogBurstLimiter(clock, 1000));
        await catalog.EnsureBurstLimitAsync(user.Id, Enumerable.Range(0, 1000).Select(index => $"site{index}.example").ToArray(), 100);
        await using var restartedDb = CreateContext();
        await new ClientCatalogAlertService(restartedDb, options, sender, clock).ProcessAsync(CancellationToken.None);
        var restartedCatalog = new ClientCatalogService(restartedDb, new ClientCatalogBurstLimiter(clock, 1000));
        var exception = await Record.ExceptionAsync(() =>
            restartedCatalog.EnsureBurstLimitAsync(user.Id, ["new.example"], 100));

        // Assert
        Assert.Null(exception);
        var alert = Assert.Single(await restartedDb.ClientCatalogAlerts.Where(row => row.UserId == user.Id).ToListAsync());
        Assert.Equal(3, alert.UniqueSites);
        Assert.Null(alert.EmailSentAtUtc);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task AutoBan_UsesPostgresRollingUnion_AndExemptsTrustedSelections()
    {
        // Arrange
        await using var db = CreateContext();
        var now = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
        var role = new IdentityRole(AppRoles.Client);
        var protectedUser = new ApplicationUser
        {
            UserName = "protected-auto-ban@example.com",
            Email = "protected-auto-ban@example.com"
        };
        var trustedUser = new ApplicationUser
        {
            UserName = "trusted-auto-ban@example.com",
            Email = "trusted-auto-ban@example.com",
            ClientSelectionLimitOverride = 101
        };
        db.AddRange(role, protectedUser, trustedUser);
        db.UserRoles.AddRange(
            new IdentityUserRole<string> { UserId = protectedUser.Id, RoleId = role.Id },
            new IdentityUserRole<string> { UserId = trustedUser.Id, RoleId = role.Id });
        db.ClientCatalogRequests.AddRange(
            Request(protectedUser.Id, now.AddMinutes(-1), 200, "a.example", "b.example"),
            Request(trustedUser.Id, now.AddMinutes(-1), 200, "a.example", "b.example", "c.example"));
        db.ExportedDomainAccesses.Add(new ExportedDomainAccess
        {
            Id = Guid.NewGuid(),
            UserId = protectedUser.Id,
            Domain = "c.example",
            ExportedAtUtc = now
        });
        var settings = await db.ClientCatalogProtectionSettings.SingleAsync();
        settings.AutoBanEnabled = true;
        settings.AutoBanUniqueSitesPer24Hours = 3;
        await db.SaveChangesAsync();
        var sender = new Moq.Mock<IClientCatalogAlertEmailSender>();
        sender.Setup(item => item.SendAutoBanAsync(
                It.IsAny<ClientCatalogAutoBan>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ClientCatalogAutoBanService(
            db,
            Options.Create(new ClientCatalogOptions { AlertEmails = "admin@example.com" }),
            sender.Object,
            new FixedClock(now));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.False(protectedUser.IsActive);
        Assert.Equal(UserDisabledReasons.ClientCatalogAutoBan, protectedUser.DisabledReason);
        Assert.True(trustedUser.IsActive);
        var autoBan = Assert.Single(await db.ClientCatalogAutoBans.ToListAsync());
        Assert.Equal(protectedUser.Id, autoBan.UserId);
        Assert.Equal(3, autoBan.UniqueSites);
        Assert.NotNull(autoBan.EmailSentAtUtc);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task ReviewCooldown_SurvivesRestart_AndUsesMostRecentReview()
    {
        // Arrange
        var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        var user = new ApplicationUser { UserName = "review-client@example.com" };
        var role = new IdentityRole(AppRoles.Client);
        await using (var db = CreateContext())
        {
            db.Users.Add(user);
            db.Roles.Add(role);
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
            db.ClientCatalogAlerts.AddRange(
                new ClientCatalogAlert { UserId = user.Id, DetectedAtUtc = now.AddHours(-3), ReviewedAtUtc = now.AddHours(-2), UniqueSites = 3, Threshold = 3 },
                new ClientCatalogAlert { UserId = user.Id, DetectedAtUtc = now.AddMinutes(-20), ReviewedAtUtc = now, UniqueSites = 3, Threshold = 3 });
            db.ClientCatalogRequests.Add(Request(user.Id, now.AddMinutes(30), 200, "a.example", "b.example", "c.example"));
            await db.SaveChangesAsync();
        }
        var options = Options.Create(new ClientCatalogOptions { AlertUniqueSitesPerHour = 3 });
        var sender = Moq.Mock.Of<IClientCatalogAlertEmailSender>();

        // Act
        int beforeExpiry;
        await using (var before = CreateContext())
        {
            await new ClientCatalogAlertService(before, options, sender, new FixedClock(now.AddSeconds(3599))).ProcessAsync(CancellationToken.None);
            beforeExpiry = await before.ClientCatalogAlerts.CountAsync();
        }
        await using var restarted = CreateContext();
        var service = new ClientCatalogAlertService(restarted, options, sender, new FixedClock(now.AddHours(1)));
        await service.ProcessAsync(CancellationToken.None);
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, beforeExpiry);
        Assert.Equal(3, await restarted.ClientCatalogAlerts.CountAsync());
        var open = await restarted.ClientCatalogAlerts.SingleAsync(alert => alert.ReviewedAtUtc == null);
        Assert.Equal(now.AddHours(1), open.DetectedAtUtc);
        Assert.Equal(3, open.UniqueSites);
        Assert.Equal(2, await restarted.ClientCatalogAlerts.CountAsync(alert => alert.ReviewedAtUtc != null));
        Assert.False(restarted.Database.HasPendingModelChanges());
    }

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
