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
    private readonly MemoryCache _catalogMemoryCache;
    private readonly ImportArtifactStorageService _artifactStorageService;
    private readonly SitesUpdateImportService _sut;

    public SitesUpdateImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _catalogMemoryCache = new MemoryCache(new MemoryCacheOptions());
        _artifactStorageService = new ImportArtifactStorageService(new MemoryCache(new MemoryCacheOptions()));
        _sut = new SitesUpdateImportService(
            _context,
            NullLogger<SitesUpdateImportService>.Instance,
            _artifactStorageService,
            new SitesCatalogCache(_catalogMemoryCache),
            new LocationNormalizer());

        SeedSites();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _catalogMemoryCache.Dispose();
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
    public async Task ImportAsync_TrafficAndDrWithoutSnapshotDate_SavesMetricSnapshotWithImportUtcDate()
    {
        // Arrange
        var expectedSnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var stream = Utf8Csv(
            "Domain,DR,Traffic\n" +
            "existing.com,55,12000\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.MetricSnapshotsSavedCount);
        Assert.Equal(expectedSnapshotDate, result.MetricSnapshotDate);
        Assert.Null(result.MetricHistorySkippedReason);

        var snapshot = Assert.Single(_context.SiteMetricSnapshots);
        Assert.Equal("existing.com", snapshot.Domain);
        Assert.Equal(expectedSnapshotDate, snapshot.SnapshotDate);
        Assert.Equal(12000, snapshot.Traffic);
        Assert.Equal(55, snapshot.DomainRating);
        Assert.Equal("SitesUpdateImport", snapshot.Source);
        Assert.Null(snapshot.AhrefsSyncRunId);
    }

    [Fact]
    public async Task ImportAsync_TrafficAndDrWithMetricSnapshotDate_SavesMetricSnapshotForProvidedDate()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,DR,Traffic\n" +
            "existing.com,55,12000\n");

        // Act
        var result = await ImportAsync(stream, new DateOnly(2026, 6, 24));

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.MetricSnapshotsSavedCount);
        Assert.Equal(new DateOnly(2026, 6, 24), result.MetricSnapshotDate);

        var snapshot = Assert.Single(_context.SiteMetricSnapshots);
        Assert.Equal(new DateOnly(2026, 6, 24), snapshot.SnapshotDate);
        Assert.Equal(12000, snapshot.Traffic);
        Assert.Equal(55, snapshot.DomainRating);
        Assert.Equal("SitesUpdateImport", snapshot.Source);
    }

    [Fact]
    public async Task ImportAsync_TrafficAndDrWithExistingSnapshot_OverwritesSnapshot()
    {
        // Arrange
        _context.SiteMetricSnapshots.Add(new SiteMetricSnapshot
        {
            Id = Guid.NewGuid(),
            Domain = "existing.com",
            SnapshotDate = new DateOnly(2026, 6, 24),
            Traffic = 100,
            DomainRating = 10,
            Source = "AhrefsMonthlySync",
            FetchedAt = new DateTime(2026, 6, 24, 1, 0, 0, DateTimeKind.Utc)
        });
        await _context.SaveChangesAsync();

        using var stream = Utf8Csv(
            "Domain,DR,Traffic\n" +
            "existing.com,55,12000\n");

        // Act
        var result = await ImportAsync(stream, new DateOnly(2026, 6, 24));

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.MetricSnapshotsSavedCount);

        var snapshot = Assert.Single(_context.SiteMetricSnapshots);
        Assert.Equal(new DateOnly(2026, 6, 24), snapshot.SnapshotDate);
        Assert.Equal(12000, snapshot.Traffic);
        Assert.Equal(55, snapshot.DomainRating);
        Assert.Equal("SitesUpdateImport", snapshot.Source);
        Assert.Null(snapshot.AhrefsSyncRunId);
    }

    [Theory]
    [InlineData("Domain,Traffic\nexisting.com,12000\n")]
    [InlineData("Domain,DR\nexisting.com,55\n")]
    public async Task ImportAsync_PartialMetricUpdate_DoesNotSaveMetricSnapshot(string csv)
    {
        // Arrange
        using var stream = Utf8Csv(csv);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Null(result.MetricSnapshotsSavedCount);
        Assert.Null(result.MetricSnapshotDate);
        Assert.Equal("File did not include both Traffic and DR.", result.MetricHistorySkippedReason);
        Assert.Empty(_context.SiteMetricSnapshots);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedDomain_DoesNotSaveMetricSnapshot()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,DR,Traffic\n" +
            "missing.com,55,12000\n");

        // Act
        var result = await ImportAsync(stream, new DateOnly(2026, 6, 24));

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.Equal(0, result.MetricSnapshotsSavedCount);
        Assert.Equal(new DateOnly(2026, 6, 24), result.MetricSnapshotDate);
        Assert.Empty(_context.SiteMetricSnapshots);
    }

    [Fact]
    public async Task ImportAsync_DuplicateMetricRows_SavesLastValidRowSnapshot()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,DR,Traffic\n" +
            "existing.com,45,10000\n" +
            "existing.com,55,12000\n");

        // Act
        var result = await ImportAsync(stream, new DateOnly(2026, 6, 24));

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.DuplicateDomainsCount);
        Assert.Equal(1, result.MetricSnapshotsSavedCount);

        var snapshot = Assert.Single(_context.SiteMetricSnapshots);
        Assert.Equal(new DateOnly(2026, 6, 24), snapshot.SnapshotDate);
        Assert.Equal(12000, snapshot.Traffic);
        Assert.Equal(55, snapshot.DomainRating);
    }

    [Fact]
    public async Task ImportAsync_MainPriceColumnWithNumericValue_UpsertsTermPrice()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceUsd\n" +
            "existing.com,1 year,120\n");

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
            "Domain,Term,PriceUsd\n" +
            "existing.com,1 year,\n");

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
        Assert.Equal("existing.com,PriceUsd,,2,Existing price for 1 year was cleared because the imported cell was empty.", warningLines[1]);
    }

    [Fact]
    public async Task ImportAsync_ServicePriceColumnWithNumericValue_UpsertsPriceAndSetsAvailable()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceCasino\n" +
            "existing.com,permanent,650\n");

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
            "Domain,Term,PriceCasino\n" +
            "existing.com,permanent,\n");

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
    [InlineData("NO", "NotAvailable")]
    [InlineData("YES", "AvailableWithUnknownPrice")]
    [InlineData("", "Unknown")]
    public async Task ImportAsync_ServiceStatusValue_ClearsExactTermPriceAndResolvesStatus(
        string rawValue,
        string expectedStatusText)
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceCasino\n" +
            $"existing.com,1 year,{rawValue}\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(1, result.SavedWithWarningsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.DoesNotContain(site.PriceOptions, price => price.PriceType == PriceType.Casino && price.TermKey == "finite:1:year");
        AssertPrice(site, PriceType.Casino, "permanent", 500m);
        AssertAvailability(site, PriceType.Casino, ServiceAvailabilityStatus.Available);

        var warningLines = GetDownloadLines(result.Downloads!.WarningRows!.Token);
        Assert.Equal($"existing.com,PriceCasino,{rawValue},2,Existing Casino price for 1 year was cleared because the imported cell was {expectedStatusText}.", warningLines[1]);
    }

    [Fact]
    public async Task ImportAsync_ServiceStatusValueWithoutRemainingPrices_UsesCellStatus()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceCrypto\n" +
            "existing.com,1 year,NO\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InvalidRowsCount);

        var site = await GetSiteAsync("existing.com");
        Assert.DoesNotContain(site.PriceOptions, price => price.PriceType == PriceType.Crypto);
        AssertAvailability(site, PriceType.Crypto, ServiceAvailabilityStatus.NotAvailable);
    }

    [Fact]
    public async Task ImportAsync_MainPriceZero_IsInvalidRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceUsd\n" +
            "existing.com,1 year,0\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("PriceUsd must be greater than 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_DuplicatePriceTypeAndTermHeader_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceUsd,PriceUsd\n" +
            "existing.com,1 year,100,120\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("Duplicate pricing column", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WithPricingHeaderWithoutTerm_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv("Domain,PriceUsd\nexisting.com,100\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("Term column is required", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1 month")]
    [InlineData("0 years")]
    [InlineData("-1 year")]
    [InlineData("1 year; permanent")]
    public async Task ImportAsync_InvalidTermValue_IsInvalidRow(string term)
    {
        // Arrange
        using var stream = Utf8Csv($"Domain,Term,PriceUsd\nexisting.com,{term},100\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(
            invalidLines,
            line => line.Contains(
                "Term must be empty, No term, permanent, or a positive number of years such as 1 year or 2 years.",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_InvalidAvailabilityValue_IsInvalidRow()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceCasino\n" +
            "existing.com,1 year,MAYBE\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("must be empty, YES, NO, or a positive numeric value", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_UnknownDomain_IsReportedAsUnmatched()
    {
        // Arrange
        using var stream = Utf8Csv(
            "Domain,Term,PriceUsd\n" +
            "missing.com,1 year,100\n");

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
            "Domain,Term,PriceUsd\n" +
            "existing.com,1 year,abc\n");

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Equal("Domain,Term,PriceUsd,Source Row Number,Error Details", invalidLines[0]);
        Assert.Contains("existing.com,1 year,abc,2,Invalid PriceUsd value.", invalidLines);
    }

    private async Task<SitesUpdateImportResult> ImportAsync(
        Stream stream,
        DateOnly? metricSnapshotDate = null)
    {
        return await _sut.ImportAsync(
            stream,
            CsvFileName,
            CsvContentType,
            UserId,
            UserEmail,
            metricSnapshotDate,
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
