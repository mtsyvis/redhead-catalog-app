using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Services.Import.Artifacts;
using Redhead.SitesCatalog.Application.Services.Import.SitesUpdate;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Tests.Application.Services.Import.SitesUpdate;

public sealed class SitesUpdateImportServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string UserEmail = "admin@test.com";
    private const string CsvFileName = "sites-update.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly MemoryCache _nicheOptionsMemoryCache;
    private readonly ImportArtifactStorageService _artifactStorageService;
    private readonly SitesUpdateImportService _sut;

    public SitesUpdateImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _nicheOptionsMemoryCache = new MemoryCache(new MemoryCacheOptions());
        _artifactStorageService = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));
        _sut = new SitesUpdateImportService(
            _context,
            NullLogger<SitesUpdateImportService>.Instance,
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
    public async Task ImportAsync_UpdatingNonPricingFieldOnly_KeepsPricingUnchanged()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Language\n" +
            "existing.com,DE\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Equal("DE", site.Language);
        AssertPrice(site, PriceType.Main, "finite:1:year", 50m);
        AssertPrice(site, PriceType.Casino, "finite:1:year", 250m);
        AssertPrice(site, PriceType.Casino, "permanent", 500m);
        AssertAvailability(site, PriceType.Casino, ServiceAvailabilityStatus.Available);
    }

    [Fact]
    public async Task ImportAsync_MainPriceColumnWithNumericValue_UpsertsTermPrice()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceUsd [1 year]\n" +
            "existing.com,120\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        AssertPrice(site, PriceType.Main, "finite:1:year", 120m);
        Assert.Null(site.PriceUsd);
    }

    [Fact]
    public async Task ImportAsync_MainPriceColumnWithEmptyCell_ClearsTermPriceAndCreatesWarning()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceUsd [1 year]\n" +
            "existing.com,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.SavedWithWarningsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.DoesNotContain(site.PriceOptions, price => price.PriceType == PriceType.Main && price.TermKey == "finite:1:year");

        var warningLines = GetDownloadLines(result.Downloads!.WarningRows!.Token);
        Assert.Equal("Domain,Field,Raw Value,Source Row Number,Warning", warningLines[0]);
        Assert.Equal("existing.com,PriceUsd [1 year],,2,Existing price was cleared because the imported cell was empty.", warningLines[1]);
    }

    [Fact]
    public async Task ImportAsync_ServicePriceColumnWithNumericValue_UpsertsPriceAndSetsAvailable()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceCasino [permanent]\n" +
            "existing.com,650\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        AssertPrice(site, PriceType.Casino, "permanent", 650m);
        AssertAvailability(site, PriceType.Casino, ServiceAvailabilityStatus.Available);
        Assert.Null(site.PriceCasino);
    }

    [Fact]
    public async Task ImportAsync_ServicePriceColumnWithEmptyCell_ClearsOnlyExactTerm()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceCasino [permanent]\n" +
            "existing.com,\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.DoesNotContain(site.PriceOptions, price => price.PriceType == PriceType.Casino && price.TermKey == "permanent");
        AssertPrice(site, PriceType.Casino, "finite:1:year", 250m);
        AssertAvailability(site, PriceType.Casino, ServiceAvailabilityStatus.Available);
    }

    [Theory]
    [InlineData("NO", ServiceAvailabilityStatus.NotAvailable, "NotAvailable")]
    [InlineData("YES", ServiceAvailabilityStatus.AvailableWithUnknownPrice, "AvailableWithUnknownPrice")]
    [InlineData("", ServiceAvailabilityStatus.Unknown, "Unknown")]
    public async Task ImportAsync_ServiceAvailabilityColumn_ClearsServicePricesAndCreatesWarning(
        string rawValue,
        ServiceAvailabilityStatus expectedStatus,
        string expectedStatusText)
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceCasinoAvailability\n" +
            $"existing.com,{rawValue}\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.SavedWithWarningsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.DoesNotContain(site.PriceOptions, price => price.PriceType == PriceType.Casino);
        AssertAvailability(site, PriceType.Casino, expectedStatus);

        var warningLines = GetDownloadLines(result.Downloads!.WarningRows!.Token);
        Assert.Equal($"existing.com,PriceCasinoAvailability,{rawValue},2,Casino status was set to {expectedStatusText} and existing Casino prices were cleared.", warningLines[1]);
    }

    [Theory]
    [InlineData("NO")]
    [InlineData("YES")]
    [InlineData("")]
    public async Task ImportAsync_ServiceAvailabilityAndNumericServicePrice_IsInvalidRow(string availability)
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceCasinoAvailability,PriceCasino [1 year]\n" +
            $"existing.com,{availability},250\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("availability cannot be empty, YES, or NO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_MainPriceZero_IsInvalidRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceUsd [1 year]\n" +
            "existing.com,0\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("PriceUsd [1 year] must be greater than 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_DuplicatePriceTypeAndTermHeader_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceUsd [1 year],PriceUsd [1 Year]\n" +
            "existing.com,100,120\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("Duplicate price column", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PriceUsd [1 month]")]
    [InlineData("PriceUsd []")]
    [InlineData("PriceUsd [0 years]")]
    [InlineData("PriceUsd [-1 year]")]
    public async Task ImportAsync_InvalidTermHeader_ThrowsImportHeaderValidationException(string header)
    {
        // Arrange
        using var stream = Utf8Csv($"Domain,{header}\nexisting.com,100\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("Invalid term header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_InvalidAvailabilityValue_IsInvalidRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceCasinoAvailability\n" +
            "existing.com,MAYBE\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("must be empty, YES, or NO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_UnknownDomain_IsReportedAsUnmatched()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceUsd [1 year]\n" +
            "missing.com,100\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.NotNull(result.Downloads!.UnmatchedRows);
    }

    [Fact]
    public async Task ImportAsync_UnmappedLocation_CreatesGenericWarningRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Location\n" +
            "existing.com,US/CA\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.SavedWithWarningsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.Null(site.LocationKey);
        Assert.Equal("US/CA", site.ImportedLocationRaw);

        var warningLines = GetDownloadLines(result.Downloads!.WarningRows!.Token);
        Assert.Equal("Domain,Field,Raw Value,Source Row Number,Warning", warningLines[0]);
        Assert.Equal("existing.com,Location,US/CA,2,Unmapped location. Site was saved with Location = Other.", warningLines[1]);
    }

    [Fact]
    public async Task ImportAsync_InvalidRowsDownloadContainsOriginalHeadersAndSourceRowNumber()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,PriceUsd [1 year]\n" +
            "existing.com,abc\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Equal("Domain,PriceUsd [1 year],Source Row Number,Error Details", invalidLines[0]);
        Assert.Contains("existing.com,abc,2,Invalid PriceUsd [1 year] value.", invalidLines);
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
            .Select(x => x.TrimEnd('\r'))
            .ToArray();
    }

    private static MemoryStream Utf8Csv(string text)
        => new(Encoding.UTF8.GetBytes(text));

    private static void AssertPrice(Site site, PriceType priceType, string termKey, decimal amountUsd)
    {
        var price = site.PriceOptions.Single(option => option.PriceType == priceType && option.TermKey == termKey);
        Assert.Equal(amountUsd, price.AmountUsd);
    }

    private static void AssertAvailability(
        Site site,
        PriceType serviceType,
        ServiceAvailabilityStatus status)
    {
        var availability = site.ServiceAvailabilities.Single(option => option.ServiceType == serviceType);
        Assert.Equal(status, availability.Status);
    }

    private void SeedSites()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = new Site
        {
            Domain = "existing.com",
            DR = 40,
            Traffic = 5000,
            Location = "US",
            LocationKey = "US",
            ImportedLocationRaw = "US",
            Language = "EN",
            Niche = "General",
            NicheTokens = NicheNormalizer.NormalizeTokens("General"),
            Categories = "Blog",
            IsQuarantined = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        existing.PriceOptions.Add(CreatePriceOption(existing, PriceType.Main, PricingTerm.FiniteYears(1), 50m, now));
        existing.PriceOptions.Add(CreatePriceOption(existing, PriceType.Casino, PricingTerm.FiniteYears(1), 250m, now));
        existing.PriceOptions.Add(CreatePriceOption(existing, PriceType.Casino, PricingTerm.Permanent, 500m, now));
        existing.ServiceAvailabilities.Add(new SiteServiceAvailability
        {
            SiteDomain = existing.Domain,
            ServiceType = PriceType.Casino,
            Status = ServiceAvailabilityStatus.Available,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        _context.Sites.AddRange(
            existing,
            new Site
            {
                Domain = "second.com",
                DR = 30,
                Traffic = 3000,
                Location = "UK",
                IsQuarantined = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        _context.SaveChanges();
    }

    private static SitePriceOption CreatePriceOption(
        Site site,
        PriceType priceType,
        PricingTerm term,
        decimal amountUsd,
        DateTime now)
    {
        return new SitePriceOption
        {
            SiteDomain = site.Domain,
            PriceType = priceType,
            TermKey = term.TermKey,
            TermType = term.TermType,
            TermValue = term.TermValue,
            TermUnit = term.TermUnit,
            AmountUsd = amountUsd,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
