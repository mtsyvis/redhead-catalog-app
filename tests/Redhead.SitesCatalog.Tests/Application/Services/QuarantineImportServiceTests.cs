using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly QuarantineImportService _sut;

    public QuarantineImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sut = new QuarantineImportService(_context, NullLogger<QuarantineImportService>.Instance);

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
        Assert.Equal(2, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Unmatched);
        Assert.Empty(result.Errors);

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
        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

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
        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

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
        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Single(result.Unmatched);
        Assert.Equal("missing-site.com", result.Unmatched[0]);

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
        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);

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
        Assert.Equal(1, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Equal("Domain is required and cannot be empty after normalization.", result.Errors[0].Message);

        var test = await GetSiteAsync("test.com");
        Assert.True(test.IsQuarantined);
        Assert.Equal("OK", test.QuarantineReason);
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
        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Equal(1, result.DuplicatesCount);
        Assert.Single(result.Duplicates);
        Assert.Equal("example.com", result.Duplicates[0]);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Equal("Second reason", site.QuarantineReason);

        var log = await GetQuarantineImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(1, log!.Duplicates);
        Assert.Equal(1, log.Matched);
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
        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

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
        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Errors);

        var site = await GetSiteAsync("example.com");
        Assert.True(site.IsQuarantined);
        Assert.Equal("Policy review", site.QuarantineReason);
    }

    [Fact]
    public async Task ImportAsync_UnsupportedFileType_ReturnsErrorResult_AndDoesNotThrow()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Reason\n" +
            "example.com,Policy review\n");

        // Act
        var result = await _sut.ImportAsync(
            stream,
            fileName: "quarantine.xlsx",
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            userId: UserId,
            userEmail: UserEmail,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.Errors[0].RowNumber);
        Assert.Equal("Unsupported file type. Use CSV.", result.Errors[0].Message);

        Assert.Empty(_context.ImportLogs);
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
