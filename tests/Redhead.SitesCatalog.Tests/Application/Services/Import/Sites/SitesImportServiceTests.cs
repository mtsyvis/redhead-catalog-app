using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Services.Import.Artifacts;
using Redhead.SitesCatalog.Application.Services.Import.Sites;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Tests.Application.Services.Import.Sites;

public sealed class SitesImportServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string UserEmail = "admin@test.com";
    private const string CsvFileName = "sites.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly MemoryCache _nicheOptionsMemoryCache;
    private readonly ImportArtifactStorageService _artifactStorageService;
    private readonly SitesImportService _sut;

    public SitesImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _nicheOptionsMemoryCache = new MemoryCache(new MemoryCacheOptions());
        _artifactStorageService = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));
        _sut = new SitesImportService(
            _context,
            NullLogger<SitesImportService>.Instance,
            _artifactStorageService,
            new NicheFilterOptionsCache(_context, _nicheOptionsMemoryCache),
            new LocationNormalizer());

        SeedSites();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _nicheOptionsMemoryCache.Dispose();
    }

    [Fact]
    public async Task ImportAsync_WithMainTermPrices_CreatesSitePriceOptionsOnly()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceUsd [1 year]", "PriceUsd [permanent]") +
            Row("main-prices.com", "55", "12000", "US", "Tech", "News", "3", "Sponsored", "EN", "100", "300"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("main-prices.com");
        Assert.Null(site.PriceUsd);
        Assert.Null(site.TermType);
        Assert.Null(site.TermValue);
        Assert.Null(site.TermUnit);

        Assert.DoesNotContain(site.ServiceAvailabilities, availability => availability.ServiceType == PriceType.Main);
        AssertPrice(site, PriceType.Main, "finite:1:year", TermType.Finite, 1, TermUnit.Year, 100m);
        AssertPrice(site, PriceType.Main, "permanent", TermType.Permanent, null, null, 300m);
    }

    [Fact]
    public async Task ImportAsync_WithCasinoAvailabilityYes_CreatesUnknownPriceAvailability()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceCasinoAvailability") +
            Row("casino-yes.com", "55", "12000", "US", "Gaming", "News", "3", "Sponsored", "EN", "YES"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("casino-yes.com");
        Assert.DoesNotContain(site.PriceOptions, price => price.PriceType == PriceType.Casino);
        AssertAvailability(site, PriceType.Casino, ServiceAvailabilityStatus.AvailableWithUnknownPrice);
    }

    [Fact]
    public async Task ImportAsync_WithCasinoPrice_CreatesPriceAndAvailableStatus()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceCasinoAvailability", "PriceCasino [1 year]") +
            Row("casino-price.com", "55", "12000", "US", "Gaming", "News", "3", "Sponsored", "EN", "", "250"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("casino-price.com");
        AssertPrice(site, PriceType.Casino, "finite:1:year", TermType.Finite, 1, TermUnit.Year, 250m);
        AssertAvailability(site, PriceType.Casino, ServiceAvailabilityStatus.Available);
    }

    [Fact]
    public async Task ImportAsync_WithUnknownTermLinkInsertPrices_CreatesUnknownTermOptionsAndAvailableStatuses()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceLinkInsert [unknown term]", "PriceLinkInsertCasino [unknown term]") +
            Row("link-prices.com", "55", "12000", "US", "Business", "News", "3", "Sponsored", "EN", "100", "150"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("link-prices.com");
        AssertPrice(site, PriceType.LinkInsertion, "unknown", null, null, null, 100m);
        AssertPrice(site, PriceType.LinkInsertionCasino, "unknown", null, null, null, 150m);
        AssertAvailability(site, PriceType.LinkInsertion, ServiceAvailabilityStatus.Available);
        AssertAvailability(site, PriceType.LinkInsertionCasino, ServiceAvailabilityStatus.Available);
    }


    [Theory]
    [InlineData("PriceUsd [1 month]")]
    [InlineData("PriceUsd []")]
    [InlineData("PriceCasino [0 years]")]
    [InlineData("PriceCasino [-1 year]")]
    public async Task ImportAsync_WithInvalidTermHeader_ThrowsImportHeaderValidationException(string header)
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine(header) + Row("bad-term-header.com"));

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("Invalid term header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WithDuplicatePriceTypeAndTermHeader_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceCasino [permanent]", "PriceCasino [Permanent]") +
            Row("duplicate-price-header.com", extras: ["100", "200"]));

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("Duplicate price column", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WithMainAvailabilityHeader_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine("PriceUsdAvailability") + Row("main-availability.com"));

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("Main pricing must not include an availability column", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PriceCasinoAvailability", "NO", "PriceCasino [1 year]", "250", "cannot be YES or NO")]
    [InlineData("PriceCasinoAvailability", "YES", "PriceCasino [1 year]", "250", "cannot be YES or NO")]
    [InlineData("PriceUsd [1 year]", "0", null, null, "must be greater than 0")]
    [InlineData("PriceUsd [1 year]", "YES", null, null, "Invalid PriceUsd [1 year] value.")]
    [InlineData("PriceCasinoAvailability", "MAYBE", null, null, "must be empty, YES, or NO")]
    public async Task ImportAsync_WithInvalidPricingRow_AddsInvalidRow(
        string firstHeader,
        string firstValue,
        string? secondHeader,
        string? secondValue,
        string expectedMessage)
    {
        // Arrange
        var headers = secondHeader is null
            ? [firstHeader]
            : new[] { firstHeader, secondHeader };
        var values = secondValue is null
            ? [firstValue]
            : new[] { firstValue, secondValue };
        using var stream = Utf8Csv(HeaderLine(headers) + Row("bad-pricing-row.com", extras: values));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_MissingRequiredBaseHeader_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv("Domain,DR,Traffic,Niche,Categories,NumberDFLinks,SponsoredTag\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.StartsWith("CSV header is invalid.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_WithOnlyRequiredBaseHeaders_InsertsSite()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,DR,Traffic,Location\n" +
            "minimal.com,55,12000,US\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("minimal.com");
        Assert.Equal(55, site.DR);
        Assert.Equal(12000, site.Traffic);
        Assert.Equal("US", site.Location);
        Assert.Null(site.Niche);
        Assert.Null(site.Categories);
        Assert.Null(site.NumberDFLinks);
        Assert.Null(site.SponsoredTag);
        Assert.Null(site.Language);
    }

    [Fact]
    public async Task ImportAsync_InvalidRequiredBaseValue_AddsInvalidRow()
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine() + Row("bad-dr.com", dr: "abc"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("DR is required.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainAlreadyExistsInDatabase_IsReportedAsDuplicateAndNotInserted()
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine("PriceUsd [1 year]") + Row("existing.com", extras: ["100"]));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.SkippedExistingCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, await _context.Sites.CountAsync(site => site.Domain == "existing.com"));
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomainsInFile_UsesLastValidRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceUsd [1 year]") +
            Row("duplicate.com", dr: "10", extras: ["100"]) +
            Row("duplicate.com", dr: "75", extras: ["300"]));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Equal("duplicate.com", Assert.Single(result.DuplicateDomainsPreview));

        var site = await GetSiteAsync("duplicate.com");
        Assert.Equal(75, site.DR);
        AssertPrice(site, PriceType.Main, "finite:1:year", TermType.Finite, 1, TermUnit.Year, 300m);
    }

    [Fact]
    public async Task ImportAsync_LastValidDuplicateRowWinsEvenIfEarlierDuplicateIsInvalid()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceUsd [1 year]") +
            Row("mixed-dup.com", dr: "invalid", extras: ["100"]) +
            Row("mixed-dup.com", dr: "60", extras: ["150"]));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(1, result.DuplicateDomainsCount);

        var site = await GetSiteAsync("mixed-dup.com");
        Assert.Equal(60, site.DR);
        AssertPrice(site, PriceType.Main, "finite:1:year", TermType.Finite, 1, TermUnit.Year, 150m);
    }

    [Fact]
    public async Task ImportAsync_UnmappedLocation_SavesOtherAndCreatesWarningRowsDownload()
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine() + Row("bad-location-warning.com", location: "United Stetes"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.SavedWithWarningsCount);
        Assert.NotNull(result.Downloads?.WarningRows);

        var site = await GetSiteAsync("bad-location-warning.com");
        Assert.Null(site.LocationKey);
        Assert.Equal("United Stetes", site.ImportedLocationRaw);

        var warningLines = GetDownloadLines(result.Downloads!.WarningRows!.Token);
        Assert.Equal("Domain,Field,Raw Value,Source Row Number,Warning", warningLines[0]);
        Assert.Equal("bad-location-warning.com,Location,United Stetes,2,Location was saved as Other because the value could not be mapped.", warningLines[1]);
    }

    [Fact]
    public async Task ImportAsync_InvalidRowWithUnmappedLocation_IsInvalidOnlyAndDoesNotCreateWarning()
    {
        // Arrange
        using var stream = Utf8Csv(HeaderLine() + Row("invalid-unmapped-location.com", dr: "invalid", location: "some trash"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Equal(0, result.SavedWithWarningsCount);
        Assert.Null(result.Downloads?.WarningRows);
    }

    [Fact]
    public async Task ImportAsync_EmptyPricingAndAvailabilityCells_DoNotCreateWarnings()
    {
        // Arrange
        using var stream = Utf8Csv(
            HeaderLine("PriceUsd [1 year]", "PriceCasinoAvailability") +
            Row("empty-pricing.com", extras: ["", ""]));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(0, result.SavedWithWarningsCount);
        Assert.Null(result.Downloads?.WarningRows);
    }

    [Fact]
    public async Task ImportAsync_SemicolonDelimitedFile_IsSupported()
    {
        // Arrange
        using var stream = Utf8Csv(
            string.Join(";", DefaultBaseHeaderColumns().Concat(["PriceUsd [1 year]"])) + "\n" +
            "semicolon.com;61;15000;DE;Finance;Blog;;;" + ";90\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("semicolon.com");
        Assert.Equal("Finance", site.Niche);
        Assert.Equal("Blog", site.Categories);
        AssertPrice(site, PriceType.Main, "finite:1:year", TermType.Finite, 1, TermUnit.Year, 90m);
    }

    [Fact]
    public async Task ImportAsync_WhenRowsCountExceedsBatchSize_InsertsAllSitesAcrossMultipleChunks()
    {
        // Arrange
        var totalRows = ImportConstants.SitesImportBatchSize + 17;
        var sb = new StringBuilder();
        sb.Append(HeaderLine("PriceUsd [1 year]"));
        for (var i = 0; i < totalRows; i++)
        {
            sb.Append(Row($"chunk-site-{i}.com", extras: ["100"]));
        }

        using var stream = Utf8Csv(sb.ToString());

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(totalRows, result.InsertedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(totalRows, await _context.Sites.CountAsync(site => site.Domain.StartsWith("chunk-site-")));

        var firstSite = await GetSiteAsync("chunk-site-0.com");
        AssertPrice(firstSite, PriceType.Main, "finite:1:year", TermType.Finite, 1, TermUnit.Year, 100m);
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
        return await _context.Sites
            .Include(site => site.PriceOptions)
            .Include(site => site.ServiceAvailabilities)
            .SingleAsync(site => site.Domain == domain);
    }

    private string[] GetDownloadLines(string token)
    {
        var download = _artifactStorageService.GetCsvDownload(token);
        Assert.NotNull(download);

        return Encoding.UTF8.GetString(download!.Content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
    }

    private static MemoryStream Utf8Csv(string text)
    {
        return new MemoryStream(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
    }

    private static string HeaderLine(params string[] extraHeaders)
    {
        return string.Join(",", DefaultBaseHeaderColumns().Concat(extraHeaders)) + "\n";
    }

    private static IEnumerable<string> DefaultBaseHeaderColumns()
        => ImportConstants.SitesImportRequiredColumns.Concat(ImportConstants.SitesImportOptionalColumns);

    private static string Row(
        string domain,
        string dr = "55",
        string traffic = "12000",
        string location = "US",
        string niche = "Tech",
        string categories = "News",
        string numberDFLinks = "3",
        string sponsoredTag = "Sponsored",
        string language = "EN",
        params string[] extras)
    {
        return string.Join(",", new[]
        {
            domain,
            dr,
            traffic,
            location,
            niche,
            categories,
            numberDFLinks,
            sponsoredTag,
            language
        }.Concat(extras)) + "\n";
    }

    private static void AssertPrice(
        Site site,
        PriceType priceType,
        string termKey,
        TermType? termType,
        int? termValue,
        TermUnit? termUnit,
        decimal amountUsd)
    {
        var price = Assert.Single(site.PriceOptions, option => option.PriceType == priceType && option.TermKey == termKey);
        Assert.Equal(termType, price.TermType);
        Assert.Equal(termValue, price.TermValue);
        Assert.Equal(termUnit, price.TermUnit);
        Assert.Equal(amountUsd, price.AmountUsd);
    }

    private static void AssertAvailability(
        Site site,
        PriceType serviceType,
        ServiceAvailabilityStatus status)
    {
        var availability = Assert.Single(site.ServiceAvailabilities, item => item.ServiceType == serviceType);
        Assert.Equal(status, availability.Status);
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
                Niche = "General",
                NicheTokens = NicheNormalizer.NormalizeTokens("General"),
                Categories = "Blog",
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        _context.SaveChanges();
    }
}
