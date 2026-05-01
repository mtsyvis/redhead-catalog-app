using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

public sealed class SitesImportServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string UserEmail = "admin@test.com";
    private const string CsvFileName = "sites.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly ImportArtifactStorageService _artifactStorageService;
    private readonly SitesImportService _sut;

    public SitesImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)) // to ignore warnings about transactions not being supported in InMemory provider
            .Options;

        _context = new ApplicationDbContext(options);
        _artifactStorageService = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));
        _sut = new SitesImportService(_context, NullLogger<SitesImportService>.Instance, _artifactStorageService);

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
            "newsite.com,55,12000,US,100,150,200,250,175,225,Tech,News,3,Sponsored,2 years\n" +
            "secondsite.com,42,8000,UK,80,,,,,,Business,,,,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(2, result.InsertedCount);
        Assert.Equal(0, result.SkippedExistingCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(0, result.DuplicateDomainsCount);
        Assert.Empty(result.DuplicateDomainsPreview);
        Assert.NotNull(result.Downloads);
        Assert.Null(result.Downloads!.InvalidRows);

        var newSite = await GetSiteAsync("newsite.com");
        Assert.Equal(55, newSite.DR);
        Assert.Equal(12000, newSite.Traffic);
        Assert.Equal("US", newSite.Location);
        Assert.Equal(100m, newSite.PriceUsd);
        Assert.Equal(150m, newSite.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, newSite.PriceCasinoStatus);
        Assert.Equal(200m, newSite.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Available, newSite.PriceCryptoStatus);
        Assert.Equal(250m, newSite.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Available, newSite.PriceLinkInsertStatus);
        Assert.Equal(175m, newSite.PriceLinkInsertCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, newSite.PriceLinkInsertCasinoStatus);
        Assert.Equal(225m, newSite.PriceDating);
        Assert.Equal(ServiceAvailabilityStatus.Available, newSite.PriceDatingStatus);
        Assert.Equal(3, newSite.NumberDFLinks);
        Assert.Equal(TermType.Finite, newSite.TermType);
        Assert.Equal(2, newSite.TermValue);
        Assert.Equal(TermUnit.Year, newSite.TermUnit);
        Assert.Equal("Tech", newSite.Niche);
        Assert.Equal("News", newSite.Categories);
        Assert.Equal("Sponsored", newSite.SponsoredTag);
        Assert.False(newSite.IsQuarantined);

        var secondSite = await GetSiteAsync("secondsite.com");
        Assert.Equal(42, secondSite.DR);
        Assert.Equal(8000, secondSite.Traffic);
        Assert.Equal("UK", secondSite.Location);
        Assert.Equal(80m, secondSite.PriceUsd);
        Assert.Null(secondSite.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, secondSite.PriceCasinoStatus);
        Assert.Null(secondSite.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, secondSite.PriceCryptoStatus);
        Assert.Null(secondSite.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, secondSite.PriceLinkInsertStatus);
        Assert.Null(secondSite.PriceLinkInsertCasino);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, secondSite.PriceLinkInsertCasinoStatus);
        Assert.Null(secondSite.PriceDating);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, secondSite.PriceDatingStatus);
        Assert.Null(secondSite.NumberDFLinks);
        Assert.Null(secondSite.TermType);
        Assert.Null(secondSite.TermValue);
        Assert.Null(secondSite.TermUnit);
        Assert.Equal("Business", secondSite.Niche);
        Assert.Null(secondSite.Categories);
        Assert.Null(secondSite.SponsoredTag);

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
            "Domain;DR;Traffic;Location;PriceUsd;PriceCasino;PriceCrypto;PriceLinkInsert;PriceLinkInsertCasino;PriceDating;Niche;Categories;NumberDFLinks;SponsoredTag;Term\n" +
            "semicolon.com;61;15000;DE;90;120;;;;;Finance;Blog;;;\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("semicolon.com");
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
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "bomsite.com,50,1000,US,10,,,,,,,,,,\n",
            withBom: true);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("bomsite.com");
        Assert.Equal(50, site.DR);
    }

    [Fact]
    public async Task ImportAsync_InvalidHeaderOrder_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(
            "DR,Domain,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating,Niche,Categories,NumberDFLinks,SponsoredTag,Term\n" +
            "55,newsite.com,12000,US,100,150,200,250,,,Tech,News,,,\n");

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
            "Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating,Niche,NumberDFLinks,SponsoredTag,Term\n" +
            "newsite.com,55,12000,US,100,150,200,250,,,Tech,,,,\n");

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
            "newsite.com,55,12000,US,100,150,200,250,,,Tech,News,,,\n");

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
            "existing.com,55,12000,US,100,150,200,250,,,Tech,News,,,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.SkippedExistingCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(0, result.DuplicateDomainsCount);
        Assert.Empty(result.DuplicateDomainsPreview);

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
            "duplicate.com,10,1000,US,10,,,,,,,,,,\n" +
            "duplicate.com,75,25000,CA,300,10,20,30,,,Finance,Magazine,,,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("duplicate.com", result.DuplicateDomainsPreview[0]);

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
            "mixed-dup.com,invalid,1000,US,10,,,,,,,,,,\n" +
            "mixed-dup.com,60,5000,US,15,,,,,,,,,,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("mixed-dup.com", result.DuplicateDomainsPreview[0]);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("2,DR is required.", StringComparison.Ordinal));

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
            "ignored-empty.com,50,1000,US,10,,,,,,,,,,\n" +
            ",,,,,,,,,,,,,,\n" +
            "\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("ignored-empty.com");
        Assert.Equal(50, site.DR);
    }

    [Theory]
    [InlineData(",55,12000,US,100,150,200,250,,,Tech,News,,,,", "Domain is required.")]
    [InlineData("bad-dr.com,,12000,US,100,150,200,250,,,Tech,News,,,,", "DR is required.")]
    [InlineData("bad-dr-format.com,abc,12000,US,100,150,200,250,,,Tech,News,,,,", "DR is required.")]
    [InlineData("bad-dr-range.com,101,12000,US,100,150,200,250,,,Tech,News,,,,", "DR must be between 0 and 100.")]
    [InlineData("bad-traffic.com,55,,US,100,150,200,250,,,Tech,News,,,,", "Traffic is required.")]
    [InlineData("bad-location.com,55,12000, ,100,150,200,250,,,Tech,News,,,,", "Location is required.")]
    [InlineData("bad-price.com,55,12000,US,,,,,,,Tech,News,,,,", "At least one numeric price")]
    [InlineData("bad-casino.com,55,12000,US,100,-1,200,250,,,Tech,News,,,,", "Price must be >= 0.")]
    [InlineData("bad-crypto.com,55,12000,US,100,150,-1,250,,,Tech,News,,,,", "Price must be >= 0.")]
    [InlineData("bad-link-insert.com,55,12000,US,100,150,200,-1,,,Tech,News,,,,", "Price must be >= 0.")]
    [InlineData("bad-link-insert-casino.com,55,12000,US,100,150,200,250,-1,,Tech,News,,,,", "Price must be >= 0.")]
    [InlineData("bad-dating.com,55,12000,US,100,150,200,250,,-1,Tech,News,,,,", "Price must be >= 0.")]
    [InlineData("bad-df-zero.com,55,12000,US,100,150,200,250,,,Tech,News,0,Sponsored,", "Number DF Links must be greater than 0.")]
    [InlineData("bad-df-format.com,55,12000,US,100,150,200,250,,,Tech,News,abc,Sponsored,", "Invalid NumberDFLinks value.")]
    [InlineData("bad-term-main.com,55,12000,US,100,150,200,250,,,Tech,News,,Sponsored,1 month", "Invalid Term value.")]
    public async Task ImportAsync_InvalidRow_AddsError_AndSkipsRow(string csvRow, string expectedMessage)
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine() + csvRow + "\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads.InvalidRows);
        Assert.True(result.Downloads.InvalidRows.Available);
        Assert.NotEmpty(result.Downloads.InvalidRows.Token);
        Assert.EndsWith(".csv", result.Downloads.InvalidRows.FileName);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows.Token);
        Assert.Contains(invalidLines, line => line.Contains($",2,{expectedMessage}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_MultipleSharedValidationErrors_AreJoinedWithSemicolon()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "multi-errors.com,,,,,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
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
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            ",55,12000,US,100,150,200,250,,,Tech,News,,,\n");

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

        Assert.Equal("Domain,DR,Traffic,Location,PriceUsd,PriceCasino,PriceCrypto,PriceLinkInsert,PriceLinkInsertCasino,PriceDating,Niche,Categories,NumberDFLinks,SponsoredTag,Term,Source Row Number,Error Details", lines[0]);
        Assert.Equal(",55,12000,US,100,150,200,250,,,Tech,News,,,,2,Domain is required.", lines[1]);
    }

    [Fact]
    public async Task ImportAsync_NotAvailableMarkers_AreParsedToNotAvailableStatus()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "marker.com,55,12000,US,100,NO,n/a,-,NONE,NOT AVAILABLE,Tech,News,,Sponsored,permanent\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("marker.com");
        Assert.Null(site.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceCasinoStatus);
        Assert.Null(site.PriceCrypto);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceCryptoStatus);
        Assert.Null(site.PriceLinkInsert);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceLinkInsertStatus);
        Assert.Null(site.PriceLinkInsertCasino);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceLinkInsertCasinoStatus);
        Assert.Null(site.PriceDating);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, site.PriceDatingStatus);
        Assert.Equal(TermType.Permanent, site.TermType);
        Assert.Null(site.TermValue);
        Assert.Null(site.TermUnit);
    }

    [Fact]
    public async Task ImportAsync_InvalidOptionalServiceValue_ReturnsFieldError()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "bad-optional.com,55,12000,US,100,abc,,,,,Tech,News,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows.Token);
        Assert.Contains(invalidLines, line => line.Contains("abc", StringComparison.Ordinal));
        Assert.Contains(invalidLines, line => line.Contains("Invalid optional service value.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_InvalidCryptoOptionalServiceValue_ReturnsFieldError()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "bad-optional-crypto.com,55,12000,US,100,,abc,,,,Tech,News,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("abc", StringComparison.Ordinal));
        Assert.Contains(invalidLines, line => line.Contains("Invalid optional service value.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_InvalidLinkInsertOptionalServiceValue_ReturnsFieldError()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "bad-optional-link.com,55,12000,US,100,,,abc,,,Tech,News,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("abc", StringComparison.Ordinal));
        Assert.Contains(invalidLines, line => line.Contains("Invalid optional service value.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0", "Number DF Links must be greater than 0.")]
    [InlineData("-1", "Number DF Links must be greater than 0.")]
    [InlineData("1.5", "Invalid NumberDFLinks value.")]
    [InlineData("abc", "Invalid NumberDFLinks value.")]
    public async Task ImportAsync_InvalidNumberDFLinks_IsInvalidRow(string rawValue, string expectedMessage)
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            $"bad-df.com,55,12000,US,100,150,200,250,,,Tech,News,{rawValue},Sponsored,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("permanent")]
    [InlineData("Permanent")]
    [InlineData("PERMANENT")]
    [InlineData("1 year")]
    [InlineData("1 years")]
    [InlineData("2 year")]
    [InlineData("2 years")]
    [InlineData("10 years")]
    public async Task ImportAsync_ValidTermValues_AreAccepted(string rawTerm)
    {
        var domainSuffix = string.IsNullOrEmpty(rawTerm)
            ? "empty"
            : rawTerm.Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
        using var stream = Utf8Csv(
            HeaderLine() +
            $"term-valid-{domainSuffix}.com,55,12000,US,100,150,200,250,,,Tech,News,,Sponsored,{rawTerm}\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);
    }

    [Theory]
    [InlineData("0 year")]
    [InlineData("-1 year")]
    [InlineData("1.5 years")]
    [InlineData("year")]
    [InlineData("years")]
    [InlineData("1 month")]
    [InlineData("6 months")]
    [InlineData("30 days")]
    [InlineData("forever")]
    [InlineData("lifetime")]
    [InlineData("perm")]
    [InlineData("1y")]
    public async Task ImportAsync_InvalidTermValues_AreInvalidRows(string rawTerm)
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            $"bad-term.com,55,12000,US,100,150,200,250,,,Tech,News,,Sponsored,{rawTerm}\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("Invalid Term value.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_DomainIsNormalizedBeforeInsert()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            "https://www.MixedCaseSite.com/,55,12000,US,100,150,200,250,,,Tech,News,,,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        var site = await GetSiteAsync("mixedcasesite.com");
        Assert.Equal("mixedcasesite.com", site.Domain);
    }

    [Fact]
    public async Task ImportAsync_TooManyInvalidRows_ExportsAllInvalidRowsAndKeepsFullCount()
    {
        // Arrange
        var sb = new StringBuilder();
        sb.Append(HeaderLine());

        for (var i = 0; i < 250; i++)
        {
            sb.Append($",55,12000,US,100,150,200,250,,,Tech,News,,,\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(250, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
        var invalidLines = GetDownloadLines(result.Downloads.InvalidRows!.Token);
        Assert.Equal(251, invalidLines.Length);
    }

    [Fact]
    public async Task ImportAsync_TooManyDuplicateRows_KeepsUniquePreviewAndAccurateDuplicateLogCount()
    {
        // Arrange
        var sb = new StringBuilder();
        sb.Append(HeaderLine());

        for (var i = 0; i < 250; i++)
        {
            sb.Append("existing.com,55,12000,US,100,150,200,250,,,Tech,News,,,\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.SkippedExistingCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("existing.com", result.DuplicateDomainsPreview[0]);

        var log = await GetSitesImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(250, log!.Duplicates);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainsPreview_CountsUniqueDomains_IncludingInvalidRows_AndLimitsTo100()
    {
        var sb = new StringBuilder();
        sb.Append(HeaderLine());
        for (var i = 0; i < ImportConstants.DuplicateDomainsPreviewLimit + 1; i++)
        {
            sb.Append($"dupe-{i}.com,invalid,5000,US,10,,,,,,,,,,\n");
            sb.Append($"dupe-{i}.com,55,5000,US,10,,,,,,,,,,\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        var result = await ImportAsync(stream);

        Assert.Equal(101, result.InsertedCount);
        Assert.Equal(101, result.InvalidRowsCount);
        Assert.Equal(101, result.DuplicateDomainsCount);
        Assert.Equal(100, result.DuplicateDomainsPreview.Count);
        Assert.Equal("dupe-0.com", result.DuplicateDomainsPreview[0]);
        Assert.Equal("dupe-99.com", result.DuplicateDomainsPreview[99]);
        Assert.DoesNotContain("dupe-100.com", result.DuplicateDomainsPreview);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomain_WithInvalidAndValidRow_IsPreviewed_InvalidRowExported_AndLastValidInserted()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "mixed-dupe-new.com,invalid,5000,US,10,,,,,,,,,,\n" +
            "mixed-dupe-new.com,55,5000,US,10,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("mixed-dupe-new.com", result.DuplicateDomainsPreview[0]);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);

        var site = await GetSiteAsync("mixed-dupe-new.com");
        Assert.Equal(55, site.DR);
    }

    [Fact]
    public async Task ImportAsync_DuplicateExistingDomainInFile_ContributesToSkippedExisting_AndDuplicateDomainReporting()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "existing.com,40,5000,US,50,,,,,,,,,,\n" +
            "existing.com,60,8000,CA,90,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.SkippedExistingCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("existing.com", result.DuplicateDomainsPreview[0]);
        var log = await GetSitesImportLogAsync();
        Assert.NotNull(log);
        Assert.Equal(2, log!.Duplicates);
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomain_AllRowsInvalid_IsStillCounted_NoInsert_AndRowsExported()
    {
        using var stream = Utf8Csv(
            HeaderLine() +
            "all-invalid.com,invalid,5000,US,10,,,,,,,,,,\n" +
            "all-invalid.com,invalid,6000,US,20,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(2, result.InvalidRowsCount);
        Assert.Equal(2, result.InvalidRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Single(result.DuplicateDomainsPreview);
        Assert.Equal("all-invalid.com", result.DuplicateDomainsPreview[0]);
        Assert.NotNull(result.Downloads);
        Assert.NotNull(result.Downloads!.InvalidRows);
    }

    [Fact]
    public async Task ImportAsync_WhenNoValidRows_StillWritesImportLog()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine() +
            ",55,12000,US,100,150,200,250,,,Tech,News,,,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);

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
            sb.Append($"chunk-site-{i}.com,55,12000,US,100,150,200,250,,,Tech,News,,,\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(totalRows, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

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

            sb.Append($"{domain},55,12000,US,100,150,200,250,,,Tech,News,,,\n");
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(totalRows - existingDomains.Count, result.InsertedCount);
        Assert.Equal(existingDomains.Count, result.SkippedExistingCount);
        Assert.Equal(0, result.InvalidRowsCount);

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
    public async Task ImportAsync_EmptyPriceUsd_WithCasinoPrice_InsertsWithNullPriceUsd()
    {
        using var stream = Utf8Csv(HeaderLine() + "nullprice.com,55,12000,US,,150,,,,,Tech,News,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("nullprice.com");
        Assert.Null(site.PriceUsd);
        Assert.Equal(150m, site.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, site.PriceCasinoStatus);
    }

    [Fact]
    public async Task ImportAsync_EmptyPriceUsd_AllPricesAbsent_IsInvalidRow()
    {
        using var stream = Utf8Csv(HeaderLine() + "noprices.com,55,12000,US,,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.NotNull(result.Downloads?.InvalidRows);

        var lines = GetDownloadLines(result.Downloads.InvalidRows.Token);
        Assert.Contains(lines, l => l.Contains("At least one numeric price is required.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_AllOptionalServicesNotAvailable_NoPriceUsd_IsInvalidRow()
    {
        using var stream = Utf8Csv(HeaderLine() + "allno.com,55,12000,US,,NO,NO,NO,,,Tech,News,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
    }

    [Fact]
    public async Task ImportAsync_InvalidPriceUsdFormat_IsInvalidRow()
    {
        using var stream = Utf8Csv(HeaderLine() + "badformat.com,55,12000,US,abc,,,,,,,,,,\n");

        var result = await ImportAsync(stream);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);

        var lines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(lines, l => l.Contains("Invalid PriceUsd value.", StringComparison.Ordinal));
    }

    #endregion

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
            });

        _context.SaveChanges();
    }
}
