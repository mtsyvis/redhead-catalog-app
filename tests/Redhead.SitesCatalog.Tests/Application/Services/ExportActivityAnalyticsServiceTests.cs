using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class ExportActivityAnalyticsServiceTests
{
    private const string ClientRoleId = "role-client";

    [Fact]
    public async Task GetExportActivityAsync_SummaryCountsSuccessfulPartialBlockedRowsAndUniqueDomains()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        AddClient(db, "client-2", "client-2@example.com");
        var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var successful = AddExportLog(db, "client-1", timestamp, requestedRows: 10, exportedRows: 10);
        var partial = AddExportLog(db, "client-1", timestamp, requestedRows: 20, exportedRows: 5, wasTruncated: true);
        AddExportLog(
            db,
            "client-2",
            timestamp,
            requestedRows: 30,
            exportedRows: 0,
            blockedReason: ExportConstants.DailyUniqueDomainLimitReached);
        AddExportLog(db, "admin-1", timestamp, requestedRows: 100, exportedRows: 100, role: AppRoles.Admin);
        AddExportLog(db, "client-1", timestamp.AddDays(2), requestedRows: 100, exportedRows: 100);
        AddDomainAccess(db, successful, "alpha.com");
        AddDomainAccess(db, successful, "beta.com");
        AddDomainAccess(db, partial, "alpha.com");
        AddDomainAccess(db, partial, "gamma.com");
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Summary.CompletedExports);
        Assert.Equal(1, result.Summary.PartialExports);
        Assert.Equal(1, result.Summary.BlockedExports);
        Assert.Equal(3, result.Summary.UniqueExportedDomains);
        Assert.Equal(60, result.Summary.RequestedRows);
        Assert.Equal(15, result.Summary.ExportedRows);
    }

    [Fact]
    public async Task GetExportActivityAsync_ExportsOverTimeGroupsByDay()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        var firstDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var secondDay = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
        var firstLog = AddExportLog(db, "client-1", firstDay, requestedRows: 10, exportedRows: 10);
        AddExportLog(db, "client-1", firstDay.AddHours(2), requestedRows: 10, exportedRows: 5, wasTruncated: true);
        AddExportLog(
            db,
            "client-1",
            secondDay,
            requestedRows: 10,
            exportedRows: 0,
            blockedReason: ExportConstants.WeeklyExportOperationLimitReached);
        AddDomainAccess(db, firstLog, "alpha.com");
        AddDomainAccess(db, firstLog, "beta.com");
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(
            CreateQuery(toUtc: new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        // Assert
        var rows = result.ExportsOverTime.ToDictionary(row => row.Date);
        Assert.Equal(1, rows["2026-06-01"].SuccessfulExports);
        Assert.Equal(1, rows["2026-06-01"].PartialExports);
        Assert.Equal(0, rows["2026-06-01"].BlockedExports);
        Assert.Equal(2, rows["2026-06-01"].ExportedDomains);
        Assert.Equal(0, rows["2026-06-02"].SuccessfulExports);
        Assert.Equal(0, rows["2026-06-02"].PartialExports);
        Assert.Equal(1, rows["2026-06-02"].BlockedExports);
    }

    [Fact]
    public async Task GetExportActivityAsync_ClientUsageCalculatesRollingDomainWindows()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        var nowUtc = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var log = AddExportLog(db, "client-1", nowUtc.AddHours(-1), requestedRows: 1, exportedRows: 1);
        AddDomainAccess(db, log, "alpha.com", nowUtc.AddHours(-1));
        AddDomainAccess(db, log, "alpha.com", nowUtc.AddHours(-2));
        AddDomainAccess(db, log, "beta.com", nowUtc.AddHours(-25));
        AddDomainAccess(db, log, "gamma.com", nowUtc.AddDays(-6));
        AddDomainAccess(db, log, "delta.com", nowUtc.AddDays(-8));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(nowUtc: nowUtc), CancellationToken.None);

        // Assert
        var usage = Assert.Single(result.ClientUsage);
        Assert.Equal(1, usage.DailyUniqueDomainsUsed);
        Assert.Equal(3, usage.WeeklyUniqueDomainsUsed);
    }

    [Fact]
    public async Task GetExportActivityAsync_ClientUsageCalculatesRollingOperationWindows()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        var nowUtc = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        AddExportLog(db, "client-1", nowUtc.AddHours(-1), requestedRows: 1, exportedRows: 1);
        AddExportLog(db, "client-1", nowUtc.AddHours(-2), requestedRows: 1, exportedRows: 1, wasTruncated: true);
        AddExportLog(
            db,
            "client-1",
            nowUtc.AddHours(-3),
            requestedRows: 1,
            exportedRows: 0,
            blockedReason: ExportConstants.DailyExportOperationLimitReached);
        AddExportLog(db, "client-1", nowUtc.AddHours(-25), requestedRows: 1, exportedRows: 1);
        AddExportLog(db, "client-1", nowUtc.AddDays(-6), requestedRows: 1, exportedRows: 1);
        AddExportLog(db, "client-1", nowUtc.AddDays(-8), requestedRows: 1, exportedRows: 1);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(nowUtc: nowUtc), CancellationToken.None);

        // Assert
        var usage = Assert.Single(result.ClientUsage);
        Assert.Equal(2, usage.DailyExportOperationsUsed);
        Assert.Equal(4, usage.WeeklyExportOperationsUsed);
    }

    [Fact]
    public async Task GetExportActivityAsync_ClientUsageStatusIsNearLimitAtEightyPercent()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db, dailyDomains: 10);
        AddClient(db, "client-1", "client-1@example.com");
        var nowUtc = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var log = AddExportLog(db, "client-1", nowUtc.AddHours(-1), requestedRows: 8, exportedRows: 8);
        for (var index = 1; index <= 8; index++)
        {
            AddDomainAccess(db, log, $"domain-{index}.com", nowUtc.AddHours(-1));
        }

        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(nowUtc: nowUtc), CancellationToken.None);

        // Assert
        var usage = Assert.Single(result.ClientUsage);
        Assert.Equal(ExportActivityClientUsageStatuses.NearLimit, usage.Status);
    }

    [Fact]
    public async Task GetExportActivityAsync_ClientUsageStatusIsLimitReachedAtFullUsageOrBlockedExport()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db, dailyDomains: 10);
        AddClient(db, "client-1", "client-1@example.com");
        AddClient(db, "client-2", "client-2@example.com");
        var nowUtc = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var log = AddExportLog(db, "client-1", nowUtc.AddHours(-1), requestedRows: 10, exportedRows: 10);
        for (var index = 1; index <= 10; index++)
        {
            AddDomainAccess(db, log, $"domain-{index}.com", nowUtc.AddHours(-1));
        }

        AddExportLog(
            db,
            "client-2",
            new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            requestedRows: 1,
            exportedRows: 0,
            blockedReason: ExportConstants.WeeklyUniqueDomainLimitReached);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(nowUtc: nowUtc), CancellationToken.None);

        // Assert
        var statuses = result.ClientUsage.ToDictionary(row => row.UserId, row => row.Status);
        Assert.Equal(ExportActivityClientUsageStatuses.LimitReached, statuses["client-1"]);
        Assert.Equal(ExportActivityClientUsageStatuses.LimitReached, statuses["client-2"]);
    }

    [Fact]
    public async Task GetExportActivityAsync_RecentExportsArePaginated()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        AddExportLog(db, "client-1", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc), requestedRows: 1, exportedRows: 1);
        AddExportLog(db, "client-1", new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), requestedRows: 2, exportedRows: 2);
        AddExportLog(db, "client-1", new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc), requestedRows: 3, exportedRows: 3);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(
            CreateQuery(recentExportsPage: 2, recentExportsPageSize: 2),
            CancellationToken.None);

        // Assert
        Assert.Equal(3, result.RecentExports.TotalCount);
        var item = Assert.Single(result.RecentExports.Items);
        Assert.Equal(1, item.RequestedRows);
    }

    [Fact]
    public async Task GetExportActivityAsync_MissingOldOrInvalidSnapshotDataDoesNotCrashRecentSummaries()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            requestedRows: 1,
            exportedRows: 1,
            filtersJson: "{not-json",
            sortJson: "{not-json",
            searchJson: "{not-json");
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var item = Assert.Single(result.RecentExports.Items);
        Assert.Equal("Unavailable", item.FiltersSummary);
        Assert.Equal("Unavailable", item.SortSummary);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ExportActivityAnalyticsService CreateService(ApplicationDbContext db)
        => new(db);

    private static ExportActivityAnalyticsQuery CreateQuery(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        DateTime? nowUtc = null,
        int recentExportsPage = 1,
        int recentExportsPageSize = 25)
        => new()
        {
            FromUtc = fromUtc ?? new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = toUtc ?? new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
            NowUtc = nowUtc ?? new DateTime(2026, 6, 10, 12, 0, 0, 0, DateTimeKind.Utc),
            RecentExportsPage = recentExportsPage,
            RecentExportsPageSize = recentExportsPageSize
        };

    private static void SeedClientRoleSettings(
        ApplicationDbContext db,
        int dailyDomains = 1000,
        int weeklyDomains = 3000,
        int dailyOperations = 20,
        int weeklyOperations = 60)
    {
        db.Roles.Add(new IdentityRole
        {
            Id = ClientRoleId,
            Name = AppRoles.Client,
            NormalizedName = AppRoles.Client.ToUpperInvariant()
        });
        db.RoleSettings.Add(new RoleSettings
        {
            RoleName = AppRoles.Client,
            ExportLimitMode = ExportLimitMode.Limited,
            ExportLimitRows = 5000,
            DailyUniqueExportedDomainsLimit = dailyDomains,
            WeeklyUniqueExportedDomainsLimit = weeklyDomains,
            DailyExportOperationsLimit = dailyOperations,
            WeeklyExportOperationsLimit = weeklyOperations
        });
    }

    private static ApplicationUser AddClient(
        ApplicationDbContext db,
        string userId,
        string email)
    {
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Client",
            LastName = userId
        };
        db.Users.Add(user);
        db.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = userId,
            RoleId = ClientRoleId
        });
        return user;
    }

    private static ExportLog AddExportLog(
        ApplicationDbContext db,
        string userId,
        DateTime timestampUtc,
        int requestedRows,
        int exportedRows,
        bool wasTruncated = false,
        string? blockedReason = null,
        string destination = ExportConstants.DestinationDownload,
        string role = AppRoles.Client,
        string? filtersJson = null,
        string? sortJson = null,
        string? searchJson = null)
    {
        var log = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = $"{userId}@example.com",
            Role = role,
            TimestampUtc = timestampUtc,
            RowsReturned = exportedRows,
            RequestedRowsCount = requestedRows,
            ExportedRowsCount = exportedRows,
            WasTruncated = wasTruncated,
            ExportLimitRows = wasTruncated ? exportedRows : null,
            Destination = destination,
            ExportMode = ExportConstants.ExportModeSites,
            BlockedReason = blockedReason
        };
        db.ExportLogs.Add(log);

        if (filtersJson != null || sortJson != null || searchJson != null)
        {
            db.ExportAnalyticsSnapshots.Add(new ExportAnalyticsSnapshot
            {
                Id = Guid.NewGuid(),
                ExportLogId = log.Id,
                ExportLog = log,
                SnapshotVersion = 1,
                FiltersSnapshotJson = filtersJson ?? FiltersJson(),
                SortSnapshotJson = sortJson ?? """{"schemaVersion":1,"sorts":[]}""",
                SearchSnapshotJson = searchJson,
                CreatedAtUtc = timestampUtc
            });
        }

        return log;
    }

    private static void AddDomainAccess(
        ApplicationDbContext db,
        ExportLog exportLog,
        string domain,
        DateTime? exportedAtUtc = null)
    {
        db.ExportedDomainAccesses.Add(new ExportedDomainAccess
        {
            Id = Guid.NewGuid(),
            ExportLogId = exportLog.Id,
            ExportLog = exportLog,
            UserId = exportLog.UserId,
            Domain = domain,
            ExportedAtUtc = exportedAtUtc ?? exportLog.TimestampUtc
        });
    }

    private static string FiltersJson(params object[] filters)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["filters"] = filters
        });
}
