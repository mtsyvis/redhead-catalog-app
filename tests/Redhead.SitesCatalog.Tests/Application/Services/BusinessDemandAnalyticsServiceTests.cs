using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class BusinessDemandAnalyticsServiceTests
{
    [Fact]
    public async Task GetBusinessDemandAsync_SummaryCountsClientExportLogs()
    {
        // Arrange
        await using var db = CreateDbContext();
        var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        AddExportLog(db, "client-1", timestamp, requestedRows: 10, exportedRows: 10);
        AddExportLog(db, "client-1", timestamp, requestedRows: 20, exportedRows: 5, wasTruncated: true);
        AddExportLog(db, "client-2", timestamp, requestedRows: 30, exportedRows: 0, blockedReason: "LimitReached");
        AddExportLog(db, "admin-1", timestamp, requestedRows: 100, exportedRows: 100, role: AppRoles.Admin);
        AddExportLog(db, "client-3", timestamp.AddDays(2), requestedRows: 100, exportedRows: 100);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetBusinessDemandAsync(CreateQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Summary.ExportRequests);
        Assert.Equal(2, result.Summary.ClientsWithExportActivity);
        Assert.Equal(60, result.Summary.RequestedRows);
        Assert.Equal(15, result.Summary.ExportedDomains);
    }

    [Fact]
    public async Task GetBusinessDemandAsync_TopLocationsCountSelectedLocationValuesOncePerExport()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedLocations(db);
        var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        AddExportLog(
            db,
            "client-1",
            timestamp,
            requestedRows: 10,
            exportedRows: 10,
            filtersJson: FiltersJson(MultiSelectFilter("locationKey", "US", "GB")));
        AddExportLog(
            db,
            "client-2",
            timestamp,
            requestedRows: 10,
            exportedRows: 10,
            filtersJson: FiltersJson(
                MultiSelectFilter("locationGroup", "western"),
                MultiSelectFilter("excludedLocationKey", "GB"),
                BooleanFilter("locationUnknown", true),
                BooleanFilter("locationOther", true)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetBusinessDemandAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var counts = result.TopLocations.ToDictionary(item => item.Name, item => item.ExportRequests);
        Assert.Equal(2, counts["United States"]);
        Assert.Equal(1, counts["United Kingdom"]);
        Assert.Equal(1, counts["Unknown"]);
        Assert.Equal(1, counts["Other"]);
    }

    [Fact]
    public async Task GetBusinessDemandAsync_ServiceDemandSeparatesAvailableFromExplicitNo()
    {
        // Arrange
        await using var db = CreateDbContext();
        var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        AddExportLog(
            db,
            "client-1",
            timestamp,
            requestedRows: 10,
            exportedRows: 10,
            filtersJson: FiltersJson(
                AvailabilityFilter("priceCasinoAvailability", "available", "availableWithUnknownPrice", "notAvailable"),
                AvailabilityFilter("priceCryptoAvailability", "notAvailable"),
                AvailabilityFilter("priceDatingAvailability", "unknown")));
        AddExportLog(
            db,
            "client-2",
            timestamp,
            requestedRows: 10,
            exportedRows: 10,
            filtersJson: FiltersJson(AvailabilityFilter("priceLinkInsertAvailability", "availableWithUnknownPrice")));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetBusinessDemandAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var casino = result.ServiceDemand.Single(item => item.Service == "Casino");
        Assert.Equal(1, casino.WantedOrAvailableRequests);
        Assert.Equal(1, casino.ExplicitlyNoRequests);

        var crypto = result.ServiceDemand.Single(item => item.Service == "Crypto");
        Assert.Equal(0, crypto.WantedOrAvailableRequests);
        Assert.Equal(1, crypto.ExplicitlyNoRequests);

        var linkInsert = result.ServiceDemand.Single(item => item.Service == "Link insert");
        Assert.Equal(1, linkInsert.WantedOrAvailableRequests);
        Assert.Equal(0, linkInsert.ExplicitlyNoRequests);

        Assert.DoesNotContain(result.ServiceDemand, item => item.Service == "Dating");
    }

    [Fact]
    public async Task GetBusinessDemandAsync_QualityRangesFormatMinMaxRanges()
    {
        // Arrange
        await using var db = CreateDbContext();
        var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        AddExportLog(
            db,
            "client-1",
            timestamp,
            requestedRows: 10,
            exportedRows: 10,
            filtersJson: FiltersJson(
                RangeFilter("dr", min: 40m, max: 100m),
                RangeFilter("traffic", min: 10000m, max: null),
                RangeFilter("priceUsd", min: null, max: 300m)));
        AddExportLog(
            db,
            "client-2",
            timestamp,
            requestedRows: 10,
            exportedRows: 10,
            filtersJson: FiltersJson(RangeFilter("priceUsd", min: 100m, max: 500m)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetBusinessDemandAsync(CreateQuery(), CancellationToken.None);

        // Assert
        Assert.Contains(result.QualityDemand.DrRanges, item => item.Name == "DR 40-100" && item.ExportRequests == 1);
        Assert.Contains(result.QualityDemand.TrafficRanges, item => item.Name == "Traffic 10,000+" && item.ExportRequests == 1);
        Assert.Contains(result.QualityDemand.PriceRanges, item => item.Name == "Price up to $300" && item.ExportRequests == 1);
        Assert.Contains(result.QualityDemand.PriceRanges, item => item.Name == "Price $100-$500" && item.ExportRequests == 1);
    }

    [Fact]
    public async Task GetBusinessDemandAsync_FilterStrictnessClassifiesFiltersAndBroadThreshold()
    {
        // Arrange
        await using var db = CreateDbContext();
        var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        AddExportLog(db, "client-1", timestamp, requestedRows: 8000, exportedRows: 100, filtersJson: FiltersJson());
        AddExportLog(
            db,
            "client-2",
            timestamp,
            requestedRows: 6000,
            exportedRows: 100,
            filtersJson: FiltersJson(EnumFilter("quarantine", "exclude")));
        AddExportLog(
            db,
            "client-3",
            timestamp,
            requestedRows: 5001,
            exportedRows: 100,
            filtersJson: FiltersJson(MultiSelectFilter("niche", "casino")));
        AddExportLog(
            db,
            "client-4",
            timestamp,
            requestedRows: 5000,
            exportedRows: 100,
            filtersJson: FiltersJson(MultiSelectFilter("categories", "Finance")));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetBusinessDemandAsync(CreateQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.FilterStrictness.NoFilters);
        Assert.Equal(1, result.FilterStrictness.BroadExports);
        Assert.Equal(1, result.FilterStrictness.FilteredExports);
        Assert.Equal(5000, result.FilterStrictness.BroadExportThreshold);
    }

    [Fact]
    public async Task GetBusinessDemandAsync_MissingOldOrInvalidSnapshotDataDoesNotCrash()
    {
        // Arrange
        await using var db = CreateDbContext();
        var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        AddExportLog(db, "client-1", timestamp, requestedRows: 10, exportedRows: 10, filtersJson: null);
        AddExportLog(db, "client-2", timestamp, requestedRows: 20, exportedRows: 20, filtersJson: "{not-json");
        AddExportLog(db, "client-3", timestamp, requestedRows: 30, exportedRows: 30, filtersJson: """{"schemaVersion":0}""");
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var result = await sut.GetBusinessDemandAsync(CreateQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Summary.ExportRequests);
        Assert.Equal(60, result.Summary.RequestedRows);
        Assert.Empty(result.TopLocations);
        Assert.Empty(result.ServiceDemand);
        Assert.Equal(0, result.FilterStrictness.NoFilters);
        Assert.Equal(0, result.FilterStrictness.BroadExports);
        Assert.Equal(0, result.FilterStrictness.FilteredExports);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static BusinessDemandAnalyticsService CreateService(ApplicationDbContext db)
        => new(db);

    private static BusinessDemandAnalyticsQuery CreateQuery()
        => new()
        {
            FromUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
        };

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
        string? filtersJson = null)
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
            Destination = destination,
            ExportMode = ExportConstants.ExportModeSites,
            BlockedReason = blockedReason
        };
        db.ExportLogs.Add(log);

        if (filtersJson != null)
        {
            db.ExportAnalyticsSnapshots.Add(new ExportAnalyticsSnapshot
            {
                Id = Guid.NewGuid(),
                ExportLogId = log.Id,
                ExportLog = log,
                SnapshotVersion = 1,
                FiltersSnapshotJson = filtersJson,
                SortSnapshotJson = """{"schemaVersion":1,"sorts":[]}""",
                CreatedAtUtc = timestampUtc
            });
        }

        return log;
    }

    private static void SeedLocations(ApplicationDbContext db)
    {
        db.CanonicalLocations.AddRange(
            new CanonicalLocation { Key = "US", DisplayName = "United States", SortOrder = 1, IsActive = true },
            new CanonicalLocation { Key = "GB", DisplayName = "United Kingdom", SortOrder = 2, IsActive = true },
            new CanonicalLocation { Key = LocationConstants.UnknownLocationKey, DisplayName = "Unknown", SortOrder = 999, IsActive = true });
        db.LocationGroups.Add(new LocationGroup { Key = "western", DisplayName = "Western", Kind = "Business", SortOrder = 1 });
        db.LocationGroupItems.AddRange(
            new LocationGroupItem { GroupKey = "western", LocationKey = "US" },
            new LocationGroupItem { GroupKey = "western", LocationKey = "GB" });
    }

    private static string FiltersJson(params object[] filters)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["filters"] = filters
        });

    private static Dictionary<string, object?> MultiSelectFilter(string field, params string[] values)
        => Filter(field, "multiSelect", "anyOf", values);

    private static Dictionary<string, object?> AvailabilityFilter(string field, params string[] values)
        => Filter(field, "availability", "in", values);

    private static Dictionary<string, object?> EnumFilter(string field, string value)
        => Filter(field, "enum", "eq", value);

    private static Dictionary<string, object?> BooleanFilter(string field, bool value)
        => Filter(field, "boolean", "eq", value);

    private static Dictionary<string, object?> RangeFilter(string field, decimal? min, decimal? max)
    {
        var value = new Dictionary<string, object?>();
        if (min.HasValue)
        {
            value["min"] = min.Value;
        }

        if (max.HasValue)
        {
            value["max"] = max.Value;
        }

        return Filter(field, "numberRange", min.HasValue && max.HasValue ? "between" : min.HasValue ? "gte" : "lte", value);
    }

    private static Dictionary<string, object?> Filter(
        string field,
        string kind,
        string filterOperator,
        object value)
        => new()
        {
            ["field"] = field,
            ["kind"] = kind,
            ["operator"] = filterOperator,
            ["value"] = value
        };
}
