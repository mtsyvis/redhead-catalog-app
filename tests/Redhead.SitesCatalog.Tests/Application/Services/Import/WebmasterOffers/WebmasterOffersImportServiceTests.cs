using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Import.Artifacts;
using Redhead.SitesCatalog.Application.Services.Import.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services.Import.WebmasterOffers;

public sealed class WebmasterOffersImportServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private const string UserEmail = "admin@test.com";
    private const string CsvFileName = "webmaster-offers.csv";
    private const string CsvContentType = "text/csv";

    private readonly ApplicationDbContext _context;
    private readonly ImportArtifactStorageService _artifactStorageService;
    private readonly MemoryCache _artifactMemoryCache;
    private readonly WebmasterOffersImportService _sut;

    public WebmasterOffersImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _artifactMemoryCache = new MemoryCache(new MemoryCacheOptions());
        _artifactStorageService = new ImportArtifactStorageService(_artifactMemoryCache);
        _sut = new WebmasterOffersImportService(
            _context,
            NullLogger<WebmasterOffersImportService>.Instance,
            _artifactStorageService);

        SeedSitesAndMailboxes();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _artifactMemoryCache.Dispose();
    }

    [Fact]
    public async Task ImportAsync_HeaderNotExact_ThrowsImportHeaderValidationException()
    {
        // Arrange
        using var stream = Utf8Csv("Domain,ContactRawText\nexisting.com,contact@example.com\n");

        // Act
        var exception = await Assert.ThrowsAsync<ImportHeaderValidationException>(() => ImportAsync(stream));

        // Assert
        Assert.Contains("CSV header is invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_UnknownDomain_IsReportedAsUnmatchedAndDoesNotCreateOffer()
    {
        // Arrange
        using var stream = CsvWithRows(Row(domain: "missing.com", contact: "contact@example.com", mainAmount: "100"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.UnmatchedRowsCount);
        Assert.Empty(_context.SiteWebmasterOffers);
        Assert.NotNull(result.Downloads!.UnmatchedRows);
    }

    [Fact]
    public async Task ImportAsync_RowWithUnexpectedColumnCount_IsInvalidRow()
    {
        // Arrange
        var row = Row(domain: "existing.com", contact: "contact@example.com", mainAmount: "100")
            .Concat(["extra cell"])
            .ToArray();
        using var stream = CsvWithRows(row);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        Assert.Empty(_context.SiteWebmasterOffers);

        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains("Row has 29 columns but expected 28.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_DuplicateDomains_CreateSeparateOffersAndReuseWebmasterByContact()
    {
        // Arrange
        using var stream = CsvWithRows(
            Row(
                domain: "https://existing.com/path",
                contact: "  Contact@Example.com  ",
                mailboxRaw: "PUBS / unknown alias",
                term: "2 years",
                mainAmount: "100",
                mainDetails: "standard guest post"),
            Row(
                domain: "existing.com",
                contact: "contact@example.com",
                mailboxRaw: "publications@redheaddigital.agency",
                term: "garbage term",
                casinoAmount: "200",
                comment: "second condition"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.InvalidRowsCount);
        Assert.Equal(0, result.UnmatchedRowsCount);
        Assert.Equal(1, result.SavedWithWarningsCount);

        var webmaster = Assert.Single(_context.Webmasters);
        Assert.Equal("contact@example.com", webmaster.NormalizedContactRawText);
        Assert.Equal("contact@example.com", webmaster.PrimaryEmail);

        var offers = await _context.SiteWebmasterOffers
            .Include(offer => offer.Prices)
            .Include(offer => offer.LinkbuilderMailboxes)
            .OrderBy(offer => offer.CreatedAtUtc)
            .ToListAsync();
        Assert.Equal(2, offers.Count);
        Assert.All(offers, offer => Assert.Equal(webmaster.Id, offer.WebmasterId));
        Assert.All(offers, offer => Assert.Equal("existing.com", offer.SiteDomain));

        Assert.Equal(TermType.Finite, offers[0].TermType);
        Assert.Equal(2, offers[0].TermValue);
        Assert.Equal(TermUnit.Year, offers[0].TermUnit);
        Assert.Equal("2 years", offers[0].TermRawText);
        Assert.Equal(WebmasterOfferPriceType.Main, Assert.Single(offers[0].Prices).PriceType);
        Assert.Single(offers[0].LinkbuilderMailboxes);

        Assert.Null(offers[1].TermType);
        Assert.Null(offers[1].TermValue);
        Assert.Null(offers[1].TermUnit);
        Assert.Equal("garbage term", offers[1].TermRawText);
        Assert.Equal(WebmasterOfferPriceType.Casino, Assert.Single(offers[1].Prices).PriceType);

        var warningLines = GetDownloadLines(result.Downloads!.WarningRows!.Token);
        Assert.Equal("Domain,Field,Raw Value,Source Row Number,Warning", warningLines[0]);
        Assert.Contains("unknown alias", warningLines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_SameFileImportedTwice_SkipsSecondImportRows()
    {
        // Arrange
        using var firstStream = CsvWithRows(
            Row(domain: "existing.com", contact: "first@example.com", mainAmount: "100"),
            Row(domain: "existing.com", contact: "second@example.com", mainAmount: "200"));

        // Act
        var firstResult = await ImportAsync(firstStream);

        using var secondStream = CsvWithRows(
            Row(domain: "existing.com", contact: "first@example.com", mainAmount: "100"),
            Row(domain: "existing.com", contact: "second@example.com", mainAmount: "200"));
        var secondResult = await ImportAsync(secondStream);

        // Assert
        Assert.Equal(2, firstResult.ImportedCount);
        Assert.Equal(0, firstResult.SkippedDuplicateCount);
        Assert.Equal(0, secondResult.ImportedCount);
        Assert.Equal(2, secondResult.SkippedDuplicateCount);
        Assert.Equal(2, await _context.SiteWebmasterOffers.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_SameExactRowAppearsTwice_SkipsSecondRowAsDuplicate()
    {
        // Arrange
        var duplicateRow = Row(
            domain: "existing.com",
            contact: "duplicate@example.com",
            mailboxRaw: "publications@redheaddigital.agency",
            term: "1 year",
            mainAmount: "100",
            mainDetails: "guest post",
            comment: "same condition");
        using var stream = CsvWithRows(duplicateRow, duplicateRow);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Equal(1, await _context.SiteWebmasterOffers.CountAsync());
        Assert.Equal(1, await _context.WebmasterOfferPrices.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_SameDomainContactWithChangedPriceOrComment_CreatesSeparateOffers()
    {
        // Arrange
        using var stream = CsvWithRows(
            Row(
                domain: "existing.com",
                contact: "changed@example.com",
                mainAmount: "100",
                comment: "first condition"),
            Row(
                domain: "existing.com",
                contact: "changed@example.com",
                mainAmount: "125",
                comment: "second condition"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.SkippedDuplicateCount);
        Assert.Equal(2, await _context.SiteWebmasterOffers.CountAsync());
        Assert.Single(_context.Webmasters);
    }

    [Fact]
    public async Task ImportAsync_SameContentWithDifferentLineEndings_SkipsSecondRowAsDuplicate()
    {
        // Arrange
        using var stream = CsvWithRows(
            Row(
                domain: "existing.com",
                contact: "info@example.com\r\nreply@example.com - answer",
                mainAmount: "100",
                comment: "line one\r\nline two"),
            Row(
                domain: "existing.com",
                contact: "info@example.com\nreply@example.com - answer",
                mainAmount: "100",
                comment: "line one\nline two"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Equal(1, await _context.SiteWebmasterOffers.CountAsync());
    }

    [Theory]
    [InlineData("info@huislijn.nl\nr.barends@huislijn.nl (ответ тут)", "r.barends@huislijn.nl")]
    [InlineData("info@huislijn.nl\nr.barends@huislijn.nl - answer here", "r.barends@huislijn.nl")]
    [InlineData("info@huislijn.nl\nr.barends@huislijn.nl = отв тут", "r.barends@huislijn.nl")]
    [InlineData("info@huislijn.nl\nr.barends@huislijn.nl - write here", "r.barends@huislijn.nl")]
    [InlineData("first@example.com - answer\nsecond@example.com - answer", "first@example.com")]
    public async Task ImportAsync_ContactWithPrimaryEmailMarker_UsesFirstMarkedEmailAsPrimaryEmail(
        string contact,
        string expectedPrimaryEmail)
    {
        // Arrange
        using var stream = CsvWithRows(Row(
            domain: "existing.com",
            contact: contact,
            mainAmount: "100"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.ImportedCount);
        var webmaster = Assert.Single(_context.Webmasters);
        Assert.Equal(expectedPrimaryEmail, webmaster.PrimaryEmail);
    }

    [Fact]
    public async Task ImportAsync_ContactWithoutMarkerAndOneEmail_UsesEmailAsPrimaryEmail()
    {
        // Arrange
        using var stream = CsvWithRows(Row(
            domain: "existing.com",
            contact: "info@huislijn.nl",
            mainAmount: "100"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.ImportedCount);
        var webmaster = Assert.Single(_context.Webmasters);
        Assert.Equal("info@huislijn.nl", webmaster.PrimaryEmail);
    }

    [Fact]
    public async Task ImportAsync_ContactWithoutMarkerAndMultipleEmails_LeavesPrimaryEmailEmpty()
    {
        // Arrange
        using var stream = CsvWithRows(Row(
            domain: "existing.com",
            contact: "info@huislijn.nl\nr.barends@huislijn.nl",
            mainAmount: "100"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.ImportedCount);
        var webmaster = Assert.Single(_context.Webmasters);
        Assert.Null(webmaster.PrimaryEmail);
    }

    [Fact]
    public async Task ImportAsync_MoreThanOneBatch_ReusesWebmasterAcrossBatchBoundary()
    {
        // Arrange
        var rows = Enumerable.Range(1, 1001)
            .Select(index => Row(
                domain: "existing.com",
                contact: "batch-contact@example.com",
                mainAmount: index.ToString()))
            .ToArray();
        using var stream = CsvWithRows(rows);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1001, result.ImportedCount);
        Assert.Equal(1001, await _context.SiteWebmasterOffers.CountAsync());
        Assert.Single(_context.Webmasters);
    }

    [Fact]
    public async Task ImportAsync_AllRawPriceTypes_CreatesWebmasterOfferPrices()
    {
        // Arrange
        var row = Row(
            domain: "existing.com",
            contact: "prices@example.com",
            term: "1 year",
            mainAmount: "100",
            casinoAmount: "110",
            cryptoAmount: "120",
            datingAmount: "130",
            linkInsertionAmount: "140",
            linkInsertion18Amount: "150",
            bannerAmount: "160",
            banner18Amount: "170",
            homepageAmount: "180",
            homepage18Amount: "190",
            homepage18Details: "homepage 18+ details");
        using var stream = CsvWithRows(row);

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.ImportedCount);
        var offer = await _context.SiteWebmasterOffers
            .Include(siteOffer => siteOffer.Prices)
            .SingleAsync();
        Assert.Equal(10, offer.Prices.Count);
        Assert.Equal(
            Enum.GetValues<WebmasterOfferPriceType>().OrderBy(type => type),
            offer.Prices.Select(price => price.PriceType).OrderBy(type => type));
        Assert.Contains(offer.Prices, price =>
            price.PriceType == WebmasterOfferPriceType.HomepageTextLink18Plus
            && price.WebmasterPriceUsd == 190m
            && price.WebmasterPriceDetails == "homepage 18+ details");
    }

    [Fact]
    public async Task ImportAsync_DetailsWithoutAmount_CreatesPriceRowWithNullAmount()
    {
        // Arrange
        using var stream = CsvWithRows(Row(
            domain: "existing.com",
            contact: "details@example.com",
            mainDetails: "ask webmaster"));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(1, result.ImportedCount);
        var price = await _context.WebmasterOfferPrices.SingleAsync();
        Assert.Equal(WebmasterOfferPriceType.Main, price.PriceType);
        Assert.Null(price.WebmasterPriceUsd);
        Assert.Equal("ask webmaster", price.WebmasterPriceDetails);
    }

    [Theory]
    [InlineData("0", "MainWebmasterPriceUsd must be greater than 0.")]
    [InlineData("abc", "Invalid MainWebmasterPriceUsd value.")]
    public async Task ImportAsync_InvalidAmount_IsInvalidRow(string amount, string expectedError)
    {
        // Arrange
        using var stream = CsvWithRows(Row(
            domain: "existing.com",
            contact: "bad-price@example.com",
            mainAmount: amount));

        // Act
        var result = await ImportAsync(stream);

        // Assert
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.InvalidRowsCount);
        var invalidLines = GetDownloadLines(result.Downloads!.InvalidRows!.Token);
        Assert.Contains(invalidLines, line => line.Contains(expectedError, StringComparison.Ordinal));
    }

    private Task<WebmasterOffersImportResult> ImportAsync(Stream stream)
        => _sut.ImportAsync(stream, CsvFileName, CsvContentType, UserId, UserEmail, CancellationToken.None);

    private string[] GetDownloadLines(string token)
    {
        var download = _artifactStorageService.GetCsvDownload(token);
        Assert.NotNull(download);

        return Encoding.UTF8.GetString(download!.Content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
    }

    private static MemoryStream CsvWithRows(params string[][] rows)
    {
        var lines = new List<string>
        {
            string.Join(",", ImportConstants.WebmasterOffersImportColumnsInOrder.Select(Escape))
        };
        lines.AddRange(rows.Select(row => string.Join(",", row.Select(Escape))));
        return Utf8Csv(string.Join("\n", lines) + "\n");
    }

    private static string[] Row(
        string domain,
        string contact,
        string? outreach = null,
        string? mailboxRaw = null,
        string? term = null,
        string? mainAmount = null,
        string? mainDetails = null,
        string? casinoAmount = null,
        string? cryptoAmount = null,
        string? datingAmount = null,
        string? linkInsertionAmount = null,
        string? linkInsertion18Amount = null,
        string? bannerAmount = null,
        string? banner18Amount = null,
        string? homepageAmount = null,
        string? homepage18Amount = null,
        string? homepage18Details = null,
        string? comment = null)
    {
        var valuesByHeader = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [ImportConstants.WebmasterOffersImportColumns.Domain] = domain,
            [ImportConstants.WebmasterOffersImportColumns.ContactRawText] = contact,
            [ImportConstants.WebmasterOffersImportColumns.OutreachSenderRawText] = outreach,
            [ImportConstants.WebmasterOffersImportColumns.LinkbuilderMailboxRawText] = mailboxRaw,
            [ImportConstants.WebmasterOffersImportColumns.Term] = term,
            [ImportConstants.WebmasterOffersImportColumns.MainWebmasterPriceUsd] = mainAmount,
            [ImportConstants.WebmasterOffersImportColumns.MainWebmasterPriceDetails] = mainDetails,
            [ImportConstants.WebmasterOffersImportColumns.CasinoWebmasterPriceUsd] = casinoAmount,
            [ImportConstants.WebmasterOffersImportColumns.CryptoWebmasterPriceUsd] = cryptoAmount,
            [ImportConstants.WebmasterOffersImportColumns.DatingWebmasterPriceUsd] = datingAmount,
            [ImportConstants.WebmasterOffersImportColumns.LinkInsertionWebmasterPriceUsd] = linkInsertionAmount,
            [ImportConstants.WebmasterOffersImportColumns.LinkInsertion18PlusWebmasterPriceUsd] = linkInsertion18Amount,
            [ImportConstants.WebmasterOffersImportColumns.BannerWebmasterPriceUsd] = bannerAmount,
            [ImportConstants.WebmasterOffersImportColumns.Banner18PlusWebmasterPriceUsd] = banner18Amount,
            [ImportConstants.WebmasterOffersImportColumns.HomepageTextLinkWebmasterPriceUsd] = homepageAmount,
            [ImportConstants.WebmasterOffersImportColumns.HomepageTextLink18PlusWebmasterPriceUsd] = homepage18Amount,
            [ImportConstants.WebmasterOffersImportColumns.HomepageTextLink18PlusWebmasterPriceDetails] = homepage18Details,
            [ImportConstants.WebmasterOffersImportColumns.CommentText] = comment
        };

        return ImportConstants.WebmasterOffersImportColumnsInOrder
            .Select(header => valuesByHeader.TryGetValue(header, out var value) ? value ?? string.Empty : string.Empty)
            .ToArray();
    }

    private static string Escape(string value)
    {
        if (value.Contains('"', StringComparison.Ordinal)
            || value.Contains(',', StringComparison.Ordinal)
            || value.Contains('\n')
            || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private static MemoryStream Utf8Csv(string text)
        => new(Encoding.UTF8.GetBytes(text));

    private void SeedSitesAndMailboxes()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _context.Sites.Add(new Site
        {
            Domain = "existing.com",
            DR = 40,
            Traffic = 5000,
            Location = "US",
            IsQuarantined = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        _context.LinkbuilderMailboxes.Add(new LinkbuilderMailbox
        {
            Id = Guid.NewGuid(),
            Email = "publications@redheaddigital.agency",
            DisplayName = "publications@redheaddigital.agency",
            Aliases = ["pubs", "publications"],
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        _context.SaveChanges();
    }
}
