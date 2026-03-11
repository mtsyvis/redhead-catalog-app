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

public sealed class SitesUpdateImportServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string UserEmail = "admin@test.com";
    private const string CsvFileName = "sites-update.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly SitesUpdateImportService _sut;

    public SitesUpdateImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sut = new SitesUpdateImportService(_context, NullLogger<SitesUpdateImportService>.Instance);

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
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,150,200,250,Tech,News\n" +
            "second.com,42,8000,UK,80,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(2, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Unmatched);
        Assert.Empty(result.Errors);
        Assert.Equal(0, result.DuplicatesCount);
        Assert.Empty(result.Duplicates);

        var first = await GetSiteAsync("existing.com");
        Assert.Equal("existing.com", first.Domain);
        Assert.Equal(55, first.DR);
        Assert.Equal(12000, first.Traffic);
        Assert.Equal("US", first.Location);
        Assert.Equal(100m, first.PriceUsd);
        Assert.Equal(150m, first.PriceCasino);
        Assert.Equal(200m, first.PriceCrypto);
        Assert.Equal(250m, first.PriceLinkInsert);
        Assert.Equal("Tech", first.Niche);
        Assert.Equal("News", first.Categories);

        var second = await GetSiteAsync("second.com");
        Assert.Equal(42, second.DR);
        Assert.Equal(8000, second.Traffic);
        Assert.Equal("UK", second.Location);
        Assert.Equal(80m, second.PriceUsd);
        Assert.Null(second.PriceCasino);
        Assert.Null(second.PriceCrypto);
        Assert.Null(second.PriceLinkInsert);
        Assert.Null(second.Niche);
        Assert.Null(second.Categories);

        var log = await GetSitesUpdateImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(2, log!.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
        Assert.Equal(0, log.Duplicates);
    }

    [Fact]
    public async Task ImportAsync_SemicolonDelimitedFile_IsSupported()
    {
        using var stream = Utf8Csv(
            "Domain;DR;Traffic;Location;PriceUsd;PriceCasino;PriceCrypto;PriceLinkInsert;Niche;Categories\n" +
            "existing.com;61;15000;DE;90;120;;;Finance;Blog\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await GetSiteAsync("existing.com");
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
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,50,1000,US,10,,,,,\n",
            withBom: true);

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal(50, site.DR);
    }

    [Fact]
    public async Task ImportAsync_UnsupportedFileType_ReturnsErrorResult_AndDoesNotThrow()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,150,200,250,Tech,News\n");

        var result = await _sut.ImportAsync(
            stream,
            fileName: "sites.xlsx",
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
        using var stream = Utf8Csv(
            "DR,Domain,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,Niche,Categories\n" +
            "55,existing.com,12000,US,100,150,200,250,Tech,News\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.StartsWith("CSV header is invalid.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_MissingRequiredHeader_ThrowsImportHeaderValidationException()
    {
        using var stream = Utf8Csv(
            "Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,Niche\n" +
            "existing.com,55,12000,US,100,150,200,250,Tech\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.StartsWith("CSV header is invalid.", exception.Message);
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
        using var stream = Utf16Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,150,200,250,Tech,News\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.Equal("CSV must be UTF-8 encoded.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedDomain_IsReported()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,,,\n" +
            "missing-site.com,42,5000,UK,50,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Single(result.Unmatched);
        Assert.Equal("missing-site.com", result.Unmatched[0]);

        var log = await GetSitesUpdateImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(1, log!.Matched);
        Assert.Equal(1, log.Unmatched);
    }

    [Fact]
    public async Task ImportAsync_EmptyOptionalFields_UpdateValuesToNull()
    {
        var site = await GetSiteAsync("existing.com");
        site.PriceCasino = 100m;
        site.PriceCrypto = 200m;
        site.Niche = "OldNiche";
        site.Categories = "OldCat";
        await _context.SaveChangesAsync();

        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);

        var updated = await GetSiteAsync("existing.com");
        Assert.Equal(55, updated.DR);
        Assert.Equal(100m, updated.PriceUsd);
        Assert.Null(updated.PriceCasino);
        Assert.Null(updated.PriceCrypto);
        Assert.Null(updated.PriceLinkInsert);
        Assert.Null(updated.Niche);
        Assert.Null(updated.Categories);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainsInFile_ReturnsDuplicates_AndLastValidRowWins()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,10,1000,US,10,,,,,\n" +
            "existing.com,75,25000,CA,300,10,20,30,Finance,Magazine\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(1, result.DuplicatesCount);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Single(result.Duplicates);
        Assert.Equal("existing.com", result.Duplicates[0]);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal(75, site.DR);
        Assert.Equal(25000, site.Traffic);
        Assert.Equal("CA", site.Location);
        Assert.Equal(300m, site.PriceUsd);
        Assert.Equal(10m, site.PriceCasino);
        Assert.Equal(20m, site.PriceCrypto);
        Assert.Equal(30m, site.PriceLinkInsert);
        Assert.Equal("Finance", site.Niche);
        Assert.Equal("Magazine", site.Categories);

        var log = await GetSitesUpdateImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(1, log!.Duplicates);
        Assert.Equal(1, log.Matched);
    }

    [Fact]
    public async Task ImportAsync_InvalidRow_AddsError_AndDoesNotOverwriteValidExistingParsedUpdate()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,60,5000,US,15,,,,,\n" +
            "existing.com,invalid,5000,US,15,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(3, result.Errors[0].RowNumber);
        Assert.Contains("DR", result.Errors[0].Message);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal(60, site.DR);
        Assert.Equal(5000, site.Traffic);
        Assert.Equal(15m, site.PriceUsd);
    }

    [Fact]
    public async Task ImportAsync_CompletelyEmptyRows_AreIgnored()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "\n" +
            "existing.com,50,1000,US,10,,,,,\n" +
            ",,,,,,,,,\n" +
            "\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Errors);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal(50, site.DR);
    }

    [Fact]
    public async Task ImportAsync_LargeFile_MultiChunkMatching_Works()
    {
        var batchSize = 1000;
        var totalSites = batchSize * 2 + 137;
        for (var i = 0; i < totalSites; i++)
        {
            _context.Sites.Add(new Site
            {
                Domain = $"chunk-{i}.com",
                DR = 1,
                Traffic = 1,
                Location = "US",
                PriceUsd = 1,
                IsQuarantined = false,
                CreatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        await _context.SaveChangesAsync();

        var sb = new StringBuilder();
        sb.Append(HeaderLine());
        for (var i = 0; i < totalSites; i++)
        {
            sb.Append($"chunk-{i}.com,55,12000,US,100,50,60,70,Tech,News\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        var result = await ImportAsync(stream);

        Assert.Equal(totalSites, result.Matched);
        Assert.Equal(0, result.ErrorsCount);
        Assert.Empty(result.Unmatched);

        var first = await GetSiteAsync("chunk-0.com");
        Assert.Equal(55, first.DR);
        Assert.Equal(12000, first.Traffic);
        Assert.Equal(100m, first.PriceUsd);
        Assert.Equal(50m, first.PriceCasino);

        var last = await GetSiteAsync($"chunk-{totalSites - 1}.com");
        Assert.Equal(55, last.DR);
        Assert.Equal(12000, last.Traffic);

        var log = await GetSitesUpdateImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(totalSites, log!.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
    }

    [Fact]
    public async Task ImportAsync_WritesImportLog()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,,,\n");

        await ImportAsync(stream);

        var log = await GetSitesUpdateImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(UserId, log!.UserId);
        Assert.Equal(UserEmail, log.UserEmail);
        Assert.Equal(ImportConstants.ImportTypeSitesUpdate, log.Type);
        Assert.Equal(1, log.Matched);
        Assert.Equal(0, log.Unmatched);
        Assert.Equal(0, log.ErrorsCount);
        Assert.Equal(0, log.Inserted);
        Assert.Equal(0, log.Duplicates);
    }

    [Fact]
    public async Task ImportAsync_DomainIsLookupOnly_NeverUpdated()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,,,\n");

        await ImportAsync(stream);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal("existing.com", site.Domain);
    }

    [Fact]
    public async Task ImportAsync_DomainNormalization_MatchesExistingSite()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "https://www.Existing.COM/,60,8000,UK,90,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.Matched);
        Assert.Empty(result.Unmatched);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal("existing.com", site.Domain);
        Assert.Equal(60, site.DR);
        Assert.Equal(8000, site.Traffic);
        Assert.Equal("UK", site.Location);
        Assert.Equal(90m, site.PriceUsd);
    }

    [Theory]
    [InlineData(",55,12000,US,100,150,200,250,Tech,News", "Domain is required.")]
    [InlineData("bad-dr.com,,12000,US,100,150,200,250,Tech,News", "DR is required")]
    [InlineData("bad-dr.com,abc,12000,US,100,150,200,250,Tech,News", "Invalid numeric format for DR")]
    [InlineData("bad-dr.com,101,12000,US,100,150,200,250,Tech,News", "DR must be between 0 and 100")]
    [InlineData("bad-traffic.com,55,,US,100,150,200,250,Tech,News", "Traffic is required")]
    [InlineData("bad-price.com,55,12000,US,,150,200,250,Tech,News", "Price USD is required")]
    [InlineData("bad-casino.com,55,12000,US,100,-1,200,250,Tech,News", "PriceCasino must be >= 0")]
    public async Task ImportAsync_InvalidRow_AddsError(string csvRow, string expectedMessageFragment)
    {
        using var stream = Utf8Csv(HeaderLine() + csvRow + "\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.Matched);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Contains(expectedMessageFragment, result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
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

    private async Task<ImportLog?> GetSitesUpdateImportLogAsync()
    {
        return await _context.ImportLogs
            .SingleOrDefaultAsync(x => x.Type == ImportConstants.ImportTypeSitesUpdate);
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

        _context.Sites.AddRange(
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
            },
            new Site
            {
                Domain = "second.com",
                DR = 30,
                Traffic = 3000,
                Location = "UK",
                PriceUsd = 40m,
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        _context.SaveChanges();
    }
}
