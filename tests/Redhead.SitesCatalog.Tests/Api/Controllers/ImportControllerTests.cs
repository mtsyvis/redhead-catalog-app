using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ImportControllerTests
{
    [Fact]
    public void DownloadImportArtifact_WhenTokenExists_ReturnsFileResult()
    {
        var artifactStorage = new StubImportArtifactStorageService
        {
            Download = new ImportArtifactDownload
            {
                FileName = "sites-invalid-rows.csv",
                ContentType = "text/csv",
                Content = "Domain,Source Row Number,Error Details\nexample.com,2,Domain is required.\n"u8.ToArray()
            }
        };

        var sut = CreateController(artifactStorage);

        var result = sut.DownloadImportArtifact("known-token");

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        Assert.Equal("sites-invalid-rows.csv", fileResult.FileDownloadName);
    }

    [Fact]
    public void DownloadImportArtifact_WhenTokenMissing_ReturnsNotFound()
    {
        var sut = CreateController(new StubImportArtifactStorageService());

        var result = sut.DownloadImportArtifact("missing-token");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    private static ImportController CreateController(IImportArtifactStorageService artifactStorage)
    {
        return new ImportController(
            new StubSitesImportService(),
            new StubQuarantineImportService(),
            new StubLastPublishedImportService(),
            new StubSitesUpdateImportService(),
            artifactStorage,
            NullLogger<ImportController>.Instance);
    }

    private sealed class StubImportArtifactStorageService : IImportArtifactStorageService
    {
        public ImportArtifactDownload? Download { get; set; }

        public ImportArtifactHandle StoreInvalidRows(string importType, InvalidRowsImportArtifactPayload payload)
            => throw new NotImplementedException();

        public ImportArtifactHandle StoreUnmatchedRows(string importType, UnmatchedRowsImportArtifactPayload payload)
            => throw new NotImplementedException();

        public ImportArtifactDownload? GetCsvDownload(string token) => Download;
    }

    private sealed class StubSitesImportService : ISitesImportService
    {
        public Task<SitesImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubQuarantineImportService : IQuarantineImportService
    {
        public Task<SitesUpdateImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubLastPublishedImportService : ILastPublishedImportService
    {
        public Task<SitesUpdateImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubSitesUpdateImportService : ISitesUpdateImportService
    {
        public Task<SitesUpdateImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
