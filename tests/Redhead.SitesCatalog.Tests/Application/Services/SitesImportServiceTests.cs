using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using System.Text;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class SitesImportServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string UserEmail = "admin@test.com";
    private const string CsvFileName = "sites.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly SitesImportService _sut;

    public SitesImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)) // to ignore warnings about transactions not being supported in InMemory provider
            .Options;

        _context = new ApplicationDbContext(options);
        _sut = new SitesImportService(_context, NullLogger<SitesImportService>.Instance);

        SeedSites();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ImportAsync_ValidCsv_InsertsNewSites_AndWritesImportLog()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "newsite.com,55,12000,US,100,150,200,250,Tech,News\n" +
            "secondsite.com,42,8000,UK,80,,,,Business,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.DuplicatesCount);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Duplicates);
        Assert.Empty(result.Errors);

        var newSite = await GetSiteAsync("newsite.com");
        Assert.Equal(55, newSite.DR);
        Assert.Equal(12000, newSite.Traffic);
        Assert.Equal("US", newSite.Location);
        Assert.Equal(100m, newSite.PriceUsd);
        Assert.Equal(150m, newSite.PriceCasino);
        Assert.Equal(200m, newSite.PriceCrypto);
        Assert.Equal(250m, newSite.PriceLinkInsert);
        Assert.Equal("Tech", newSite.Niche);
        Assert.Equal("News", newSite.Categories);
        Assert.False(newSite.IsQuarantined);

        var secondSite = await GetSiteAsync("secondsite.com");
        Assert.Equal(42, secondSite.DR);
        Assert.Equal(8000, secondSite.Traffic);
        Assert.Equal("UK", secondSite.Location);
        Assert.Equal(80m, secondSite.PriceUsd);
        Assert.Null(secondSite.PriceCasino);
        Assert.Null(secondSite.PriceCrypto);
        Assert.Null(secondSite.PriceLinkInsert);
        Assert.Equal("Business", secondSite.Niche);
        Assert.Null(secondSite.Categories);

        var log = await GetSitesImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(UserId, log!.UserId);
        Assert.Equal(UserEmail, log.UserEmail);
        Assert.Equal(2, log.Inserted);
        Assert.Equal(0, log.Duplicates);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_SemicolonDelimitedFile_IsSupported()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain;DR;Traffic;Location;PriceUsd;PriceCasino;PriceCrypto;PriceLinkInsert;Niche;Categories\n" +
            "semicolon.com;61;15000;DE;90;120;;;Finance;Blog\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.ErrorsCount);

        var site = await GetSiteAsync("semicolon.com");
        Assert.Equal(61, site.DR);
        Assert.Equal(15000, site.Traffic);
        Assert.Equal("DE", site.Location);
        Assert.Equal(90m, site.PriceUsd);
        Assert.Equal(120m, site.PriceCasino);
        Assert.Null(site.PriceCrypto);
        Assert.Null(site.PriceLinkInsert);
        Assert.Equal("Finance", site.Niche);
        Assert.Equal("Blog", site.Categories);
    }

    [Fact]
    public async Task ImportAsync_Utf8Bom_IsAccepted()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "bomsite.com,50,1000,US,10,,,,,\n",
            withBom: true);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.ErrorsCount);

        var site = await GetSiteAsync("bomsite.com");
        Assert.Equal(50, site.DR);
    }

    [Fact]
    public async Task ImportAsync_UnsupportedFileType_ReturnsErrorResult_AndDoesNotThrow()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "newsite.com,55,12000,US,100,150,200,250,Tech,News\n");

        // Act
        var result = await _sut.ImportAsync(
            stream,
            fileName: "sites.xlsx",
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            userId: UserId,
            userEmail: UserEmail,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Inserted);
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
            "DR,Domain,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,Niche,Categories\n" +
            "55,newsite.com,12000,US,100,150,200,250,Tech,News\n");

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
            "Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,Niche\n" +
            "newsite.com,55,12000,US,100,150,200,250,Tech\n");

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
            HeaderLine() +
            "newsite.com,55,12000,US,100,150,200,250,Tech,News\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Equal("CSV must be UTF-8 encoded.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainAlreadyExistsInDatabase_IsReportedAsDuplicate_AndNotInserted()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,150,200,250,Tech,News\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.DuplicatesCount);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Single(result.Duplicates);
        Assert.Equal("existing.com", result.Duplicates[0]);

        var allExisting = await _context.Sites.CountAsync(x => x.Domain == "existing.com");
        Assert.Equal(1, allExisting);

        var log = await GetSitesImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Inserted);
        Assert.Equal(1, log.Duplicates);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainsInFile_UsesLastValidRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "duplicate.com,10,1000,US,10,,,,,\n" +
            "duplicate.com,75,25000,CA,300,10,20,30,Finance,Magazine\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.DuplicatesCount);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Single(result.Duplicates);
        Assert.Equal("duplicate.com", result.Duplicates[0]);

        var site = await GetSiteAsync("duplicate.com");
        Assert.Equal(75, site.DR);
        Assert.Equal(25000, site.Traffic);
        Assert.Equal("CA", site.Location);
        Assert.Equal(300m, site.PriceUsd);
        Assert.Equal(10m, site.PriceCasino);
        Assert.Equal(20m, site.PriceCrypto);
        Assert.Equal(30m, site.PriceLinkInsert);
        Assert.Equal("Finance", site.Niche);
        Assert.Equal("Magazine", site.Categories);
    }

    [Fact]
    public async Task ImportAsync_LastValidRow_WinsEvenIfEarlierDuplicateIsInvalid()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "mixed-dup.com,invalid,1000,US,10,,,,,\n" +
            "mixed-dup.com,60,5000,US,15,,,,,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.DuplicatesCount);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Equal("Invalid numeric format for DR.", result.Errors[0].Message);

        var site = await GetSiteAsync("mixed-dup.com");
        Assert.Equal(60, site.DR);
        Assert.Equal(5000, site.Traffic);
        Assert.Equal(15m, site.PriceUsd);
    }

    [Fact]
    public async Task ImportAsync_EmptyRows_AreIgnored()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "\n" +
            "ignored-empty.com,50,1000,US,10,,,,,\n" +
            ",,,,,,,,,\n" +
            "\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Errors);

        var site = await GetSiteAsync("ignored-empty.com");
        Assert.Equal(50, site.DR);
    }

    [Theory]
    [InlineData(",55,12000,US,100,150,200,250,Tech,News", "Domain is required.")]
    [InlineData("bad-dr.com,,12000,US,100,150,200,250,Tech,News", "DR is required and must be between 0 and 100.")]
    [InlineData("bad-dr-format.com,abc,12000,US,100,150,200,250,Tech,News", "Invalid numeric format for DR.")]
    [InlineData("bad-dr-range.com,101,12000,US,100,150,200,250,Tech,News", "DR must be between 0 and 100.")]
    [InlineData("bad-traffic.com,55,,US,100,150,200,250,Tech,News", "Traffic is required and must be >= 0.")]
    [InlineData("bad-price.com,55,12000,US,,150,200,250,Tech,News", "Price USD is required and must be >= 0.")]
    [InlineData("bad-casino.com,55,12000,US,100,-1,200,250,Tech,News", "PriceCasino must be >= 0 or empty.")]
    [InlineData("bad-crypto.com,55,12000,US,100,150,-1,250,Tech,News", "PriceCrypto must be >= 0 or empty.")]
    [InlineData("bad-link-insert.com,55,12000,US,100,150,200,-1,Tech,News", "PriceLinkInsert must be >= 0 or empty.")]
    public async Task ImportAsync_InvalidRow_AddsError_AndSkipsRow(string csvRow, string expectedMessage)
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine() + csvRow + "\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Equal(expectedMessage, result.Errors[0].Message);
    }

    [Fact]
    public async Task ImportAsync_DomainIsNormalizedBeforeInsert()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "https://www.MixedCaseSite.com/,55,12000,US,100,150,200,250,Tech,News\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.Inserted);
        var site = await GetSiteAsync("mixedcasesite.com");
        Assert.Equal("mixedcasesite.com", site.Domain);
    }

    [Fact]
    public async Task ImportAsync_TooManyInvalidRows_CapsErrorDetailsButKeepsFullCount()
    {
        // Arrange
        var sb = new StringBuilder();
        sb.Append(HeaderLine());

        for (var i = 0; i < 250; i++)
        {
            sb.Append($",55,12000,US,100,150,200,250,Tech,News\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.Inserted);
        Assert.Equal(250, result.ErrorsCount);
        Assert.Equal(200, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal("Domain is required.", error.Message));
    }

    [Fact]
    public async Task ImportAsync_TooManyDuplicates_CapsDuplicateDetailsButKeepsFullCount()
    {
        // Arrange
        var sb = new StringBuilder();
        sb.Append(HeaderLine());

        for (var i = 0; i < 250; i++)
        {
            sb.Append("existing.com,55,12000,US,100,150,200,250,Tech,News\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.Inserted);
        Assert.Equal(250, result.DuplicatesCount);
        Assert.Equal(200, result.Duplicates.Count);
        Assert.All(result.Duplicates, duplicate => Assert.Equal("existing.com", duplicate));
    }

    [Fact]
    public async Task ImportAsync_WhenNoValidRows_StillWritesImportLog()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            ",55,12000,US,100,150,200,250,Tech,News\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.ErrorsCount);

        var log = await GetSitesImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(0, log!.Inserted);
        Assert.Equal(0, log.Duplicates);
        Assert.Equal(1, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_WhenRowsCountExceedsBatchSize_InsertsAllSitesAcrossMultipleChunks()
    {
        // Arrange
        var batchSize = ImportConstants.SitesImportBatchSize;
        var totalRows = batchSize * 2 + 137;

        var sb = new StringBuilder();
        sb.Append(HeaderLine());

        for (var i = 0; i < totalRows; i++)
        {
            sb.Append($"chunk-site-{i}.com,55,12000,US,100,150,200,250,Tech,News\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(totalRows, result.Inserted);
        Assert.Equal(0, result.DuplicatesCount);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Duplicates);
        Assert.Empty(result.Errors);

        var insertedCount = await _context.Sites.CountAsync(x => x.Domain.StartsWith("chunk-site-"));
        Assert.Equal(totalRows, insertedCount);

        var firstSite = await GetSiteAsync("chunk-site-0.com");
        Assert.Equal(55, firstSite.DR);
        Assert.Equal(12000, firstSite.Traffic);
        Assert.Equal("US", firstSite.Location);
        Assert.Equal(100m, firstSite.PriceUsd);

        var lastSite = await GetSiteAsync($"chunk-site-{totalRows - 1}.com");
        Assert.Equal(55, lastSite.DR);
        Assert.Equal(12000, lastSite.Traffic);
        Assert.Equal("US", lastSite.Location);
        Assert.Equal(100m, lastSite.PriceUsd);

        var log = await GetSitesImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(totalRows, log!.Inserted);
        Assert.Equal(0, log.Duplicates);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_WhenRowsExceedBatchSize_AndSomeDomainsAlreadyExist_InsertsOnlyNewSitesAndCountsDuplicates()
    {
        // Arrange
        var batchSize = ImportConstants.SitesImportBatchSize;
        var totalRows = batchSize * 2 + 73;

        var existingDomains = new HashSet<string>(StringComparer.Ordinal)
        {
            "existing-batch-10.com",
            "existing-batch-700.com",
            "existing-batch-1200.com",
            "existing-batch-1800.com"
        };

        foreach (var domain in existingDomains)
        {
            _context.Sites.Add(new Site
            {
                Domain = domain,
                DR = 40,
                Traffic = 5000,
                Location = "US",
                PriceUsd = 50m,
                IsQuarantined = false,
                CreatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }

        await _context.SaveChangesAsync();

        var sb = new StringBuilder();
        sb.Append(HeaderLine());

        for (var i = 0; i < totalRows; i++)
        {
            var domain = existingDomains.Contains($"existing-batch-{i}.com")
                ? $"existing-batch-{i}.com"
                : $"new-batch-site-{i}.com";

            sb.Append($"{domain},55,12000,US,100,150,200,250,Tech,News\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(totalRows - existingDomains.Count, result.Inserted);
        Assert.Equal(existingDomains.Count, result.DuplicatesCount);
        Assert.Equal(0, result.ErrorsCount);

        foreach (var domain in existingDomains)
        {
            var count = await _context.Sites.CountAsync(x => x.Domain == domain);
            Assert.Equal(1, count);
        }

        var insertedNewCount = await _context.Sites.CountAsync(x => x.Domain.StartsWith("new-batch-site-"));
        Assert.Equal(totalRows - existingDomains.Count, insertedNewCount);

        var log = await GetSitesImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(totalRows - existingDomains.Count, log!.Inserted);
        Assert.Equal(existingDomains.Count, log.Duplicates);
        Assert.Equal(0, log.ErrorsCount);
    }

    private async Task<SitesImportResult> ImportAsync(Stream stream)
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

    private async Task<ImportLog?> GetSitesImportLogAsync()
    {
        return await _context.ImportLogs.SingleOrDefaultAsync(x => x.Type == ImportConstants.ImportTypeSites);
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

    private static string HeaderLine()
    {
        return "Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,Niche,Categories\n";
    }

    private void SeedSites()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        _context.Sites.Add(
            new Site
            {
                Domain = "existing.com",
                DR = 40,
                Traffic = 5000,
                Location = "US",
                PriceUsd = 50m,
                PriceCasino = null,
                PriceCrypto = null,
                PriceLinkInsert = null,
                Niche = "General",
                Categories = "Blog",
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        _context.SaveChanges();
    }
}
