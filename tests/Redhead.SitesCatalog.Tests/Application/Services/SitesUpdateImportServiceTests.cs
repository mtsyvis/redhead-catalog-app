using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
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
    private readonly ImportArtifactStorageService _artifactStorageService;
    private readonly SitesUpdateImportService _sut;

    public SitesUpdateImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _artifactStorageService = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));
        _sut = new SitesUpdateImportService(_context, NullLogger<SitesUpdateImportService>.Instance, _artifactStorageService);

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
            "existing.com,55,12000,US,100,150,200,250,,,Tech,News,,Sponsored,\n" +
            "second.com,42,8000,UK,80,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(0, result.UnmatchedRowsCount);
        Assert.Equal(0, result.DuplicateDomainsCount);
        Assert.Empty(result.DuplicateDomainsPreview);
        Assert.NotNull(result.Downloads);
        Assert.Null(result.Downloads!.InvalidRows);
        Assert.Null(result.Downloads.UnmatchedRows);

        var first = await GetSiteAsync("existing.com");
        Assert.Equal("existing.com", first.Domain);
        Assert.Equal(55, first.DR);
        Assert.Equal(12000, first.Traffic);
        Assert.Equal("US", first.Location);
        Assert.Equal(100m, first.PriceUsd);
        Assert.Equal(150m, first.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, first.PriceCasinoStatus);
        Assert.Equal(200m, first.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Available, first.PriceCryptoStatus);
        Assert.Equal(250m, first.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Available, first.PriceLinkInsertStatus);
        Assert.Equal("Tech", first.Niche);
        Assert.Equal("News", first.Categories);
        Assert.Equal("Sponsored", first.SponsoredTag);

        var second = await GetSiteAsync("second.com");
        Assert.Equal(42, second.DR);
        Assert.Equal(8000, second.Traffic);
        Assert.Equal("UK", second.Location);
        Assert.Equal(80m, second.PriceUsd);
        Assert.Null(second.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, second.PriceCasinoStatus);
        Assert.Null(second.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, second.PriceCryptoStatus);
        Assert.Null(second.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, second.PriceLinkInsertStatus);
        Assert.Null(second.Niche);
        Assert.Null(second.Categories);
        Assert.Null(second.SponsoredTag);

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
            "Domain;DR;Traffic;Location;PriceUsd;PriceCasino;PriceCrypto;PriceLinkInsert;PriceLinkInsertCasino;PriceDating;Niche;Categories;NumberDFLinks;SponsoredTag;Term\n" +
            "existing.com;61;15000;DE;90;120;;;;;Finance;Blog;;;\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal(61, site.DR);
        Assert.Equal(15000, site.Traffic);
        Assert.Equal("DE", site.Location);
        Assert.Equal(90m, site.PriceUsd);
        Assert.Equal(120m, site.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, site.PriceCasinoStatus);
        Assert.Null(site.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, site.PriceCryptoStatus);
        Assert.Null(site.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, site.PriceLinkInsertStatus);
        Assert.Equal("Finance", site.Niche);
        Assert.Equal("Blog", site.Categories);
    }

    [Fact]
    public async Task ImportAsync_Utf8Bom_IsAccepted()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,50,1000,US,10,,,,,,,,,,\n",
            withBom: true);

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal(50, site.DR);
    }

    [Fact]
    public async Task ImportAsync_InvalidHeaderOrder_ThrowsImportHeaderValidationException()
    {
        using var stream = Utf8Csv(
            "DR,Domain,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating,Niche,Categories,NumberDFLinks,SponsoredTag,Term\n" +
            "55,existing.com,12000,US,100,150,200,250,,,Tech,News,,,\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.StartsWith("CSV header is invalid.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_MissingRequiredHeader_ThrowsImportHeaderValidationException()
    {
        using var stream = Utf8Csv(
            "Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating,Niche,NumberDFLinks,SponsoredTag,Term\n" +
            "existing.com,55,12000,US,100,150,200,250,,,Tech,,,,\n");

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
            "existing.com,55,12000,US,100,150,200,250,,,Tech,News,,,\n");

        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        Assert.Equal("CSV must be UTF-8 encoded.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedDomain_IsReported()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,,,,,,,,\n" +
            "missing-site.com,42,5000,UK,50,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.UnmatchedRows);

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
        site.SponsoredTag = "OldSponsoredTag";
        await _context.SaveChangesAsync();

        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var updated = await GetSiteAsync("existing.com");
        Assert.Equal(55, updated.DR);
        Assert.Equal(100m, updated.PriceUsd);
        Assert.Null(updated.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, updated.PriceCasinoStatus);
        Assert.Null(updated.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, updated.PriceCryptoStatus);
        Assert.Null(updated.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, updated.PriceLinkInsertStatus);
        Assert.Null(updated.Niche);
        Assert.Null(updated.Categories);
        Assert.Null(updated.SponsoredTag);
    }

    [Fact]
    public async Task ImportAsync_SponsoredTag_IsTrimmed_AndEmptyBecomesNull()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,,,,,,,   ,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var updated = await GetSiteAsync("existing.com");
        Assert.Null(updated.SponsoredTag);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainsInFile_ReturnsDuplicates_AndLastValidRowWins()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,10,1000,US,10,,,,,,,,,,\n" +
            "existing.com,75,25000,CA,300,10,20,30,,,Finance,Magazine,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("existing.com", result.DuplicateDomainsPreview[0]);

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
            "existing.com,60,5000,US,15,,,,,,,,,,\n" +
            "existing.com,invalid,5000,US,15,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("3,DR is required.", StringComparison.Ordinal));

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
            "existing.com,50,1000,US,10,,,,,,,,,,\n" +
            ",,,,,,,,,,,,,,\n" +
            "\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

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
            sb.Append($"chunk-{i}.com,55,12000,US,100,50,60,70,,,Tech,News,,,\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        var result = await ImportAsync(stream);

        Assert.Equal(totalSites, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

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
            "existing.com,55,12000,US,100,,,,,,,,,,\n");

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
            "existing.com,55,12000,US,100,,,,,,,,,,\n");

        await ImportAsync(stream);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal("existing.com", site.Domain);
    }

    [Fact]
    public async Task ImportAsync_DomainNormalization_MatchesExistingSite()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "https://www.Existing.COM/,60,8000,UK,90,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.UnmatchedRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal("existing.com", site.Domain);
        Assert.Equal(60, site.DR);
        Assert.Equal(8000, site.Traffic);
        Assert.Equal("UK", site.Location);
        Assert.Equal(90m, site.PriceUsd);
    }

    [Theory]
    [InlineData(",55,12000,US,100,150,200,250,,,Tech,News,,,,", "Domain is required.")]
    [InlineData("bad-dr.com,,12000,US,100,150,200,250,,,Tech,News,,,,", "DR is required")]
    [InlineData("bad-dr.com,abc,12000,US,100,150,200,250,,,Tech,News,,,,", "DR is required")]
    [InlineData("bad-dr.com,101,12000,US,100,150,200,250,,,Tech,News,,,,", "DR must be between 0 and 100")]
    [InlineData("bad-traffic.com,55,,US,100,150,200,250,,,Tech,News,,,,", "Traffic is required")]
    [InlineData("bad-location.com,55,12000, ,100,150,200,250,,,Tech,News,,,,", "Location is required")]
    [InlineData("bad-price.com,55,12000,US,,,,,,,Tech,News,,,,", "At least one numeric price")]
    [InlineData("bad-casino.com,55,12000,US,100,-1,200,250,,,Tech,News,,,,", "Price must be >= 0")]
    [InlineData("bad-link-insert-casino.com,55,12000,US,100,150,200,250,-1,,Tech,News,,,,", "Price must be >= 0.")]
    [InlineData("bad-dating.com,55,12000,US,100,150,200,250,,-1,Tech,News,,,,", "Price must be >= 0.")]
    [InlineData("bad-df-zero.com,55,12000,US,100,150,200,250,,,Tech,News,0,Sponsored,", "Number DF Links must be greater than 0.")]
    [InlineData("bad-df-format.com,55,12000,US,100,150,200,250,,,Tech,News,abc,Sponsored,", "Invalid NumberDFLinks value.")]
    [InlineData("bad-term-main.com,55,12000,US,100,150,200,250,,,Tech,News,,Sponsored,1 month", "Invalid Term value.")]
    public async Task ImportAsync_InvalidRow_AddsError(string csvRow, string expectedMessageFragment)
    {
        using var stream = Utf8Csv(HeaderLine() + csvRow + "\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows!.Token);
        Assert.Contains(
            invalidLines,
            line => line.Contains(",2,", StringComparison.Ordinal)
                    && line.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_MultipleSharedValidationErrors_AreJoinedWithSemicolon()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,,,,,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);

        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows.Token);
        var invalidRowLine = Assert.Single(invalidLines, line => line.Contains(",2,", StringComparison.Ordinal));
        Assert.Contains("DR is required.", invalidRowLine, StringComparison.Ordinal);
        Assert.Contains("Traffic is required.", invalidRowLine, StringComparison.Ordinal);
        Assert.Contains("Location is required.", invalidRowLine, StringComparison.Ordinal);
        Assert.Contains("At least one numeric price", invalidRowLine, StringComparison.Ordinal);
        Assert.Contains("; ", invalidRowLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_InvalidRow_InvalidRowsDownloadContainsSourceRowNumber()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            ",55,12000,US,100,150,200,250,,,Tech,News,,,\n");

        var result = await ImportAsync(stream);

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

        Assert.Equal("Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating,Niche,Categories,NumberDFLinks,SponsoredTag,Term,Source Row Number,Error Details", lines[0]);
        Assert.Equal(",55,12000,US,100,150,200,250,,,Tech,News,,,,2,Domain is required.", lines[1]);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedRows_UnmatchedRowsDownloadContainsOriginalHeadersAndSourceRowNumber()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "missing-site.com,42,5000,UK,50,,,,,,,,,,\n" +
            "existing.com,55,12000,US,100,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.UnmatchedRows);
        Assert.Null(result.Downloads.InvalidRows);

        var token = result.Downloads.UnmatchedRows!.Token;
        var download = _artifactStorageService.GetCsvDownload(token);
        Assert.NotNull(download);

        var csv = Encoding.UTF8.GetString(download!.Content);
        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd('\r'))
            .ToArray();

        Assert.Equal("Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating,Niche,Categories,NumberDFLinks,SponsoredTag,Term,Source Row Number", lines[0]);
        Assert.Equal("missing-site.com,42,5000,UK,50,,,,,,,,,,,2", lines[1]);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainsPreview_CountsUniqueDomains_IncludingInvalidRows_AndLimitsTo100()
    {
        var sb = new StringBuilder();
        sb.Append(HeaderLine());
        for (var i = 0; i < 101; i++)
        {
            sb.Append($"dupe-{i}.com,invalid,5000,US,10,,,,,,,,,,\n");
            sb.Append($"dupe-{i}.com,55,5000,US,10,,,,,,,,,,\n");
        }
        sb.Append("existing.com,60,9000,CA,20,,,,,,,,,,\n");

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
    public async Task ImportAsync_DuplicateDomain_WithInvalidAndValidRow_IsInDuplicatePreview_InvalidRowRemainsSeparate()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "missing-site.com,invalid,5000,US,10,,,,,,,,,,\n" +
            "missing-site.com,55,5000,US,10,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("missing-site.com", result.DuplicateDomainsPreview[0]);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        Assert.NotNull(result.Downloads.UnmatchedRows);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomain_AllRowsInvalid_IsCountedAndExported()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "all-invalid-update.com,invalid,5000,US,10,,,,,,,,,,\n" +
            "all-invalid-update.com,invalid,6000,US,20,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(2, result.InvalidRowsCount);
        Assert.Equal(0, result.UnmatchedRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("all-invalid-update.com", result.DuplicateDomainsPreview[0]);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        Assert.Null(result.Downloads.UnmatchedRows);
    }

    [Fact]
    public async Task ImportAsync_NoMarker_OverPricedRow_SetsNotAvailable()
    {
        var site = await GetSiteAsync("existing.com");
        site.PriceCasino = 111m;
        site.PriceCasinoStatus = ServiceAvailabilityStatus.Available;
        await _context.SaveChangesAsync();

        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,NO,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        var updated = await GetSiteAsync("existing.com");
        Assert.Null(updated.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, updated.PriceCasinoStatus);
    }

    [Fact]
    public async Task ImportAsync_EmptyOptionalValue_OverPricedRow_SetsUnknown()
    {
        var site = await GetSiteAsync("existing.com");
        site.PriceCasino = 222m;
        site.PriceCasinoStatus = ServiceAvailabilityStatus.Available;
        await _context.SaveChangesAsync();

        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,, ,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        var updated = await GetSiteAsync("existing.com");
        Assert.Null(updated.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, updated.PriceCasinoStatus);
    }

    [Fact]
    public async Task ImportAsync_NumericOptionalValue_OverUnknownRow_SetsAvailable()
    {
        var site = await GetSiteAsync("existing.com");
        site.PriceLinkInsert = null;
        site.PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown;
        await _context.SaveChangesAsync();

        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,,,55,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        var updated = await GetSiteAsync("existing.com");
        Assert.Equal(55m, updated.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Available, updated.PriceLinkInsertStatus);
    }

    [Fact]
    public async Task ImportAsync_NewFields_UpdatesValues()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,55,12000,US,100,150,200,250,175,225,Tech,News,3,Sponsored,2 years\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var updated = await GetSiteAsync("existing.com");
        Assert.Equal(175m, updated.PriceLinkInsertCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, updated.PriceLinkInsertCasinoStatus);
        Assert.Equal(225m, updated.PriceDating);
        Assert.Equal(ServiceAvailabilityStatus.Available, updated.PriceDatingStatus);
        Assert.Equal(3, updated.NumberDFLinks);
        Assert.Equal(TermType.Finite, updated.TermType);
        Assert.Equal(2, updated.TermValue);
        Assert.Equal(TermUnit.Year, updated.TermUnit);
    }

    [Theory]
    [InlineData("0", "Number DF Links must be greater than 0.")]
    [InlineData("1.5", "Invalid NumberDFLinks value.")]
    public async Task ImportAsync_InvalidNumberDFLinks_IsInvalidRow(string rawValue, string expectedMessage)
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            $"existing.com,55,12000,US,100,150,200,250,,,Tech,News,{rawValue},Sponsored,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("permanent")]
    [InlineData("10 years")]
    public async Task ImportAsync_ValidTermValues_AreAccepted(string rawTerm)
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            $"existing.com,55,12000,US,100,150,200,250,,,Tech,News,,Sponsored,{rawTerm}\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
    }

    [Theory]
    [InlineData("0 year")]
    [InlineData("1 month")]
    [InlineData("1y")]
    public async Task ImportAsync_InvalidTermValues_AreInvalidRows(string rawTerm)
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            $"existing.com,55,12000,US,100,150,200,250,,,Tech,News,,Sponsored,{rawTerm}\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("Invalid Term value.", StringComparison.Ordinal));
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

    private static string HeaderLine()
    {
        return string.Join(",", ImportConstants.SitesImportRequiredColumnOrder) + "\n";
    }

    #region PriceUsd nullable

    [Fact]
    public async Task ImportAsync_EmptyPriceUsd_WithCasinoPrice_ClearsPriceUsdToNull()
    {
        // "existing.com" is seeded with PriceUsd = 50m; import with empty PriceUsd + casino = 150
        using var stream = Utf8Csv(HeaderLine() + "existing.com,55,12000,US,,150,,,,,Tech,News,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Null(site.PriceUsd);
        Assert.Equal(150m, site.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, site.PriceCasinoStatus);
    }

    [Fact]
    public async Task ImportAsync_EmptyPriceUsd_AllPricesAbsent_IsInvalidRow_SiteNotUpdated()
    {
        // "existing.com" is seeded with PriceUsd = 50m; import with all prices absent → rejected
        using var stream = Utf8Csv(HeaderLine() + "existing.com,55,12000,US,,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal(50m, site.PriceUsd);
    }

    #endregion

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
                PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
                PriceCrypto = null,
                PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
                PriceLinkInsert = null,
                PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
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
