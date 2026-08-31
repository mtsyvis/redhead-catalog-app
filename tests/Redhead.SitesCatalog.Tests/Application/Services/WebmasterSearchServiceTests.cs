using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services.WebmasterSearch;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterSearchServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly WebmasterSearchService _sut;

    public WebmasterSearchServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sut = new WebmasterSearchService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SearchAsync_ContactSubstringWithDifferentCase_GroupsByWebmasterAndReturnsTotalCounts()
    {
        // Arrange
        var older = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = older.AddDays(1);
        var firstWebmaster = AddWebmaster("Publisher TEAM <Sales@Example.com>", "sales@example.com");
        var secondWebmaster = AddWebmaster("Other EXAMPLE contact", null);
        AddSite("one.com");
        AddSite("two.com");
        AddSite("three.com");
        AddOffer(firstWebmaster.Id, "one.com", "Sales@Example.com", SiteWebmasterOfferStatus.Active, older);
        AddOffer(firstWebmaster.Id, "two.com", "legacy contact", SiteWebmasterOfferStatus.Inactive, newer);
        AddOffer(secondWebmaster.Id, "three.com", "OTHER example CONTACT", SiteWebmasterOfferStatus.Active, older);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SearchAsync("  ExAmPlE  ", 1, 20);

        // Assert
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        var first = Assert.Single(result.Items, item => item.WebmasterId == firstWebmaster.Id);
        Assert.Equal("sales@example.com", first.PrimaryEmail);
        Assert.Equal(2, first.OfferCount);
        Assert.Equal(1, first.ActiveOfferCount);
        Assert.Equal(2, first.DomainCount);
        Assert.Equal("Sales@Example.com", first.MatchingContactSnippet);
        Assert.Equal(newer, first.LatestOfferUpdatedAtUtc);
    }

    [Fact]
    public async Task SearchAsync_PaginatesWebmasterGroupsWithDeterministicNewestFirstOrdering()
    {
        // Arrange
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 3; index++)
        {
            var webmaster = AddWebmaster($"team-{index}@example.com", $"team-{index}@example.com");
            var domain = $"site-{index}.com";
            AddSite(domain);
            AddOffer(
                webmaster.Id,
                domain,
                $"team-{index}@example.com",
                SiteWebmasterOfferStatus.Active,
                start.AddDays(index));
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SearchAsync("example", 2, 1);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal("team-1@example.com", item.PrimaryEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public async Task SearchAsync_QueryShorterThanThreeCharacters_ReturnsEmptyResult(string? contact)
    {
        // Arrange
        var webmaster = AddWebmaster("ab@example.com", "ab@example.com");
        AddSite("short.com");
        AddOffer(
            webmaster.Id,
            "short.com",
            "ab@example.com",
            SiteWebmasterOfferStatus.Active,
            DateTime.UtcNow);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SearchAsync(contact, 1, 20);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task GetWorkspaceAsync_ReturnsAllOffersGroupedByDomainWithCatalogAndRawData()
    {
        // Arrange
        var now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        var webmaster = AddWebmaster(
            "first@example.com\nsecond@example.com",
            null);
        AddSite("available.com");
        AddSite("quarantined.com", isQuarantined: true, quarantineReason: "Temporarily unavailable");
        var firstOffer = AddOffer(
            webmaster.Id,
            "quarantined.com",
            "reply@example.com - answer",
            SiteWebmasterOfferStatus.Active,
            now,
            termRawText: "2 years",
            termType: TermType.Finite,
            termValue: 2,
            termUnit: TermUnit.Year);
        firstOffer.OutreachSenderRawText = "sender raw";
        firstOffer.LinkPolicyText = "dofollow";
        firstOffer.DfLinksRawText = "2 links";
        firstOffer.SponsoredTagRawText = "Sponsored";
        firstOffer.CommentText = "Imported comment";
        firstOffer.ClientRawText = "Client A";
        firstOffer.Prices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(),
            SiteWebmasterOfferId = firstOffer.Id,
            PriceType = WebmasterOfferPriceType.Main,
            AvailabilityStatus = ServiceAvailabilityStatus.Available,
            WebmasterPriceUsd = 125m,
            WebmasterPriceDetails = "raw main details",
            TermType = TermType.Finite,
            TermValue = 2,
            TermUnit = TermUnit.Year,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        firstOffer.Prices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(),
            SiteWebmasterOfferId = firstOffer.Id,
            PriceType = WebmasterOfferPriceType.Casino,
            AvailabilityStatus = ServiceAvailabilityStatus.NotAvailable,
            WebmasterPriceDetails = "not offered",
            TermType = TermType.Finite,
            TermValue = 2,
            TermUnit = TermUnit.Year,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        AddOffer(
            webmaster.Id,
            "quarantined.com",
            "older contact evidence",
            SiteWebmasterOfferStatus.Inactive,
            now.AddDays(-1));
        AddOffer(
            webmaster.Id,
            "available.com",
            "available contact evidence",
            SiteWebmasterOfferStatus.Active,
            now.AddDays(-2));
        var otherWebmaster = AddWebmaster("other@example.com", "other@example.com");
        AddOffer(
            otherWebmaster.Id,
            "quarantined.com",
            "other active contact",
            SiteWebmasterOfferStatus.Active,
            now.AddDays(-3));
        AddOffer(
            otherWebmaster.Id,
            "quarantined.com",
            "other inactive contact",
            SiteWebmasterOfferStatus.Inactive,
            now.AddDays(-4));
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetWorkspaceAsync(webmaster.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Webmaster.OfferCount);
        Assert.Equal(2, result.Webmaster.ActiveOfferCount);
        Assert.Equal(2, result.Webmaster.DomainCount);
        Assert.Null(result.Webmaster.PrimaryEmail);
        var quarantined = Assert.Single(result.Domains, domain => domain.Domain == "quarantined.com");
        Assert.True(quarantined.SiteFound);
        Assert.True(quarantined.IsQuarantined);
        Assert.Equal("Temporarily unavailable", quarantined.QuarantineReason);
        Assert.Equal(2, quarantined.Offers.Count);
        Assert.Equal(2, quarantined.OtherWebmasterOfferCount);
        var available = Assert.Single(result.Domains, domain => domain.Domain == "available.com");
        Assert.Equal(0, available.OtherWebmasterOfferCount);
        var mappedOffer = Assert.Single(quarantined.Offers, offer => offer.Id == firstOffer.Id);
        Assert.Equal("reply@example.com - answer", mappedOffer.ContactRawText);
        Assert.Equal("2 years", mappedOffer.TermLabel);
        Assert.Equal("sender raw", mappedOffer.OutreachSenderRawText);
        Assert.Equal(2, mappedOffer.Prices.Count);
        Assert.Contains(mappedOffer.Prices, price =>
            price.PriceType == WebmasterOfferPriceType.Main
            && price.WebmasterPriceUsd == 125m
            && price.AvailabilityStatus == ServiceAvailabilityStatus.Available);
        Assert.Contains(mappedOffer.Prices, price =>
            price.PriceType == WebmasterOfferPriceType.Casino
            && price.WebmasterPriceUsd is null
            && price.AvailabilityStatus == ServiceAvailabilityStatus.NotAvailable);
    }

    [Fact]
    public async Task GetWorkspaceAsync_UnknownWebmaster_ReturnsNull()
    {
        // Arrange

        // Act
        var result = await _sut.GetWorkspaceAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    private Webmaster AddWebmaster(string contact, string? primaryEmail)
    {
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(),
            ContactRawText = contact,
            NormalizedContactRawText = contact.ToLowerInvariant(),
            PrimaryEmail = primaryEmail,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Webmasters.Add(webmaster);
        return webmaster;
    }

    private void AddSite(
        string domain,
        bool isQuarantined = false,
        string? quarantineReason = null)
    {
        _context.Sites.Add(new Site
        {
            Domain = domain,
            DR = 30,
            Traffic = 1000,
            Location = "US",
            IsQuarantined = isQuarantined,
            QuarantineReason = quarantineReason,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    private SiteWebmasterOffer AddOffer(
        Guid webmasterId,
        string domain,
        string contact,
        SiteWebmasterOfferStatus status,
        DateTime updatedAtUtc,
        string? termRawText = null,
        TermType? termType = null,
        int? termValue = null,
        TermUnit? termUnit = null)
    {
        var offerId = Guid.NewGuid();
        var offer = new SiteWebmasterOffer
        {
            Id = offerId,
            SiteDomain = domain,
            WebmasterId = webmasterId,
            ImportFingerprint = offerId.ToString("N").PadRight(64, '0'),
            ContactRawText = contact,
            TermRawText = termRawText,
            TermType = termType,
            TermValue = termValue,
            TermUnit = termUnit,
            Status = status,
            CreatedAtUtc = updatedAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
        _context.SiteWebmasterOffers.Add(offer);
        return offer;
    }
}
