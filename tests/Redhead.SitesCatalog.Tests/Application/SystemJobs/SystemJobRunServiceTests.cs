using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.SystemJobs;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Repositories;

namespace Redhead.SitesCatalog.Tests.Application.SystemJobs;

public sealed class SystemJobRunServiceTests
{
    [Fact]
    public async Task StartRunAndMarkSucceededAsync_TracksSuccessfulRun()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = new SystemJobRunService(new SystemJobRunRepository(db));
        var periodKey = "2026-W23";

        // Act
        var run = await sut.StartRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            periodKey,
            CancellationToken.None);
        var existsBeforeSuccess = await sut.HasSuccessfulRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            periodKey,
            CancellationToken.None);
        await sut.MarkSucceededAsync(
            run.Id,
            cancellationToken: CancellationToken.None);
        var existsAfterSuccess = await sut.HasSuccessfulRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            periodKey,
            CancellationToken.None);

        // Assert
        Assert.False(existsBeforeSuccess);
        Assert.True(existsAfterSuccess);

        var saved = await db.SystemJobRuns.SingleAsync();
        Assert.Equal(SystemJobRunStatus.Succeeded, saved.Status);
        Assert.NotNull(saved.FinishedAtUtc);
        Assert.Null(saved.ErrorMessage);
    }

    [Fact]
    public async Task AddArtifactAsync_StoresFileMetadataSeparateFromJobRun()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = new SystemJobRunService(new SystemJobRunRepository(db));
        var run = await sut.StartRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            "2026-W23",
            CancellationToken.None);

        // Act
        var artifact = await sut.AddArtifactAsync(
            run.Id,
            new SystemJobArtifactInput(
                "redhead-sites-full-2026-06-01.xlsx",
                1234,
                StorageProvider: "GoogleDrive",
                StoragePath: "shared-drive/path",
                ExternalFileId: "file-1"),
            CancellationToken.None);

        // Assert
        var saved = await db.SystemJobArtifacts.SingleAsync();
        Assert.Equal(artifact.Id, saved.Id);
        Assert.Equal(run.Id, saved.SystemJobRunId);
        Assert.Equal("redhead-sites-full-2026-06-01.xlsx", saved.FileName);
        Assert.Equal(1234, saved.FileSizeBytes);
        Assert.Equal("GoogleDrive", saved.StorageProvider);
        Assert.Equal("shared-drive/path", saved.StoragePath);
        Assert.Equal("file-1", saved.ExternalFileId);
    }

    [Fact]
    public async Task MarkFailedAsync_TracksFailureDetails()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = new SystemJobRunService(new SystemJobRunRepository(db));
        var run = await sut.StartRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            "2026-W23",
            CancellationToken.None);

        // Act
        await sut.MarkFailedAsync(run.Id, "Export failed.", CancellationToken.None);

        // Assert
        var saved = await db.SystemJobRuns.SingleAsync();
        Assert.Equal(SystemJobRunStatus.Failed, saved.Status);
        Assert.NotNull(saved.FinishedAtUtc);
        Assert.Equal("Export failed.", saved.ErrorMessage);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
