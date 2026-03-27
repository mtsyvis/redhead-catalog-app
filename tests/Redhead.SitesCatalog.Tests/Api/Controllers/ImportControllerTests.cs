using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ImportControllerTests
{
    [Fact]
    public async Task ImportSites_WhenUnsupportedFileType_ReturnsBadRequest_AndDoesNotCallService()
    {
        var sitesService = new StubSitesImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), sitesImportService: sitesService);

        var result = await sut.ImportSites(CreateUnsupportedFile("sites.xlsx"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(0, sitesService.CallCount);
    }

    [Fact]
    public async Task ImportSitesUpdate_WhenUnsupportedFileType_ReturnsBadRequest_AndDoesNotCallService()
    {
        var sitesUpdateService = new StubSitesUpdateImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), sitesUpdateImportService: sitesUpdateService);

        var result = await sut.ImportSitesUpdate(CreateUnsupportedFile("sites-update.xlsx"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(0, sitesUpdateService.CallCount);
    }

    [Fact]
    public async Task ImportLastPublished_WhenUnsupportedFileType_ReturnsBadRequest_AndDoesNotCallService()
    {
        var lastPublishedService = new StubLastPublishedImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), lastPublishedImportService: lastPublishedService);

        var result = await sut.ImportLastPublished(CreateUnsupportedFile("last-published.xlsx"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(0, lastPublishedService.CallCount);
    }

    [Fact]
    public async Task ImportQuarantine_WhenUnsupportedFileType_ReturnsBadRequest_AndDoesNotCallService()
    {
        var quarantineService = new StubQuarantineImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), quarantineImportService: quarantineService);

        var result = await sut.ImportQuarantine(CreateUnsupportedFile("quarantine.xlsx"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(0, quarantineService.CallCount);
    }

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

    private static IFormFile CreateUnsupportedFile(string fileName)
    {
        var bytes = "not,csv,for,type-check\n1,2,3\n"u8.ToArray();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private static ImportController CreateController(
        IImportArtifactStorageService artifactStorage,
        ISitesImportService? sitesImportService = null,
        IQuarantineImportService? quarantineImportService = null,
        ILastPublishedImportService? lastPublishedImportService = null,
        ISitesUpdateImportService? sitesUpdateImportService = null)
    {
        return new ImportController(
            sitesImportService ?? new StubSitesImportService(),
            quarantineImportService ?? new StubQuarantineImportService(),
            lastPublishedImportService ?? new StubLastPublishedImportService(),
            sitesUpdateImportService ?? new StubSitesUpdateImportService(),
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
        public int CallCount { get; private set; }

        public Task<SitesImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SitesImportResult());
        }
    }

    private sealed class StubQuarantineImportService : IQuarantineImportService
    {
        public int CallCount { get; private set; }

        public Task<SitesUpdateImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SitesUpdateImportResult());
        }
    }

    private sealed class StubLastPublishedImportService : ILastPublishedImportService
    {
        public int CallCount { get; private set; }

        public Task<SitesUpdateImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SitesUpdateImportResult());
        }
    }

    private sealed class StubSitesUpdateImportService : ISitesUpdateImportService
    {
        public int CallCount { get; private set; }

        public Task<SitesUpdateImportResult> ImportAsync(Stream fileStream, string fileName, string? contentType, string userId, string userEmail, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SitesUpdateImportResult());
        }
    }
}
