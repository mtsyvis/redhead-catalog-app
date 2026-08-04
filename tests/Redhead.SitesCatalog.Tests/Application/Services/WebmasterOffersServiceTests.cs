using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterOffersServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly WebmasterOffersService _sut;

    public WebmasterOffersServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sut = new WebmasterOffersService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetByDomainAsync_ReturnsRawPriceAvailabilityStatus()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmasterId = Guid.NewGuid();
        var offerId = Guid.NewGuid();

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
        _context.Webmasters.Add(new Webmaster
        {
            Id = webmasterId,
            ContactRawText = "contact@example.com",
            NormalizedContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.SiteWebmasterOffers.Add(new SiteWebmasterOffer
        {
            Id = offerId,
            SiteDomain = "existing.com",
            WebmasterId = webmasterId,
            ImportFingerprint = new string('a', 64),
            ContactRawText = "contact@example.com",
            DfLinksRawText = "2 DF links",
            SponsoredTagRawText = "Sponsored",
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.WebmasterOfferPrices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(),
            SiteWebmasterOfferId = offerId,
            PriceType = WebmasterOfferPriceType.Casino,
            AvailabilityStatus = ServiceAvailabilityStatus.AvailableWithUnknownPrice,
            WebmasterPriceUsd = null,
            WebmasterPriceDetails = "ask webmaster",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByDomainAsync("existing.com");

        // Assert
        var offer = Assert.Single(result.Offers);
        Assert.Equal("contact@example.com", offer.PrimaryEmail);
        Assert.Equal("2 DF links", offer.DfLinksRawText);
        Assert.Equal("Sponsored", offer.SponsoredTagRawText);
        var price = Assert.Single(offer.Prices);
        Assert.Equal(WebmasterOfferPriceType.Casino, price.PriceType);
        Assert.Equal(ServiceAvailabilityStatus.AvailableWithUnknownPrice, price.AvailabilityStatus);
        Assert.Null(price.WebmasterPriceUsd);
    }

    [Fact]
    public async Task GetByDomainAsync_WithoutPrimaryEmail_ReturnsNullPrimaryEmail()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmasterId = Guid.NewGuid();

        _context.Sites.Add(new Site
        {
            Domain = "no-primary.com",
            DR = 40,
            Traffic = 5000,
            Location = "US",
            IsQuarantined = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.Webmasters.Add(new Webmaster
        {
            Id = webmasterId,
            ContactRawText = "first@example.com\nsecond@example.com",
            NormalizedContactRawText = "first@example.com second@example.com",
            PrimaryEmail = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.SiteWebmasterOffers.Add(new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = "no-primary.com",
            WebmasterId = webmasterId,
            ImportFingerprint = new string('b', 64),
            ContactRawText = "first@example.com\nsecond@example.com",
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByDomainAsync("no-primary.com");

        // Assert
        var offer = Assert.Single(result.Offers);
        Assert.Null(offer.PrimaryEmail);
    }

    [Fact]
    public async Task GetByDomainAsync_WithPermanentTerm_ReturnsPermanentTermLabels()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmasterId = Guid.NewGuid();
        var offerId = Guid.NewGuid();

        _context.Sites.Add(new Site
        {
            Domain = "permanent.com",
            DR = 40,
            Traffic = 5000,
            Location = "US",
            IsQuarantined = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.Webmasters.Add(new Webmaster
        {
            Id = webmasterId,
            ContactRawText = "contact@example.com",
            NormalizedContactRawText = "contact@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.SiteWebmasterOffers.Add(new SiteWebmasterOffer
        {
            Id = offerId,
            SiteDomain = "permanent.com",
            WebmasterId = webmasterId,
            ImportFingerprint = new string('c', 64),
            ContactRawText = "contact@example.com",
            TermType = TermType.Permanent,
            TermValue = null,
            TermUnit = null,
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.WebmasterOfferPrices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(),
            SiteWebmasterOfferId = offerId,
            PriceType = WebmasterOfferPriceType.Main,
            AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = 100m,
            TermType = TermType.Permanent,
            TermValue = null,
            TermUnit = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByDomainAsync("permanent.com");

        // Assert
        var offer = Assert.Single(result.Offers);
        Assert.Equal("Permanent", offer.TermLabel);
        var price = Assert.Single(offer.Prices);
        Assert.Equal("Permanent", price.TermLabel);
    }

    [Fact]
    public async Task GetByDomainAsync_SortsByMainPriceWithLowestOtherPriceAsFallback()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmasterId = Guid.NewGuid();
        var fallbackOfferId = Guid.NewGuid();
        var lowerMainOfferId = Guid.NewGuid();
        var higherMainOfferId = Guid.NewGuid();
        var noNumericPriceOfferId = Guid.NewGuid();

        _context.Sites.Add(new Site
        {
            Domain = "sorted.com",
            DR = 40,
            Traffic = 5000,
            Location = "US",
            IsQuarantined = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.Webmasters.Add(new Webmaster
        {
            Id = webmasterId,
            ContactRawText = "contact@example.com",
            NormalizedContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _context.SiteWebmasterOffers.AddRange(
            CreateOffer(fallbackOfferId, "d", webmasterId, now),
            CreateOffer(lowerMainOfferId, "e", webmasterId, now),
            CreateOffer(higherMainOfferId, "f", webmasterId, now),
            CreateOffer(noNumericPriceOfferId, "0", webmasterId, now));
        _context.WebmasterOfferPrices.AddRange(
            CreatePrice(fallbackOfferId, WebmasterOfferPriceType.Casino, 100m, now),
            CreatePrice(fallbackOfferId, WebmasterOfferPriceType.Crypto, 120m, now),
            CreatePrice(lowerMainOfferId, WebmasterOfferPriceType.Main, 165m, now),
            CreatePrice(higherMainOfferId, WebmasterOfferPriceType.Main, 220m, now),
            CreatePrice(higherMainOfferId, WebmasterOfferPriceType.Casino, 50m, now),
            new WebmasterOfferPrice
            {
                Id = Guid.NewGuid(),
                SiteWebmasterOfferId = noNumericPriceOfferId,
                PriceType = WebmasterOfferPriceType.Casino,
                AvailabilityStatus = ServiceAvailabilityStatus.NotAvailable,
                WebmasterPriceUsd = null,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByDomainAsync("sorted.com");

        // Assert
        Assert.Equal(
            [fallbackOfferId, lowerMainOfferId, higherMainOfferId, noNumericPriceOfferId],
            result.Offers.Select(offer => offer.Id));
    }

    private static SiteWebmasterOffer CreateOffer(
        Guid id,
        string fingerprintCharacter,
        Guid webmasterId,
        DateTime now)
        => new()
        {
            Id = id,
            SiteDomain = "sorted.com",
            WebmasterId = webmasterId,
            ImportFingerprint = new string(fingerprintCharacter[0], 64),
            ContactRawText = "contact@example.com",
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static WebmasterOfferPrice CreatePrice(
        Guid offerId,
        WebmasterOfferPriceType priceType,
        decimal amount,
        DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            SiteWebmasterOfferId = offerId,
            PriceType = priceType,
            AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = amount,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
}
