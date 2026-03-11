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

public sealed class LastPublishedImportServiceTests : IDisposable
{
    private const string UserId = "u1";
    private const string UserEmail = "user@test.com";
    private const string CsvFileName = "last-published.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly LastPublishedImportService _sut;

    public LastPublishedImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sut = new LastPublishedImportService(_context, NullLogger<LastPublishedImportService>.Instance);

        SeedSites();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Theory]
    [InlineData("31.01.2026", 2026, 1, 31, false)]
    [InlineData("January 2026", 2026, 1, 1, true)]
    [InlineData("Jan 2026", 2026, 1, 1, true)]
    public async Task ImportAsync_ValidDate_UpdatesMatchedSite(
        string rawDate,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        bool expectedIsMonthOnly)
    {
        using var stream = Utf8Csv($"Domain,LastPublishedDate\nexample.com,{rawDate}\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Unmatched);
        Assert.Empty(result.Errors);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
        Assert.Equal(expectedIsMonthOnly, site.LastPublishedDateIsMonthOnly);

        var log = await GetLastPublishedImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Duplicates);
        Assert.Equal(1, log.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_SemicolonDelimitedFile_IsSupported()
    {
        using var stream = Utf8Csv("Domain;LastPublishedDate\nexample.com;31.01.2026\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);
    }

    [Fact]
    public async Task ImportAsync_Utf8Bom_IsAccepted()
    {
        using var stream = Utf8Csv("Domain,LastPublishedDate\nexample.com,31.01.2026\n", withBom: true);

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
    }

    [Fact]
    public async Task ImportAsync_EmptyLastPublishedDate_AddsRowError_AndDoesNotUpdateSite()
    {
        var originalUpdatedAt = await SetExistingLastPublishedDateAsync(
            "example.com",
            new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            isMonthOnly: false);

        using var stream = Utf8Csv("Domain,LastPublishedDate\nexample.com,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Equal("LastPublishedDate is required and cannot be empty.", result.Errors[0].Message);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);
        Assert.Equal(originalUpdatedAt, site.UpdatedAtUtc);

        var log = await GetLastPublishedImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(1, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_InvalidLastPublishedDate_AddsRowError_AndDoesNotUpdateSite()
    {
        var originalUpdatedAt = await SetExistingLastPublishedDateAsync(
            "example.com",
            new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            isMonthOnly: false);

        using var stream = Utf8Csv("Domain,LastPublishedDate\nexample.com,2026-01-31\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Equal(
            "LastPublishedDate could not be parsed. Use a full date 'DD.MM.YYYY' or month+year like 'January 2026' or 'Jan 2026'.",
            result.Errors[0].Message);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);
        Assert.Equal(originalUpdatedAt, site.UpdatedAtUtc);
    }

    [Fact]
    public async Task ImportAsync_InvalidDomain_AddsRowError_AndDoesNotUpdateAnything()
    {
        using var stream = Utf8Csv("Domain,LastPublishedDate\n,31.01.2026\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Equal("Domain is required and cannot be empty after normalization.", result.Errors[0].Message);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedDomain_IsReported_AndDoesNotCountAsMatched()
    {
        using var stream = Utf8Csv("Domain,LastPublishedDate\nmissing-site.com,31.01.2026\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Single(result.Unmatched);
        Assert.Equal("missing-site.com", result.Unmatched[0]);

        var log = await GetLastPublishedImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Matched);
        Assert.Equal(1, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomains_KeepsLastValue_AndStoresDuplicateCountInLog()
    {
        using var stream = Utf8Csv(
            "Domain,LastPublishedDate\n" +
            "example.com,31.01.2026\n" +
            "example.com,01.02.2026\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Equal(1, result.DuplicatesCount);
        Assert.Single(result.Duplicates);
        Assert.Equal("example.com", result.Duplicates[0]);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);

        var log = await GetLastPublishedImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(1, log!.Duplicates);
        Assert.Equal(1, log.Matched);
    }

    [Fact]
    public async Task ImportAsync_EmptyRows_AreIgnored()
    {
        using var stream = Utf8Csv(
            "Domain,LastPublishedDate\n" +
            "\n" +
            "example.com,31.01.2026\n" +
            ",\n" +
            "\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Errors);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
    }

    [Fact]
    public async Task ImportAsync_InvalidFirstRow_ValidDuplicateSecondRow_UsesValidRowAndDoesNotCountDuplicate()
    {
        using var stream = Utf8Csv(
            "Domain,LastPublishedDate\n" +
            "example.com,invalid\n" +
            "example.com,31.01.2026\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Equal(0, result.DuplicatesCount);
        Assert.Empty(result.Duplicates);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);

        var log = await GetLastPublishedImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Duplicates);
        Assert.Equal(1, log.Matched);
        Assert.Equal(1, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_ValidFirstRow_InvalidDuplicateSecondRow_KeepsFirstRowAndDoesNotCountDuplicate()
    {
        using var stream = Utf8Csv(
            "Domain,LastPublishedDate\n" +
            "example.com,31.01.2026\n" +
            "example.com,invalid\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Equal(0, result.DuplicatesCount);
        Assert.Empty(result.Duplicates);

        var site = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), site.LastPublishedDate);
        Assert.False(site.LastPublishedDateIsMonthOnly);

        var log = await GetLastPublishedImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Duplicates);
        Assert.Equal(1, log.Matched);
        Assert.Equal(1, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_MixedRows_AggregatesMatchedErrorsAndUnmatchedCorrectly()
    {
        using var stream = Utf8Csv(
            "Domain,LastPublishedDate\n" +
            "example.com,31.01.2026\n" +
            "missing-site.com,31.01.2026\n" +
            "month-only.com,\n" +
            "another-missing.com,not-a-date\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(2, result.ErrorsCount);
        Assert.Equal(2, result.Errors.Count);
        Assert.Single(result.Unmatched);
        Assert.Equal("missing-site.com", result.Unmatched[0]);

        var example = await GetSiteAsync("example.com");
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), example.LastPublishedDate);

        var monthOnly = await GetSiteAsync("month-only.com");
        Assert.Null(monthOnly.LastPublishedDate);

        var log = await GetLastPublishedImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(1, log!.Matched);
        Assert.Equal(1, log.Unmatched);
        Assert.Equal(2, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_UnsupportedFileType_ReturnsErrorResult_AndDoesNotThrow()
    {
        using var stream = Utf8Csv("Domain,LastPublishedDate\nexample.com,31.01.2026\n");

        var result = await _sut.ImportAsync(
            stream,
            fileName: "last-published.xlsx",
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            userId: UserId,
            userEmail: UserEmail,
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.Errors[0].RowNumber);
        Assert.Equal("Unsupported file type. Use CSV.", result.Errors[0].Message);

        Assert.Empty(_context.ImportLogs);
    }

    [Fact]
    public async Task ImportAsync_InvalidHeaderOrder_ThrowsImportHeaderValidationException()
    {
        using var stream = Utf8Csv("LastPublishedDate,Domain\n31.01.2026,example.com\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.Contains("header", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_MissingRequiredHeader_ThrowsImportHeaderValidationException()
    {
        using var stream = Utf8Csv("Domain\nexample.com\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.Contains("Missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_EmptyFile_ThrowsImportHeaderValidationException()
    {
        using var stream = Utf8Csv(string.Empty);

        await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));
    }

    [Fact]
    public async Task ImportAsync_NonUtf8File_ThrowsImportHeaderValidationException()
    {
        using var stream = Utf16Csv("Domain,LastPublishedDate\nexample.com,31.01.2026\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.Equal("CSV must be UTF-8 encoded.", exception.Message);
    }

    [Theory]
    [InlineData("31.01.2026", 2026, 1, 31, false)]
    [InlineData("January 2026", 2026, 1, 1, true)]
    [InlineData("Jan 2026", 2026, 1, 1, true)]
    public void TryParseLastPublishedDate_ValidValue_ReturnsExpectedResult(
        string input,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        bool expectedIsMonthOnly)
    {
        var success = LastPublishedImportService.TryParseLastPublishedDate(
            input,
            out var parsedDate,
            out var isMonthOnly,
            out var errorMessage);

        Assert.True(success);
        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay, 0, 0, 0, DateTimeKind.Utc), parsedDate);
        Assert.Equal(expectedIsMonthOnly, isMonthOnly);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-01-31")]
    [InlineData("31/01/2026")]
    [InlineData("январь 2026")]
    public void TryParseLastPublishedDate_InvalidValue_ReturnsFalseAndError(string input)
    {
        var success = LastPublishedImportService.TryParseLastPublishedDate(
            input,
            out var parsedDate,
            out var isMonthOnly,
            out var errorMessage);

        Assert.False(success);
        Assert.Equal(default, parsedDate);
        Assert.False(isMonthOnly);
        Assert.Equal(
            "LastPublishedDate could not be parsed. Use a full date 'DD.MM.YYYY' or month+year like 'January 2026' or 'Jan 2026'.",
            errorMessage);
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

    private async Task<ImportLog?> GetLastPublishedImportLogAsync()
    {
        return await _context.ImportLogs.SingleOrDefaultAsync(x => x.Type == ImportConstants.ImportTypeLastPublished);
    }

    private async Task<DateTime> SetExistingLastPublishedDateAsync(string domain, DateTime dateUtc, bool isMonthOnly)
    {
        var site = await GetSiteAsync(domain);
        site.LastPublishedDate = dateUtc;
        site.LastPublishedDateIsMonthOnly = isMonthOnly;
        site.UpdatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _context.SaveChangesAsync();
        return site.UpdatedAtUtc;
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
                DR = 10,
                Traffic = 1000,
                Location = "US",
                PriceUsd = 10,
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Site
            {
                Domain = "month-only.com",
                DR = 20,
                Traffic = 2000,
                Location = "US",
                PriceUsd = 20,
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        _context.SaveChanges();
    }
}
