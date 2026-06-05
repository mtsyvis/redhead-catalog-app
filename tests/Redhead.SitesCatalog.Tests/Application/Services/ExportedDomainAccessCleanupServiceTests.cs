using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class ExportedDomainAccessCleanupServiceTests
{
    [Fact]
    public async Task DeleteOldAccessesAsync_RemovesOnlyRowsBeforeCutoff()
    {
        // Arrange
        await using var db = CreateDbContext();
        var cutoffUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        AddExportedDomainAccess(db, "old.example.com", cutoffUtc.AddTicks(-1));
        AddExportedDomainAccess(db, "cutoff.example.com", cutoffUtc);
        AddExportedDomainAccess(db, "recent.example.com", cutoffUtc.AddMinutes(1));
        await db.SaveChangesAsync();

        var sut = new ExportedDomainAccessCleanupService(db);

        // Act
        var deletedCount = await sut.DeleteOldAccessesAsync(cutoffUtc, batchSize: 10);

        // Assert
        Assert.Equal(1, deletedCount);
        var remainingDomains = await db.ExportedDomainAccesses
            .OrderBy(access => access.Domain)
            .Select(access => access.Domain)
            .ToArrayAsync();
        Assert.Equal(["cutoff.example.com", "recent.example.com"], remainingDomains);
    }

    [Fact]
    public async Task DeleteOldAccessesAsync_DeletesOldRowsInBatches()
    {
        // Arrange
        await using var db = CreateDbContext();
        var cutoffUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            AddExportedDomainAccess(db, $"old-{i}.example.com", cutoffUtc.AddDays(-1));
        }
        AddExportedDomainAccess(db, "recent.example.com", cutoffUtc.AddDays(1));
        await db.SaveChangesAsync();

        var sut = new ExportedDomainAccessCleanupService(db);

        // Act
        var deletedCount = await sut.DeleteOldAccessesAsync(cutoffUtc, batchSize: 2);

        // Assert
        Assert.Equal(5, deletedCount);
        var remainingDomain = await db.ExportedDomainAccesses
            .Select(access => access.Domain)
            .SingleAsync();
        Assert.Equal("recent.example.com", remainingDomain);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void AddExportedDomainAccess(
        ApplicationDbContext db,
        string domain,
        DateTime exportedAtUtc)
    {
        var exportLog = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            UserEmail = "user@example.com",
            Role = AppRoles.Client,
            TimestampUtc = exportedAtUtc,
            RowsReturned = 1,
            RequestedRowsCount = 1,
            ExportedRowsCount = 1,
            Destination = ExportConstants.DestinationDownload,
            ExportMode = ExportConstants.ExportModeSites
        };

        db.ExportLogs.Add(exportLog);
        db.ExportedDomainAccesses.Add(new ExportedDomainAccess
        {
            Id = Guid.NewGuid(),
            ExportLogId = exportLog.Id,
            UserId = exportLog.UserId,
            Domain = domain,
            ExportedAtUtc = exportedAtUtc
        });
    }
}
