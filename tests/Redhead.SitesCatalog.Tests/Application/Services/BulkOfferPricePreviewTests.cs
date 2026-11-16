using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class BulkOfferPricePreviewTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _context.Dispose();
    private BulkOfferPricePreviewService CreateService() => new(_context, new OfferCurrencyService(_context));

    [Fact]
    public async Task Preview_UsesExactNormalizedContactAcrossTechnicalWebmasters_AndDoesNotWrite()
    {
        // Arrange
        var first = AddOffer("first.example", PricingTerm.Unknown, amount: 20m);
        var second = AddOffer("second.example", PricingTerm.FiniteYears(1), amount: 30m);
        AddOffer("other.example", PricingTerm.FiniteYears(1), contact: "other@example.com");
        first.Site.IsQuarantined = true;
        first.Status = SiteWebmasterOfferStatus.Inactive;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var before = await SnapshotAsync();
        var request = Request(PricingTerm.FiniteYears(1), [0],
            ("https://www.FIRST.example/path?q=1", ["20"]), ("second.example", ["30"]), ("other.example", ["40"]));

        // Act
        var result = await CreateService().PreviewAsync(request with { PrimaryEmail = " CONTACT@EXAMPLE.COM " });

        // Assert
        Assert.Equal("contact@example.com", result.PrimaryEmail);
        Assert.Equal(1, result.ReadyCount);
        Assert.Equal(1, result.NoChangesCount);
        Assert.Equal(1, result.ExceptionCount);
        var ready = result.Rows[0];
        Assert.Equal(first.Id, ready.OfferId);
        Assert.Equal("first.example", ready.Domain);
        Assert.Equal("No term", ready.CurrentTerm);
        Assert.Equal("1 year", ready.ProposedTerm);
        Assert.Equal(SiteWebmasterOfferStatus.Active, ready.ProposedStatus);
        Assert.False(Assert.Single(ready.Prices).Changed);
        Assert.Equal(second.Id, result.Rows[1].OfferId);
        Assert.Equal("Offer not found", result.Rows[2].Category);
        Assert.Equal(before, await SnapshotAsync());
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("unknown", "1year", "Ready", null)]
    [InlineData("1year", "1year", "No changes", null)]
    [InlineData("permanent", "1year", "Exceptions", "Different term")]
    [InlineData("1year", "unknown", "Exceptions", "Different term")]
    [InlineData("unknown", "unknown", "No changes", null)]
    [InlineData("permanent", "permanent", "No changes", null)]
    public async Task Preview_MatchesOnlySelectedOrUnknownTerm(string saved, string selected, string outcome, string? category)
    {
        // Arrange
        AddOffer("term.example", Term(saved));
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(Term(selected), [0], ("term.example", ["10"])));

        // Assert
        var row = Assert.Single(result.Rows);
        Assert.Equal(outcome, row.Outcome);
        Assert.Equal(category, row.Category);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preview_NeverPrefersExactTermOrActiveAcrossDuplicateCandidates(bool sameTerm)
    {
        // Arrange
        AddOffer("duplicate.example", PricingTerm.FiniteYears(1));
        var second = AddOffer("duplicate.example", sameTerm ? PricingTerm.FiniteYears(1) : PricingTerm.Unknown);
        second.Status = SiteWebmasterOfferStatus.Inactive;
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.FiniteYears(1), [0], ("duplicate.example", ["15"])));

        // Assert
        Assert.Equal("Multiple matching offers", Assert.Single(result.Rows).Category);
        Assert.Equal(0, result.ReadyCount);
    }

    [Fact]
    public async Task Preview_IgnoresOtherValidKnownTerms_AndRawContactSubstring()
    {
        // Arrange
        var target = AddOffer("match.example", PricingTerm.FiniteYears(1));
        AddOffer("match.example", PricingTerm.Permanent);
        var other = AddOffer("match.example", PricingTerm.FiniteYears(1), contact: "another@example.com");
        other.ContactRawText = "please contact contact@example.com";
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.FiniteYears(1), [0], ("match.example", ["15"])));

        // Assert
        Assert.Equal(target.Id, Assert.Single(result.Rows).OfferId);
        Assert.Equal(1, result.ReadyCount);
    }

    [Theory]
    [InlineData(false, "Invalid structured term")]
    [InlineData(true, "Inconsistent price terms")]
    public async Task Preview_RejectsMalformedOfferOrUnselectedPriceTerms(bool priceTerm, string category)
    {
        // Arrange
        var offer = AddOffer("malformed.example", PricingTerm.FiniteYears(1));
        if (priceTerm)
        {
            offer.Prices.Add(new WebmasterOfferPrice { Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Casino, TermValue = 2 });
        }
        else
        {
            AddOffer("malformed.example", PricingTerm.Unknown).TermValue = 2;
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.FiniteYears(1), [0], ("malformed.example", ["15"])));

        // Assert
        Assert.Equal(category, Assert.Single(result.Rows).Category);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(0, "YES")]
    [InlineData(0, "NO")]
    [InlineData(0, "CLEAR")]
    [InlineData(1, "")]
    [InlineData(1, "Unknown")]
    [InlineData(1, "CLEAR")]
    [InlineData(1, "0")]
    [InlineData(1, "-1")]
    [InlineData(1, "0.001")]
    [InlineData(1, "10000000000000000")]
    [InlineData(1, "$20")]
    public async Task Preview_RejectsInvalidSelectedCells(int type, string input)
    {
        // Arrange
        AddOffer("input.example", PricingTerm.Unknown);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [type], ("input.example", [input])));

        // Assert
        Assert.Equal("Invalid selected value", Assert.Single(result.Rows).Category);
        Assert.Equal(1, result.ExceptionCount);
        Assert.Contains("Row 1", result.Rows[0].Reasons[0]);
    }

    [Theory]
    [InlineData(" yEs ", ServiceAvailabilityStatus.AvailableWithUnknownPrice)]
    [InlineData("no", ServiceAvailabilityStatus.NotAvailable)]
    public async Task Preview_OptionalYesNoPreservesInactiveStatus_AndClearsNumericProvenance(string input, ServiceAvailabilityStatus expected)
    {
        // Arrange
        var offer = AddOffer("optional.example", PricingTerm.Unknown);
        offer.Status = SiteWebmasterOfferStatus.Inactive;
        offer.Prices.Single().PriceType = WebmasterOfferPriceType.Casino;
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [1], ("optional.example", [input])));

        // Assert
        var row = Assert.Single(result.Rows);
        Assert.Equal(SiteWebmasterOfferStatus.Inactive, row.ProposedStatus);
        var price = Assert.Single(row.Prices).Proposed;
        Assert.Equal(expected, price.AvailabilityStatus);
        Assert.Null(price.WebmasterPriceUsd);
        Assert.Null(price.SourceAmount);
        Assert.Null(price.AppliedUsdRate);
        Assert.Null(price.RateDate);
    }

    [Fact]
    public async Task Preview_CollapsesIdenticalProposals_ReportsConflictsAndAllReasonsInInputOrder()
    {
        // Arrange
        AddOffer("same.example", PricingTerm.Unknown);
        AddOffer("conflict.example", PricingTerm.Unknown);
        _context.Sites.Add(new Site { Domain = "no-offer.example" });
        await _context.SaveChangesAsync();
        var request = Request(PricingTerm.Unknown, [0, 1],
            ("same.example", ["20,50", "yes"]), ("https://www.SAME.example/path", ["20.50", "YES"]),
            ("conflict.example", ["20", "NO"]), ("conflict.example", ["21", ""]),
            ("missing.example", ["10", "10"]), ("no-offer.example", ["10", "10"]), ("bad domain", ["0", "CLEAR"]));

        // Act
        var result = await CreateService().PreviewAsync(request);

        // Assert
        Assert.Equal(7, result.InputCount);
        Assert.Equal(1, result.DuplicateInputCount);
        Assert.Equal(1, result.ReadyCount);
        Assert.Equal(4, result.ExceptionCount);
        Assert.Equal(new[] { "same.example", "conflict.example", "missing.example", "no-offer.example", "bad domain" }, result.Rows.Select(row => row.Domain));
        Assert.Equal(new[] { "Conflicting input rows", "Site not found", "Offer not found", "Invalid domain" }, result.Rows.Skip(1).Select(row => row.Category));
        Assert.Equal(2, result.Rows[0].Inputs.Count);
        Assert.Equal("20,50", result.Rows[0].Inputs[0].Values[0]);
        Assert.True(result.Rows[^1].InvalidDomain);
        Assert.Equal(3, result.Rows[^1].Reasons.Count);
        Assert.Equal(2, result.Rows[1].Reasons.Count);
    }

    [Theory]
    [InlineData("EUR", "43.55", "54")]
    [InlineData("EUR", "43.56", "55")]
    [InlineData("EUR", "43.60", "55")]
    [InlineData("GBP", "10", "14")]
    [InlineData("CAD", "10", "13")]
    [InlineData("USD", "10.25", "10.25")]
    public async Task Preview_ReusesCurrencyConversionAndReferenceContract(string currency, string input, string expectedUsd)
    {
        // Arrange
        AddOffer("currency.example", PricingTerm.Unknown);
        OfferCurrencyTestData.AddSnapshot(_context, 0.8m, new DateTime(2026, 11, 12, 23, 0, 0, DateTimeKind.Utc));
        await _context.SaveChangesAsync();
        var request = Request(PricingTerm.Unknown, [0], ("currency.example", [input])) with { SourceCurrency = currency };

        // Act
        var result = await CreateService().PreviewAsync(request);

        // Assert
        var price = Assert.Single(Assert.Single(result.Rows).Prices).Proposed;
        Assert.Equal(decimal.Parse(expectedUsd, System.Globalization.CultureInfo.InvariantCulture), price.WebmasterPriceUsd);
        Assert.Equal(currency, price.SourceCurrency);
        Assert.NotNull(result.ConversionReference);
        var expected = await new OfferCurrencyService(_context).ConvertAsync(new(currency, price.SourceAmount!.Value, result.ConversionReference.RateReference), true);
        Assert.Equal(expected.WebmasterPriceUsd, price.WebmasterPriceUsd);
        Assert.Equal(expected.AppliedUsdRate, price.AppliedUsdRate);
        Assert.Equal(currency == "CAD" ? 5m : 0m, price.AppliedUsdSurcharge);
        Assert.Equal(expected.AppliedUsdSurcharge, price.AppliedUsdSurcharge);
        Assert.Equal(expected.AppliedUsdSurcharge, result.ConversionReference.AppliedUsdSurcharge);
        Assert.Equal(currency == "CAD" ? new DateOnly(2026, 11, 12) : null, price.RateDate);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(5, 13)]
    public async Task Preview_UnchangedForeignQuotePreservesSavedSurchargeAndHistoricConversion(int surcharge, int usd)
    {
        // Arrange
        var offer = AddOffer("historic.example", PricingTerm.Unknown);
        var price = offer.Prices.Single();
        price.SourceCurrency = "CAD";
        price.SourceAmount = 10m;
        price.AppliedUsdRate = 0.8m;
        price.WebmasterPriceUsd = usd;
        price.AppliedUsdSurcharge = surcharge;
        price.RateDate = new DateOnly(2026, 11, 10);
        OfferCurrencyTestData.AddSnapshot(_context, 0.9m, new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var before = await SnapshotAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [0], ("historic.example", ["10"])) with { SourceCurrency = "CAD" });

        // Assert
        Assert.Equal(1, result.NoChangesCount);
        var change = Assert.Single(Assert.Single(result.Rows).Prices);
        Assert.False(change.Changed);
        Assert.Equal(surcharge, change.Current!.AppliedUsdSurcharge);
        Assert.Equal(surcharge, change.Proposed.AppliedUsdSurcharge);
        Assert.Equal(usd, change.Proposed.WebmasterPriceUsd);
        Assert.Equal(0.8m, change.Proposed.AppliedUsdRate);
        Assert.Equal(new DateOnly(2026, 11, 10), change.Proposed.RateDate);
        Assert.Null(result.ConversionReference);
        Assert.Equal(before, await SnapshotAsync());
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Preview_ChargesEachChangedNumericPriceAndClearsSurchargeForNoAvailability()
    {
        // Arrange
        var offer = AddOffer("services.example", PricingTerm.Unknown);
        offer.Prices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Casino,
            AvailabilityStatus = ServiceAvailabilityStatus.Available, SourceCurrency = "USD", SourceAmount = 20m,
            WebmasterPriceUsd = 20m, AppliedUsdRate = 1m
        });
        offer.Prices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.LinkInsertion,
            AvailabilityStatus = ServiceAvailabilityStatus.Available, SourceCurrency = "CAD", SourceAmount = 10m,
            WebmasterPriceUsd = 13m, AppliedUsdRate = 0.8m, AppliedUsdSurcharge = 5m
        });
        OfferCurrencyTestData.AddSnapshot(_context, 0.8m, DateTime.UtcNow);
        await _context.SaveChangesAsync();
        var before = await SnapshotAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [0, 1, 4],
            ("services.example", ["100", "50", "NO"])) with { SourceCurrency = "CAD" });

        // Assert
        Assert.Equal(1, result.ReadyCount);
        var changes = Assert.Single(result.Rows).Prices;
        Assert.Equal(85m, changes[0].Proposed.WebmasterPriceUsd);
        Assert.Equal(45m, changes[1].Proposed.WebmasterPriceUsd);
        Assert.Equal(5m, changes[0].Proposed.AppliedUsdSurcharge);
        Assert.Equal(5m, changes[1].Proposed.AppliedUsdSurcharge);
        Assert.Equal(5m, changes[2].Current!.AppliedUsdSurcharge);
        Assert.Equal(0m, changes[2].Proposed.AppliedUsdSurcharge);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, changes[2].Proposed.AvailabilityStatus);
        Assert.Null(changes[2].Proposed.WebmasterPriceUsd);
        Assert.Null(changes[2].Proposed.SourceAmount);
        Assert.Null(changes[2].Proposed.AppliedUsdRate);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData("0.01", null)]
    [InlineData("9999999999999994", "9999999999999999")]
    [InlineData("9999999999999995", null)]
    public async Task Preview_ValidatesRoundedAmountBeforeSurchargeAndFinalUsdBounds(string amount, string? expectedUsd)
    {
        // Arrange
        AddOffer("bounds.example", PricingTerm.Unknown);
        OfferCurrencyTestData.AddSnapshot(_context, 1m, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [0], ("bounds.example", [amount])) with { SourceCurrency = "CAD" });

        // Assert
        var row = Assert.Single(result.Rows);
        if (expectedUsd is null)
        {
            Assert.Equal("Invalid converted amount", row.Category);
            Assert.Equal(0, result.ReadyCount);
        }
        else
        {
            Assert.Equal(1, result.ReadyCount);
            var price = Assert.Single(row.Prices).Proposed;
            Assert.Equal(decimal.Parse(expectedUsd, System.Globalization.CultureInfo.InvariantCulture), price.WebmasterPriceUsd);
            Assert.Equal(5m, price.AppliedUsdSurcharge);
        }
    }

    [Theory]
    [InlineData("CAD", "10", "Conversion unavailable")]
    [InlineData("EUR", "0.01", "Invalid converted amount")]
    [InlineData("GBP", "9999999999999999.99", "Invalid converted amount")]
    public async Task Preview_ReportsMissingRateAndConvertedBounds(string currency, string input, string expected)
    {
        // Arrange
        AddOffer("currency.example", PricingTerm.Unknown);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [0], ("currency.example", [input])) with { SourceCurrency = currency });

        // Assert
        Assert.Equal(expected, Assert.Single(result.Rows).Category);
        Assert.Equal(0, result.ReadyCount);
    }

    [Fact]
    public async Task Preview_PreservesHistoricQuoteForNoOp_ButSourceCurrencyChangeIsRealEvenWhenUsdMatches()
    {
        // Arrange
        var unchanged = AddOffer("unchanged.example", PricingTerm.Unknown);
        var current = unchanged.Prices.Single();
        current.SourceCurrency = "EUR";
        current.SourceAmount = 10m;
        current.AppliedUsdRate = 1.1m;
        current.WebmasterPriceUsd = 11m;
        AddOffer("changed.example", PricingTerm.Unknown, amount: 13m);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [0],
            ("unchanged.example", ["10"]), ("changed.example", ["10"])) with { SourceCurrency = "EUR" });

        // Assert
        Assert.Equal("No changes", result.Rows[0].Outcome);
        Assert.Equal(11m, result.Rows[0].Prices[0].Proposed.WebmasterPriceUsd);
        Assert.Equal("Ready", result.Rows[1].Outcome);
        Assert.Equal(13m, result.Rows[1].Prices[0].Proposed.WebmasterPriceUsd);
        Assert.True(result.Rows[1].Prices[0].Changed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50001)]
    public async Task Preview_EnforcesRawInputCeilingBeforeDuplicateCollapse(int count)
    {
        // Arrange
        var request = Request(PricingTerm.Unknown, [0]) with { Rows = Enumerable.Range(1, count)
            .Select(number => new BulkOfferPriceInputRow(number, "repeat.example", ["10"])).ToArray() };

        // Act
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().PreviewAsync(request));

        // Assert
        Assert.Contains("50,000", error.Message);
    }

    [Fact]
    public async Task Preview_AcceptsCeilingWithoutRenderingOrQueryingPerInput()
    {
        // Arrange
        var request = Request(PricingTerm.Unknown, [0]) with { Rows = Enumerable.Range(1, 50000)
            .Select(number => new BulkOfferPriceInputRow(number, "repeat.example", ["10"])).ToArray() };

        // Act
        var result = await CreateService().PreviewAsync(request);

        // Assert
        Assert.Single(result.Rows);
        Assert.Equal(49999, result.DuplicateInputCount);
        Assert.Equal(1, result.ExceptionCount);
    }

    [Fact]
    public async Task Preview_RequiresEverySelectedColumnAndRejectsExtraCells()
    {
        // Arrange
        AddOffer("missing.example", PricingTerm.Unknown);
        AddOffer("extra.example", PricingTerm.Unknown);
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [0, 1],
            ("missing.example", ["10"]), ("extra.example", ["10", "YES", "20"])));

        // Assert
        Assert.Equal(2, result.ExceptionCount);
        Assert.All(result.Rows, row => Assert.Equal("Invalid selected value", row.Category));
    }

    [Theory]
    [InlineData("Contact instructions", true)]
    [InlineData("contact instructions", false)]
    public async Task Preview_ContactInstructionsRetainCase(string contact, bool expectedMatch)
    {
        // Arrange
        AddOffer("instructions.example", PricingTerm.Unknown, contact: "Contact instructions");
        await _context.SaveChangesAsync();

        // Act
        var result = await CreateService().PreviewAsync(Request(PricingTerm.Unknown, [0], ("instructions.example", ["10"])) with { PrimaryEmail = contact });

        // Assert
        Assert.Equal(expectedMatch ? "No changes" : "Exceptions", Assert.Single(result.Rows).Outcome);
    }

    [Theory]
    [InlineData(null, 1, null)]
    [InlineData(null, null, TermUnit.Year)]
    [InlineData(TermType.Permanent, 1, null)]
    [InlineData(TermType.Permanent, null, TermUnit.Year)]
    [InlineData(TermType.Finite, 0, TermUnit.Year)]
    [InlineData(TermType.Finite, -1, TermUnit.Year)]
    [InlineData(TermType.Finite, 1, null)]
    [InlineData(TermType.Finite, 1, (TermUnit)99)]
    [InlineData((TermType)99, null, null)]
    public async Task Preview_RejectsMalformedRequestTermBeforeReadingOffers(TermType? type, int? value, TermUnit? unit)
    {
        // Arrange
        var request = Request(PricingTerm.Unknown, [0], ("term.example", ["10"])) with
        {
            TermType = type, TermValue = value, TermUnit = unit
        };

        // Act
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().PreviewAsync(request));

        // Assert
        Assert.Contains("Price-list term", error.Message);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    private SiteWebmasterOffer AddOffer(string domain, PricingTerm term, string contact = "contact@example.com", decimal amount = 10m)
    {
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = domain, ImportFingerprint = Guid.NewGuid().ToString("N"),
            ContactRawText = "import evidence " + Guid.NewGuid(), TermRawText = "source term", Status = SiteWebmasterOfferStatus.Active,
            Site = _context.Sites.Local.FirstOrDefault(site => site.Domain == domain) ?? new Site { Domain = domain },
            Webmaster = new Webmaster { Id = Guid.NewGuid(), PrimaryEmail = contact, ContactRawText = Guid.NewGuid().ToString(), NormalizedContactRawText = Guid.NewGuid().ToString() },
            TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit,
            Prices = [new WebmasterOfferPrice { Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main,
                AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = amount,
                SourceCurrency = "USD", SourceAmount = amount, AppliedUsdRate = 1m, WebmasterPriceDetails = "keep payment details",
                TermType = term.TermType, TermValue = term.TermValue, TermUnit = term.TermUnit }]
        };
        _context.SiteWebmasterOffers.Add(offer);
        return offer;
    }

    private static PricingTerm Term(string term) => term switch
    {
        "unknown" => PricingTerm.Unknown, "permanent" => PricingTerm.Permanent, _ => PricingTerm.FiniteYears(1)
    };

    private static BulkOfferPricePreviewRequest Request(PricingTerm term, int[] types, params (string Domain, string[] Values)[] rows)
        => new("contact@example.com", term.TermType, term.TermValue, term.TermUnit, "USD",
            types.Select(type => (WebmasterOfferPriceType)type).ToArray(),
            rows.Select((row, index) => new BulkOfferPriceInputRow(index + 1, row.Domain, row.Values)).ToArray());

    private async Task<string> SnapshotAsync() => JsonSerializer.Serialize(new
    {
        Sites = await _context.Sites.AsNoTracking().ToListAsync(),
        Offers = await _context.SiteWebmasterOffers.AsNoTracking().ToListAsync(),
        Prices = await _context.WebmasterOfferPrices.AsNoTracking().ToListAsync(),
        Webmasters = await _context.Webmasters.AsNoTracking().ToListAsync(),
        History = await _context.EntityChangeHistories.AsNoTracking().ToListAsync(),
        CatalogPrices = await _context.SitePriceOptions.AsNoTracking().ToListAsync()
    }, new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles });
}
