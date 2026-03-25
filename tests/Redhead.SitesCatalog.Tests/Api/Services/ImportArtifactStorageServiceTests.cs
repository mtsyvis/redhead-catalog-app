using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Tests.Api.Services;

public sealed class ImportArtifactStorageServiceTests
{
    [Fact]
    public void GetCsvDownload_WhenTokenMissing_ReturnsNull()
    {
        var sut = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));

        var download = sut.GetCsvDownload("missing-token");

        Assert.Null(download);
    }

    [Fact]
    public void StoreInvalidRows_ThenGetCsvDownload_ReturnsCsvWithRequiredColumns()
    {
        var sut = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));
        var payload = new InvalidRowsImportArtifactPayload
        {
            Headers = ["Domain", "Reason"],
            Rows =
            {
                new InvalidImportRowRecord
                {
                    SourceRowNumber = 7,
                    RawValues = ["example.com", "raw reason"],
                    Errors = ["Domain is invalid", "Reason is too long"]
                }
            }
        };

        var handle = sut.StoreInvalidRows("quarantine", payload);
        var download = sut.GetCsvDownload(handle.Token);

        Assert.NotNull(download);
        Assert.Equal("text/csv", download!.ContentType);
        Assert.Equal(handle.FileName, download.FileName);

        var csv = Encoding.UTF8.GetString(download.Content);
        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd('\r'))
            .ToArray();

        Assert.Equal("Domain,Reason,Source Row Number,Error Details", lines[0]);
        Assert.Equal("example.com,raw reason,7,Domain is invalid; Reason is too long", lines[1]);
    }
}
