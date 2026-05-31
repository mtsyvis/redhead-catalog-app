using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using System.Security.Claims;

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
    public async Task ImportAvailability_WhenUnsupportedFileType_ReturnsBadRequest_AndDoesNotCallService()
    {
        var quarantineService = new StubQuarantineImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), quarantineImportService: quarantineService);

        var result = await sut.ImportAvailability(CreateUnsupportedFile("availability.xlsx"), "markUnavailable", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(0, quarantineService.CallCount);
    }

    [Fact]
    public async Task ImportAvailability_WhenActionMissing_ReturnsBadRequest_AndDoesNotCallService()
    {
        // Arrange
        var quarantineService = new StubQuarantineImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), quarantineImportService: quarantineService);

        // Act
        var result = await sut.ImportAvailability(CreateCsvFile("availability.csv"), null, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(0, quarantineService.CallCount);
    }

    [Fact]
    public async Task ImportAvailability_WhenRestoreAvailableAction_CallsServiceWithRestoreAvailable()
    {
        // Arrange
        var quarantineService = new StubQuarantineImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), quarantineImportService: quarantineService);
        AttachUser(sut);

        // Act
        var result = await sut.ImportAvailability(CreateCsvFile("availability.csv"), "restoreAvailable", CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, quarantineService.CallCount);
        Assert.Equal(SiteAvailabilityImportAction.RestoreAvailable, quarantineService.LastAction);
    }

    [Fact]
    public async Task ImportAvailability_WhenUnsupportedAction_ReturnsBadRequest_AndDoesNotCallService()
    {
        // Arrange
        var quarantineService = new StubQuarantineImportService();
        var sut = CreateController(new StubImportArtifactStorageService(), quarantineImportService: quarantineService);

        // Act
        var result = await sut.ImportAvailability(CreateCsvFile("availability.csv"), "restore", CancellationToken.None);

        // Assert
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

    private static IFormFile CreateCsvFile(string fileName)
    {
        var bytes = "Domain,Reason\nexample.com,Reason\n"u8.ToArray();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private static void AttachUser(ControllerBase controller)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Email, "admin@test.com")
            },
            authenticationType: "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
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

        public ImportArtifactHandle StoreWarningRows(string importType, WarningRowsImportArtifactPayload payload)
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
        public SiteAvailabilityImportAction? LastAction { get; private set; }

        public Task<SitesUpdateImportResult> ImportAsync(
            Stream fileStream,
            string fileName,
            string? contentType,
            string userId,
            string userEmail,
            SiteAvailabilityImportAction action,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastAction = action;
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
