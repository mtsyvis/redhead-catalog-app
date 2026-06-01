using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.SystemJobs;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.SystemExports;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;
using Redhead.SitesCatalog.Infrastructure.Repositories;

namespace Redhead.SitesCatalog.Tests.Application.Exports;

public sealed class EmergencySitesExportRunnerTests
{
    [Fact]
    public async Task RunOnceAsync_GeneratesUploadsAndTracksSuccessfulRun()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.Sites.Add(new Site
        {
            Domain = "example.com",
            DR = 10,
            Traffic = 100,
            Location = "US",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var storage = new FakeSystemExportStorage();
        var sut = CreateRunner(db, storage);
        var utcNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await sut.RunOnceAsync(utcNow, CancellationToken.None);

        // Assert
        Assert.False(result.Skipped);
        Assert.Equal("2026-W23", result.PeriodKey);
        Assert.NotNull(result.RunId);
        Assert.NotNull(result.UploadedFile);
        Assert.Equal(SystemExportStorageProviders.GoogleDriveSharedDrive, result.UploadedFile.StorageProvider);
        Assert.Equal(1, storage.UploadCount);
        Assert.Equal(1, storage.CleanupCount);
        Assert.StartsWith("redhead-sites-full-", storage.UploadedFileName, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", storage.UploadedFileName, StringComparison.Ordinal);

        var run = await db.SystemJobRuns.Include(jobRun => jobRun.Artifacts).SingleAsync();
        Assert.Equal(SystemJobNames.WeeklySitesEmergencyExport, run.JobName);
        Assert.Equal(SystemJobRunStatus.Succeeded, run.Status);
        Assert.Equal("2026-W23", run.PeriodKey);
        Assert.Single(run.Artifacts);
        Assert.Equal("file-1", run.Artifacts.Single().ExternalFileId);
        Assert.Equal(SystemExportStorageProviders.GoogleDriveSharedDrive, run.Artifacts.Single().StorageProvider);
        Assert.Empty(db.ExportLogs);
    }

    [Fact]
    public async Task RunOnceAsync_WithExistingSuccessfulRun_SkipsUpload()
    {
        // Arrange
        await using var db = CreateDbContext();
        var jobRunService = new SystemJobRunService(new SystemJobRunRepository(db));
        var run = await jobRunService.StartRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            "2026-W23",
            CancellationToken.None);
        await jobRunService.MarkSucceededAsync(run.Id, CancellationToken.None);

        var storage = new FakeSystemExportStorage();
        var sut = CreateRunner(db, storage);
        var utcNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await sut.RunOnceAsync(utcNow, CancellationToken.None);

        // Assert
        Assert.True(result.Skipped);
        Assert.Null(result.RunId);
        Assert.Equal("2026-W23", result.PeriodKey);
        Assert.Equal(0, storage.UploadCount);
        Assert.Equal(0, storage.CleanupCount);
        Assert.Single(db.SystemJobRuns);
    }

    [Fact]
    public async Task RunOnceAsync_WhenUploadFails_MarksRunFailed()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeSystemExportStorage
        {
            UploadException = new InvalidOperationException("Upload failed.")
        };
        var sut = CreateRunner(db, storage);
        var utcNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunOnceAsync(utcNow, CancellationToken.None));

        // Assert
        Assert.Equal("Upload failed.", exception.Message);
        Assert.Equal(1, storage.UploadCount);
        Assert.Equal(0, storage.CleanupCount);

        var run = await db.SystemJobRuns.SingleAsync();
        Assert.Equal(SystemJobRunStatus.Failed, run.Status);
        Assert.Equal("Upload failed.", run.ErrorMessage);
        Assert.NotNull(run.FinishedAtUtc);
    }

    private static EmergencySitesExportRunner CreateRunner(
        ApplicationDbContext db,
        ISystemExportStorage storage)
        => new(
            new EmergencySitesExportService(db, new SitesExcelExportGenerator()),
            storage,
            new SystemJobRunService(new SystemJobRunRepository(db)),
            Options.Create(new EmergencySitesExportOptions
            {
                Enabled = true,
                GoogleDriveFolderId = "folder-1",
                ServiceAccountJsonPath = "unused.json",
                RetentionWeeks = 8,
                FilePrefix = "redhead-sites-full"
            }));

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeSystemExportStorage : ISystemExportStorage
    {
        public int UploadCount { get; private set; }
        public int CleanupCount { get; private set; }
        public string? UploadedFileName { get; private set; }
        public Exception? UploadException { get; init; }

        public async Task<SystemExportUploadedFile> UploadAsync(
            string fileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            UploadCount++;
            UploadedFileName = fileName;
            if (UploadException != null)
            {
                throw UploadException;
            }

            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);

            return new SystemExportUploadedFile(
                fileName,
                copy.Length,
                SystemExportStorageProviders.GoogleDriveSharedDrive,
                $"google-drive://folders/folder-1/{fileName}",
                "file-1",
                "https://drive.google.com/file/d/file-1/view");
        }

        public Task<SystemExportCleanupResult> DeleteOldFilesAsync(
            string fileNamePrefix,
            TimeSpan retention,
            CancellationToken cancellationToken = default)
        {
            CleanupCount++;
            return Task.FromResult(new SystemExportCleanupResult(DeletedCount: 1, FailedCount: 0));
        }
    }
}
