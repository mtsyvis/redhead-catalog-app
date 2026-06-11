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
    public async Task GetExportActivityAsync_ClientSummariesAggregateSelectedPeriodResults()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            requestedRows: 10,
            exportedRows: 10);
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc),
            requestedRows: 20,
            exportedRows: 5,
            wasTruncated: true);
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc),
            requestedRows: 30,
            exportedRows: 0,
            blockedReason: ExportConstants.DailyUniqueDomainLimitReached);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var summary = Assert.Single(result.ClientSummaries);
        Assert.Equal("client-1", summary.UserId);
        Assert.Equal(1, summary.SuccessfulExports);
        Assert.Equal(1, summary.PartialExports);
        Assert.Equal(1, summary.BlockedExports);
        Assert.Equal(60, summary.RequestedRows);
        Assert.Equal(15, summary.ExportedRows);
        Assert.Equal(new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc), summary.LastExportAtUtc);
    }

    [Fact]
    public async Task GetExportActivityAsync_ClientSummariesUseSelectedPeriodOnly()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 5, 31, 23, 0, 0, DateTimeKind.Utc),
            requestedRows: 999,
            exportedRows: 999);
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc),
            requestedRows: 25,
            exportedRows: 25);
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
            requestedRows: 500,
            exportedRows: 500);
        AddClient(db, "client-2", "client-2@example.com");
        AddExportLog(
            db,
            "client-2",
            new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            requestedRows: 10,
            exportedRows: 10);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.ClientSummaries.Count);
        var first = Assert.Single(result.ClientSummaries, item => item.UserId == "client-1");
        Assert.Equal(1, first.SuccessfulExports);
        Assert.Equal(25, first.RequestedRows);
        Assert.Equal(25, first.ExportedRows);
        Assert.Equal(new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc), first.LastExportAtUtc);
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

    [Fact]
    public async Task GetExportActivityAsync_RecentExportSummaryIncludesSelectedTerm()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            requestedRows: 10,
            exportedRows: 10,
            filtersJson: """
                {
                  "schemaVersion": 1,
                  "filters": [
                    { "field": "locationKey", "kind": "multiSelect", "operator": "anyOf", "value": ["US"] },
                    { "field": "dr", "kind": "numberRange", "operator": "gte", "value": { "min": 40 } },
                    { "field": "traffic", "kind": "numberRange", "operator": "gte", "value": { "min": 1000 } },
                    { "field": "priceUsd", "kind": "numberRange", "operator": "lte", "value": { "max": 300 } },
                    { "field": "termKey", "kind": "term", "operator": "eq", "value": "permanent" }
                  ]
                }
                """);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportActivityAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var item = Assert.Single(result.RecentExports.Items);
        Assert.Contains("Term Permanent", item.FiltersSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetExportLogDetailsAsync_WhenLogExists_ReturnsReadableFilterAndSortDetails()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        AddClient(db, "client-1", "client-1@example.com");
        db.CanonicalLocations.AddRange(
            new CanonicalLocation
            {
                Key = "ID",
                DisplayName = "Indonesia",
                IsActive = true
            },
            new CanonicalLocation
            {
                Key = "US",
                DisplayName = "United States",
                IsActive = true
            });
        AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc),
            requestedRows: 1,
            exportedRows: 1);
        var selected = AddExportLog(
            db,
            "client-1",
            new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            requestedRows: 10,
            exportedRows: 5,
            wasTruncated: true,
            filtersJson: """
                {
                  "schemaVersion": 1,
                  "filters": [
                    { "field": "locationKey", "kind": "multiSelect", "operator": "anyOf", "value": ["ID", "US"] },
                    { "field": "dr", "kind": "numberRange", "operator": "between", "value": { "min": 10, "max": 70 } },
                    { "field": "priceUsd", "kind": "numberRange", "operator": "gte", "value": { "min": 100 } },
                    { "field": "termKey", "kind": "term", "operator": "eq", "value": "permanent" },
                    { "field": "quarantine", "kind": "enum", "operator": "eq", "value": "exclude" },
                    { "field": "priceCasinoAvailability", "kind": "availability", "operator": "in", "value": ["available", "availableWithUnknownPrice"] },
                    { "field": "priceCryptoAvailability", "kind": "availability", "operator": "in", "value": ["notAvailable"] },
                    { "field": "lastPublishedDate", "kind": "monthRange", "operator": "between", "value": { "minMonth": "2026-01", "maxMonth": "2026-03" } }
                  ]
                }
                """,
            sortJson: """
                {
                  "schemaVersion": 1,
                  "sorts": [
                    { "field": "dr", "direction": "desc", "priority": 1 }
                  ]
                }
                """,
            searchJson: """
                {
                  "schemaVersion": 1,
                  "mode": "multiSearch",
                  "inputCount": 4,
                  "uniqueInputCount": 3,
                  "foundCount": 2,
                  "notFoundCount": 1
                }
                """);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportLogDetailsAsync(selected.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(selected.Id, result.Id);
        Assert.Equal("Partial", result.Status);
        Assert.Equal("Rows per export limit", result.OutcomeReason);
        Assert.Equal(ExportConstants.ExportModeSites, result.ExportMode);
        Assert.Equal("Indonesia, United States", GetFilterValue(result, "Locations", "Locations"));
        Assert.Equal("10-70", GetFilterValue(result, "Quality and price", "DR"));
        Assert.Equal("No filter", GetFilterValue(result, "Quality and price", "Traffic"));
        Assert.Equal("From $100", GetFilterValue(result, "Quality and price", "Price USD"));
        Assert.Equal("Permanent", GetFilterValue(result, "Quality and price", "Term"));
        Assert.Equal("Available", GetFilterValue(result, "Status", "Status"));
        Assert.Equal("2026-01-2026-03", GetFilterValue(result, "Status", "Last published"));
        Assert.Equal("Has price, YES", GetFilterValue(result, "Optional services", "Casino"));
        Assert.Equal("NO", GetFilterValue(result, "Optional services", "Crypto"));
        Assert.Equal("Yes", GetFilterValue(result, "Multi-search", "Enabled"));
        Assert.Equal("4", GetFilterValue(result, "Multi-search", "Input domains count"));
        Assert.Equal("2", GetFilterValue(result, "Multi-search", "Found count"));
        Assert.Equal("DR descending", result.Sort.Summary);
        var sortItem = Assert.Single(result.Sort.Items);
        Assert.Equal("DR", sortItem.Label);
        Assert.Equal("descending", sortItem.Value);
        Assert.NotNull(result.TechnicalDetails);
        Assert.Contains("\"filters\"", result.TechnicalDetails.FiltersSnapshotJson);
    }

    [Fact]
    public async Task GetExportLogDetailsAsync_WhenLogDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedClientRoleSettings(db);
        var sut = CreateService(db);

        // Act
        var result = await sut.GetExportLogDetailsAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
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

    private static string GetFilterValue(
        ExportLogDetailsDto details,
        string sectionTitle,
        string label)
    {
        var section = Assert.Single(details.AppliedFilters, section => section.Title == sectionTitle);
        return Assert.Single(section.Rows, row => row.Label == label).Value;
    }
}
