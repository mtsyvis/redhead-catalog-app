using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
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
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "original@example.com",
            PrimaryEmail = "original@example.com",
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

    [Fact]
    public async Task GetForEditAsync_SharedWebmaster_ReturnsPrimaryEmailScopeMetadata()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(),
            ContactRawText = "shared@example.com",
            NormalizedContactRawText = "shared@example.com",
            PrimaryEmail = "shared@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.Sites.Add(new Site
        {
            Domain = "shared.com",
            Location = "US",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        var selectedOffer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = "shared.com",
            Webmaster = webmaster,
            ImportFingerprint = new string('a', 64),
            ContactRawText = webmaster.ContactRawText,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.SiteWebmasterOffers.AddRange(
            selectedOffer,
            new SiteWebmasterOffer
            {
                Id = Guid.NewGuid(),
                SiteDomain = "shared.com",
                Webmaster = webmaster,
                ImportFingerprint = new string('b', 64),
                ContactRawText = webmaster.ContactRawText,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetForEditAsync(selectedOffer.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(webmaster.Version, result.WebmasterVersion);
        Assert.Equal(2, result.WebmasterOfferCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public async Task UpdateAsync_EmptyPrimaryEmail_RejectsWithoutChangingDataOrHistory(string? primary)
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "first@example.com second@example.com (reply)",
            NormalizedContactRawText = "first@example.com second@example.com (reply)",
            PrimaryEmail = "chosen@example.com", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.Sites.Add(new Site
        {
            Domain = "required-primary.com", Location = "US", CreatedAtUtc = now, UpdatedAtUtc = now
        });
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = "required-primary.com", Webmaster = webmaster,
            ImportFingerprint = new string('r', 64), ContactRawText = webmaster.ContactRawText,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.SiteWebmasterOffers.Add(offer);
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ContactRawText = "changed@example.com", PrimaryEmail = primary,
            Status = SiteWebmasterOfferStatus.Inactive, CommentText = "must not be saved"
        };

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => _sut.UpdateAsync(offer.Id, request, "editor@example.com"));

        // Assert
        Assert.Equal("Primary email is required.", exception.Message);
        _context.ChangeTracker.Clear();
        var savedWebmaster = await _context.Webmasters.SingleAsync();
        var savedOffer = await _context.SiteWebmasterOffers.SingleAsync();
        Assert.Equal("chosen@example.com", savedWebmaster.PrimaryEmail);
        Assert.Equal(now, savedWebmaster.UpdatedAtUtc);
        Assert.Equal(webmaster.ContactRawText, savedOffer.ContactRawText);
        Assert.Equal(offer.Status, savedOffer.Status);
        Assert.Null(savedOffer.CommentText);
        Assert.Equal(now, savedOffer.UpdatedAtUtc);
        Assert.Empty(await _context.EntityChangeHistories.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_PrimaryEmailChange_UpdatesSharedWebmasterAndHistory()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "shared@example.com", NormalizedContactRawText = "shared@example.com",
            PrimaryEmail = "shared@example.com", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.Sites.Add(new Site
        {
            Domain = "shared-primary.com", Location = "US", CreatedAtUtc = now, UpdatedAtUtc = now
        });
        var selectedOffer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = "shared-primary.com", Webmaster = webmaster,
            ImportFingerprint = new string('g', 64), ContactRawText = webmaster.ContactRawText,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.SiteWebmasterOffers.AddRange(
            selectedOffer,
            new SiteWebmasterOffer
            {
                Id = Guid.NewGuid(), SiteDomain = "shared-primary.com", Webmaster = webmaster,
                ImportFingerprint = new string('h', 64), ContactRawText = webmaster.ContactRawText,
                CreatedAtUtc = now, UpdatedAtUtc = now
            });
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = webmaster.ContactRawText,
            PrimaryEmail = "corrected@example.com",
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var result = await _sut.UpdateAsync(selectedOffer.Id, request, "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        _context.ChangeTracker.Clear();
        var savedWebmaster = await _context.Webmasters.SingleAsync();
        Assert.Equal("corrected@example.com", savedWebmaster.PrimaryEmail);
        Assert.True(savedWebmaster.UpdatedAtUtc > now);
        var offers = await _sut.GetByDomainAsync("shared-primary.com");
        Assert.All(offers.Offers, offer => Assert.Equal("corrected@example.com", offer.PrimaryEmail));
        var history = Assert.Single(await _context.EntityChangeHistories.ToListAsync());
        Assert.Contains("Primary email", history.ChangesJson, StringComparison.Ordinal);
        Assert.Contains("corrected@example.com", history.ChangesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_OfferOnlyChange_DoesNotTouchWebmaster()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(),
            ContactRawText = "future@example.com",
            NormalizedContactRawText = "future@example.com",
            PrimaryEmail = "future@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Version = 7
        };
        _context.Sites.Add(new Site
        {
            Domain = "future-version.com",
            Location = "US",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = "future-version.com",
            Webmaster = webmaster,
            ImportFingerprint = new string('v', 64),
            ContactRawText = webmaster.ContactRawText,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Version = 3
        };
        _context.SiteWebmasterOffers.Add(offer);
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 3,
            ExpectedWebmasterVersion = 7,
            ContactRawText = webmaster.ContactRawText,
            PrimaryEmail = webmaster.PrimaryEmail,
            Status = SiteWebmasterOfferStatus.Active,
            CommentText = "Changed"
        };

        // Act
        var result = await _sut.UpdateAsync(offer.Id, request, "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        Assert.Equal(7U, webmaster.Version);
        Assert.Equal(now, webmaster.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ChangedContact_CreatesWebmasterAndRelinksOnlySelectedOffer()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalWebmaster = new Webmaster
        {
            Id = Guid.NewGuid(),
            ContactRawText = "old@example.com",
            NormalizedContactRawText = "old@example.com",
            PrimaryEmail = "old@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.Sites.Add(new Site
        {
            Domain = "contact-change.com",
            Location = "US",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        var selectedOffer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = "contact-change.com",
            Webmaster = originalWebmaster,
            ImportFingerprint = new string('c', 64),
            ContactRawText = "old@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var siblingOffer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = "contact-change.com",
            Webmaster = originalWebmaster,
            ImportFingerprint = new string('d', 64),
            ContactRawText = "old@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.SiteWebmasterOffers.AddRange(selectedOffer, siblingOffer);
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "New@Example.com\nnew@example.com - reply",
            PrimaryEmail = "new@example.com",
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var result = await _sut.UpdateAsync(selectedOffer.Id, request, "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        _context.ChangeTracker.Clear();
        var updated = await _context.SiteWebmasterOffers.SingleAsync(offer => offer.Id == selectedOffer.Id);
        var unchangedSibling = await _context.SiteWebmasterOffers.SingleAsync(offer => offer.Id == siblingOffer.Id);
        Assert.NotEqual(originalWebmaster.Id, updated.WebmasterId);
        Assert.Equal(originalWebmaster.Id, unchangedSibling.WebmasterId);
        Assert.Equal(new string('c', 64), updated.ImportFingerprint);
        Assert.Equal("New@Example.com\nnew@example.com - reply", updated.ContactRawText);
        var createdWebmaster = await _context.Webmasters.SingleAsync(item => item.Id == updated.WebmasterId);
        var savedOriginalWebmaster = await _context.Webmasters.SingleAsync(item => item.Id == originalWebmaster.Id);
        Assert.Equal("new@example.com new@example.com - reply", createdWebmaster.NormalizedContactRawText);
        Assert.Equal("new@example.com", createdWebmaster.PrimaryEmail);
        Assert.Equal(now, savedOriginalWebmaster.UpdatedAtUtc);
        var history = Assert.Single(await _context.EntityChangeHistories.ToListAsync());
        Assert.Contains("Primary email", history.ChangesJson, StringComparison.Ordinal);
        Assert.Contains("New@Example.com", history.ChangesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_ContactFormattingChange_DoesNotCreateAnotherWebmaster()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(),
            ContactRawText = "contact@example.com",
            NormalizedContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.Sites.Add(new Site
        {
            Domain = "formatting-change.com",
            Location = "US",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = "formatting-change.com",
            Webmaster = webmaster,
            ImportFingerprint = new string('w', 64),
            ContactRawText = webmaster.ContactRawText,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.SiteWebmasterOffers.Add(offer);
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "CONTACT@EXAMPLE.COM",
            PrimaryEmail = webmaster.PrimaryEmail,
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var result = await _sut.UpdateAsync(offer.Id, request, "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        Assert.Equal(webmaster.Id, offer.WebmasterId);
        Assert.Equal("CONTACT@EXAMPLE.COM", offer.ContactRawText);
        Assert.Equal(1, await _context.Webmasters.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ChangedContact_ReusesExistingWebmasterWithoutOverwritingPrimaryEmail()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalWebmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "old@example.com", NormalizedContactRawText = "old@example.com",
            PrimaryEmail = "old@example.com", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var targetWebmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "target@example.com", NormalizedContactRawText = "target@example.com",
            PrimaryEmail = "target@example.com", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.Sites.Add(new Site
        {
            Domain = "existing-target.com", Location = "US", CreatedAtUtc = now, UpdatedAtUtc = now
        });
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = "existing-target.com", Webmaster = originalWebmaster,
            ImportFingerprint = new string('e', 64), ContactRawText = "old@example.com",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.Webmasters.Add(targetWebmaster);
        _context.SiteWebmasterOffers.Add(offer);
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "target@example.com",
            PrimaryEmail = "target@example.com",
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var result = await _sut.UpdateAsync(offer.Id, request, "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        _context.ChangeTracker.Clear();
        Assert.Equal(targetWebmaster.Id, (await _context.SiteWebmasterOffers.SingleAsync()).WebmasterId);
        Assert.Equal("target@example.com", (await _context.Webmasters.SingleAsync(item => item.Id == targetWebmaster.Id)).PrimaryEmail);
    }

    [Fact]
    public async Task UpdateAsync_ChangedContactMatchingExistingWebmaster_WithDifferentPrimaryEmailRejectsRequest()
    {
        // Arrange
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalWebmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "old@example.com", NormalizedContactRawText = "old@example.com",
            PrimaryEmail = "old@example.com", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var targetWebmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "target@example.com", NormalizedContactRawText = "target@example.com",
            PrimaryEmail = "target@example.com", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.Sites.Add(new Site
        {
            Domain = "primary-conflict.com", Location = "US", CreatedAtUtc = now, UpdatedAtUtc = now
        });
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = "primary-conflict.com", Webmaster = originalWebmaster,
            ImportFingerprint = new string('f', 64), ContactRawText = "old@example.com",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _context.Webmasters.Add(targetWebmaster);
        _context.SiteWebmasterOffers.Add(offer);
        await _context.SaveChangesAsync();
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "target@example.com",
            PrimaryEmail = "different@example.com",
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _sut.UpdateAsync(offer.Id, request, "editor@example.com"));

        // Assert
        Assert.Contains("existing webmaster", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalWebmaster.Id, offer.WebmasterId);
        Assert.Equal("target@example.com", targetWebmaster.PrimaryEmail);
        Assert.Empty(_context.EntityChangeHistories);
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
            PrimaryEmail = "contact@example.com",
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
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
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
            ExpectedVersion = 0,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
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
            .Include(offer => offer.Webmaster)
            .Include(offer => offer.Prices)
            .SingleAsync(offer => offer.Id == offerId);
        Assert.Equal(originalTime, unchanged.UpdatedAtUtc);
        Assert.Equal(originalTime, unchanged.Webmaster.UpdatedAtUtc);
        Assert.Null(unchanged.UpdatedBy);
        Assert.Equal(originalTime, Assert.Single(unchanged.Prices).UpdatedAtUtc);
        Assert.Empty(_context.EntityChangeHistories);
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ReturnsConflictWithoutChanges()
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
            ExpectedVersion = 1,
            ExpectedWebmasterVersion = 0,
            ContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
            Status = SiteWebmasterOfferStatus.Inactive
        };

        // Act
        var result = await _sut.UpdateAsync(offerId, request, "admin@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Conflict, result.Status);
        var offer = await _context.SiteWebmasterOffers.SingleAsync(item => item.Id == offerId);
        Assert.Equal(SiteWebmasterOfferStatus.Active, offer.Status);
        Assert.Equal("contact@example.com", offer.ContactRawText);
        Assert.Equal(0U, offer.Version);
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
