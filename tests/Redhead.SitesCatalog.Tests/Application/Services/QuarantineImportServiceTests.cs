using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using System.Text;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class QuarantineImportServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string UserEmail = "admin@test.com";
    private const string CsvFileName = "quarantine.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly ImportArtifactStorageService _artifactStorageService;
    private readonly QuarantineImportService _sut;

    public QuarantineImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _artifactStorageService = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));
        _sut = new QuarantineImportService(_context, NullLogger<QuarantineImportService>.Instance, _artifactStorageService);

        SeedSites();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ImportAsync_ValidCsv_UpdatesMatchedSites()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "example.com,Policy review\n" +
            "test.com,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(0, result.UnmatchedRowsCount);

        var example = await GetSiteAsync("example.com");
        Assert.True(example.IsQuarantined);
        Assert.Equal("Policy review", example.QuarantineReason);
        Assert.NotNull(example.QuarantineUpdatedAtUtc);

        var test = await GetSiteAsync("test.com");
        Assert.True(test.IsQuarantined);
        Assert.Null(test.QuarantineReason);
        Assert.NotNull(test.QuarantineUpdatedAtUtc);

        var log = await GetQuarantineImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Duplicates);
        Assert.Equal(2, log.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_SemicolonDelimitedFile_IsSupported()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain;Reason\n" +
            "example.com;Policy review\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Equal("Policy review", site.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_Utf8Bom_IsAccepted()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "example.com,Policy review\n",
            withBom: true);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Equal("Policy review", site.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedDomain_IsReported()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "example.com,OK\n" +
            "missing-site.com,Why\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.UnmatchedRows);
        Assert.Null(result.Downloads.InvalidRows);

        var download = _artifactStorageService.GetCsvDownload(result.Downloads.UnmatchedRows!.Token);
        Assert.NotNull(download);
        var csv = Encoding.UTF8.GetString(download!.Content);
        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd('\r'))
            .ToArray();
        Assert.Equal("Domain,Reason,Source Row Number", lines[0]);
        Assert.Equal("missing-site.com,Why,3", lines[1]);

        var log = await GetQuarantineImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(1, log!.Matched);
        Assert.Equal(1, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_NormalizesDomain_AndMatchesExistingSite()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "https://www.Example.COM/,Normalized\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.UnmatchedRowsCount);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Equal("Normalized", site.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_EmptyDomain_AddsRowError_AndSkipsRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            ",Some reason\n" +
            "test.com,OK\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains(",2,Domain is required and cannot be empty after normalization.", StringComparison.Ordinal));

        var test = await GetSiteAsync("test.com");
        Assert.True(test.IsQuarantined);
        Assert.Equal("OK", test.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_InvalidRow_InvalidRowsDownloadContainsSourceRowNumber()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            ",Some reason\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var token = result.Downloads.InvalidRows!.Token;
        Assert.False(string.IsNullOrWhiteSpace(token));

        var download = _artifactStorageService.GetCsvDownload(token);
        Assert.NotNull(download);

        var csv = Encoding.UTF8.GetString(download!.Content);
        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd('\r'))
            .ToArray();

        Assert.Equal("Domain,Reason,Source Row Number,Error Details", lines[0]);
        Assert.Equal(",Some reason,2,Domain is required and cannot be empty after normalization.", lines[1]);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomains_KeepsLastReason_AndStoresDuplicateCountInLog()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "example.com,First reason\n" +
            "example.com,Second reason\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("example.com", result.DuplicateDomainsPreview[0]);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Equal("Second reason", site.QuarantineReason);

        var log = await GetQuarantineImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(1, log!.Duplicates);
        Assert.Equal(1, log.Matched);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainsPreview_CountsUniqueDomains_AndLimitsTo100()
    {
        var sb = new StringBuilder();
        sb.Append("Domain,Reason\n");
        for (var i = 0; i < ImportConstants.DuplicateDomainsPreviewLimit + 1; i++)
        {
            sb.Append($"dupe-{i}.com,First\n");
            sb.Append($"dupe-{i}.com,Second\n");
        }
        sb.Append("example.com,Final\n");

        using var stream = Utf8Csv(sb.ToString());

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(101, result.DuplicateDomainsCount);
        Assert.Equal(100, result.DuplicateDomainsPreview.Count);
        Assert.Equal("dupe-0.com", result.DuplicateDomainsPreview[0]);
        Assert.Equal("dupe-99.com", result.DuplicateDomainsPreview[99]);
        Assert.DoesNotContain("dupe-100.com", result.DuplicateDomainsPreview);
    }

    [Fact]
    public async Task ImportAsync_InvalidRows_RemainSeparate_FromUnmatchedRows()
    {
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            ",Invalid\n" +
            "missing-site.com,Why\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        Assert.NotNull(result.Downloads.UnmatchedRows);
    }

    [Fact]
    public async Task ImportAsync_AllRowsWithInvalidDomain_AreExportedAsInvalid_WithoutDuplicatePreview()
    {
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            ",Invalid one\n" +
            ",Invalid two\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(2, result.InvalidRowsCount);
        Assert.Equal(0, result.UnmatchedRowsCount);
        Assert.Equal(0, result.DuplicateDomainsCount);
        Assert.Empty(result.DuplicateDomainsPreview);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        Assert.Null(result.Downloads.UnmatchedRows);
    }

    [Fact]
    public async Task ImportAsync_EmptyReason_IsAllowed_AndStoredAsNull()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "example.com,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Null(site.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_EmptyRows_AreIgnored()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "\n" +
            "example.com,Policy review\n" +
            ",\n" +
            "\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Equal("Policy review", site.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_InvalidHeaderOrder_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Reason,Domain\n" +
            "Policy review,example.com\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.StartsWith("CSV header is invalid.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_MissingRequiredHeader_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain\n" +
            "example.com\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.StartsWith("CSV header is invalid.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_EmptyFile_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(string.Empty);

        // Act / Assert
        await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));
    }

    [Fact]
    public async Task ImportAsync_NonUtf8File_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf16Csv(
            "Domain,Reason\n" +
            "example.com,Policy review\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Equal("CSV must be UTF-8 encoded.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_WritesImportLog()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "example.com,OK\n");

        // Act
        await ImportAsync(stream);

        // Assert
        var log = await GetQuarantineImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(UserId, log!.UserId);
        Assert.Equal(UserEmail, log.UserEmail);
        Assert.Equal(1, log.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
        Assert.Equal(0, log.Duplicates);
    }

    private async Task<SitesUpdateImportResult> ImportAsync(Stream stream)
    {
        return await _sut.ImportAsync(
            stream,
            CsvFileName,
            CsvContentType,
            UserId,
            UserEmail,
            CancellationToken.None);
    }

    private async Task<Site> GetSiteAsync(string domain)
    {
        return await _context.Sites.SingleAsync(x => x.Domain == domain);
    }

    private async Task<ImportLog?> GetQuarantineImportLogAsync()
    {
        return await _context.ImportLogs.SingleOrDefaultAsync(x => x.Type == ImportConstants.ImportTypeQuarantine);
    }

    private string[] GetDownloadLines(string token)
    {
        var download = _artifactStorageService.GetCsvDownload(token);
        Assert.NotNull(download);

        return Encoding.UTF8.GetString(download!.Content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd('\r'))
            .ToArray();
    }

    private static MemoryStream Utf8Csv(string text, bool withBom = false)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: withBom);
        return new MemoryStream(encoding.GetBytes(text));
    }

    private static MemoryStream Utf16Csv(string text)
    {
        return new MemoryStream(Encoding.Unicode.GetBytes(text));
    }

    private void SeedSites()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        _context.Sites.AddRange(
            new Site
            {
                Domain = "example.com",
                DR = 50,
                Traffic = 10000,
                Location = "US",
                PriceUsd = 100m,
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Site
            {
                Domain = "test.com",
                DR = 70,
                Traffic = 50000,
                Location = "UK",
                PriceUsd = 200m,
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        _context.SaveChanges();
    }
}
