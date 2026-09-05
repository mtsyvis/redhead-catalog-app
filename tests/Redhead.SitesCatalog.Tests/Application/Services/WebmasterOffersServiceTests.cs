using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
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

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesOfferAndWritesHistoryWithoutChangingImportedIdentityData()
    {
        // Arrange
        var originalTime = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmasterId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        const string fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        _context.Sites.Add(new Site
        {
            Domain = "edit.com",
            DR = 30,
            Traffic = 1000,
            Location = "US",
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.Webmasters.Add(new Webmaster
        {
            Id = webmasterId,
            ContactRawText = "original@example.com",
            NormalizedContactRawText = "original@example.com",
            PrimaryEmail = "original@example.com",
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.LinkbuilderMailboxes.Add(new LinkbuilderMailbox
        {
            Id = mailboxId,
            Email = "outreach@example.com",
            DisplayName = "Outreach",
            IsActive = true,
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.SiteWebmasterOffers.Add(new SiteWebmasterOffer
        {
            Id = offerId,
            SiteDomain = "edit.com",
            WebmasterId = webmasterId,
            ImportFingerprint = fingerprint,
            ContactRawText = "original@example.com",
            LinkbuilderMailboxRawText = "Imported Outreach Alias",
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.WebmasterOfferPrices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(),
            SiteWebmasterOfferId = offerId,
            PriceType = WebmasterOfferPriceType.Main,
            AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = 100m,
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = originalTime,
            Status = SiteWebmasterOfferStatus.Inactive,
            CommentText = "Updated conditions",
            TermType = TermType.Finite,
            TermValue = 2,
            TermUnit = TermUnit.Year,
            LinkbuilderMailboxIds = [mailboxId],
            Prices =
            [
                new UpdateWebmasterOfferPriceRequest
                {
                    PriceType = WebmasterOfferPriceType.Main,
                    AvailabilityStatus = ServiceAvailabilityStatus.Available,
                    WebmasterPriceUsd = 125m,
                    WebmasterPriceDetails = "new price"
                }
            ]
        };

        // Act
        var result = await _sut.UpdateAsync(offerId, request, "admin@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        var updated = await _context.SiteWebmasterOffers
            .Include(offer => offer.Prices)
            .Include(offer => offer.LinkbuilderMailboxes)
            .SingleAsync(offer => offer.Id == offerId);
        Assert.Equal(SiteWebmasterOfferStatus.Inactive, updated.Status);
        Assert.Equal("original@example.com", updated.ContactRawText);
        Assert.Equal("Imported Outreach Alias", updated.LinkbuilderMailboxRawText);
        Assert.Equal("admin@example.com", updated.UpdatedBy);
        Assert.Equal(fingerprint, updated.ImportFingerprint);
        Assert.Equal(webmasterId, updated.WebmasterId);
        Assert.Equal(125m, Assert.Single(updated.Prices).WebmasterPriceUsd);
        Assert.Single(updated.LinkbuilderMailboxes);

        var webmaster = await _context.Webmasters.SingleAsync(item => item.Id == webmasterId);
        Assert.Equal("original@example.com", webmaster.ContactRawText);
        Assert.Equal("original@example.com", webmaster.PrimaryEmail);

        var history = Assert.Single(_context.EntityChangeHistories);
        Assert.Equal(offerId.ToString("D"), history.EntityId);
        Assert.Equal("admin@example.com", history.ChangedBy);
        Assert.Contains("Updated conditions", history.ChangesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"field\":\"Contact\"", history.ChangesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"field\":\"Linkbuilder mailbox raw text\"", history.ChangesJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WebmasterOfferPriceType.Main, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.Casino, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.Crypto, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.Dating, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.LinkInsertion, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.LinkInsertion18Plus, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.Banner, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.Banner18Plus, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.HomepageTextLink, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.HomepageTextLink18Plus, ServiceAvailabilityStatus.Available, false)]
    [InlineData(WebmasterOfferPriceType.Casino, ServiceAvailabilityStatus.AvailableWithUnknownPrice, false)]
    [InlineData(WebmasterOfferPriceType.Casino, ServiceAvailabilityStatus.NotAvailable, false)]
    [InlineData(WebmasterOfferPriceType.Casino, ServiceAvailabilityStatus.Unknown, false)]
    [InlineData(WebmasterOfferPriceType.Main, ServiceAvailabilityStatus.Available, true)]
    [InlineData(WebmasterOfferPriceType.Casino, ServiceAvailabilityStatus.Available, true)]
    public async Task UpdateAsync_UnknownPrice_PersistsPriceAndHistory(
        WebmasterOfferPriceType priceType,
        ServiceAvailabilityStatus availabilityStatus,
        bool hasExistingPriceRow)
    {
        // Arrange
        var originalTime = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(),
            ContactRawText = "contact@example.com",
            NormalizedContactRawText = "contact@example.com",
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        };
        var offer = CreateOffer(Guid.NewGuid(), "a", webmaster.Id, originalTime);
        offer.Webmaster = webmaster;
        offer.TermType = TermType.Finite;
        offer.TermValue = 2;
        offer.TermUnit = TermUnit.Year;
        _context.Sites.Add(new Site
        {
            Domain = offer.SiteDomain,
            Location = "US",
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.SiteWebmasterOffers.Add(offer);
        Guid? existingPriceId = null;
        if (hasExistingPriceRow)
        {
            var existingPrice = CreatePrice(offer.Id, priceType, 100m, originalTime);
            existingPrice.AvailabilityStatus = ServiceAvailabilityStatus.Unknown;
            existingPrice.WebmasterPriceUsd = null;
            existingPrice.WebmasterPriceDetails = "Ask for price";
            existingPriceId = existingPrice.Id;
            _context.WebmasterOfferPrices.Add(existingPrice);
        }

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        decimal? amount = availabilityStatus == ServiceAvailabilityStatus.Available ? 169.50m : null;
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = originalTime,
            Status = SiteWebmasterOfferStatus.Active,
            TermType = offer.TermType,
            TermValue = offer.TermValue,
            TermUnit = offer.TermUnit,
            Prices =
            [
                new UpdateWebmasterOfferPriceRequest
                {
                    PriceType = priceType,
                    AvailabilityStatus = availabilityStatus,
                    WebmasterPriceUsd = amount,
                    WebmasterPriceDetails = "Updated price details"
                }
            ]
        };

        // Act
        var result = await _sut.UpdateAsync(offer.Id, request, "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        _context.ChangeTracker.Clear();
        var saved = await _context.SiteWebmasterOffers.Include(item => item.Prices).SingleAsync();
        var price = Assert.Single(saved.Prices);
        Assert.NotEqual(Guid.Empty, price.Id);
        if (existingPriceId.HasValue)
        {
            Assert.Equal(existingPriceId.Value, price.Id);
        }
        Assert.Equal(price.Id, Assert.Single(result.Offer!.Prices).Id);
        Assert.Equal(priceType, price.PriceType);
        Assert.Equal(availabilityStatus, price.AvailabilityStatus);
        Assert.Equal(amount, price.WebmasterPriceUsd);
        Assert.Equal("Updated price details", price.WebmasterPriceDetails);
        Assert.Equal(TermType.Finite, price.TermType);
        Assert.Equal(2, price.TermValue);
        Assert.Equal(TermUnit.Year, price.TermUnit);
        Assert.Equal(hasExistingPriceRow ? originalTime : saved.UpdatedAtUtc, price.CreatedAtUtc);
        Assert.Equal(saved.UpdatedAtUtc, price.UpdatedAtUtc);
        Assert.True(saved.UpdatedAtUtc > originalTime);
        Assert.Equal("editor@example.com", saved.UpdatedBy);
        var history = Assert.Single(await _context.EntityChangeHistories.ToListAsync());
        Assert.Equal(offer.Id.ToString("D"), history.EntityId);
        Assert.Equal("editor@example.com", history.ChangedBy);
        Assert.Contains("Updated price details", history.ChangesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_UnchangedRequest_DoesNotUpdateAuditFieldsOrWriteHistory()
    {
        // Arrange
        var originalTime = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmasterId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        _context.Sites.Add(new Site
        {
            Domain = "unchanged.com",
            DR = 30,
            Traffic = 1000,
            Location = "US",
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.Webmasters.Add(new Webmaster
        {
            Id = webmasterId,
            ContactRawText = "contact@example.com",
            NormalizedContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.SiteWebmasterOffers.Add(new SiteWebmasterOffer
        {
            Id = offerId,
            SiteDomain = "unchanged.com",
            WebmasterId = webmasterId,
            ImportFingerprint = new string('u', 64),
            ContactRawText = "contact@example.com",
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        _context.WebmasterOfferPrices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(),
            SiteWebmasterOfferId = offerId,
            PriceType = WebmasterOfferPriceType.Main,
            AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = 100m,
            CreatedAtUtc = originalTime,
            UpdatedAtUtc = originalTime
        });
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = originalTime,
            Status = SiteWebmasterOfferStatus.Active,
            Prices =
            [
                new UpdateWebmasterOfferPriceRequest
                {
                    PriceType = WebmasterOfferPriceType.Main,
                    AvailabilityStatus = ServiceAvailabilityStatus.Available,
                    WebmasterPriceUsd = 100m
                }
            ]
        };

        // Act
        var result = await _sut.UpdateAsync(offerId, request, "admin@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        _context.ChangeTracker.Clear();
        var unchanged = await _context.SiteWebmasterOffers
            .Include(offer => offer.Prices)
            .SingleAsync(offer => offer.Id == offerId);
        Assert.Equal(originalTime, unchanged.UpdatedAtUtc);
        Assert.Null(unchanged.UpdatedBy);
        Assert.Equal(originalTime, Assert.Single(unchanged.Prices).UpdatedAtUtc);
        Assert.Empty(_context.EntityChangeHistories);
    }

    [Fact]
    public async Task UpdateAsync_StaleTimestamp_ReturnsConflictWithoutChanges()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmasterId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        _context.Sites.Add(new Site
        {
            Domain = "conflict.com",
            DR = 30,
            Traffic = 1000,
            Location = "US",
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
            SiteDomain = "conflict.com",
            WebmasterId = webmasterId,
            ImportFingerprint = new string('c', 64),
            ContactRawText = "contact@example.com",
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = now.AddMinutes(-1),
            Status = SiteWebmasterOfferStatus.Inactive
        };

        // Act
        var result = await _sut.UpdateAsync(offerId, request, "admin@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Conflict, result.Status);
        var offer = await _context.SiteWebmasterOffers.SingleAsync(item => item.Id == offerId);
        Assert.Equal(SiteWebmasterOfferStatus.Active, offer.Status);
        Assert.Equal("contact@example.com", offer.ContactRawText);
        Assert.Empty(_context.EntityChangeHistories);
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
