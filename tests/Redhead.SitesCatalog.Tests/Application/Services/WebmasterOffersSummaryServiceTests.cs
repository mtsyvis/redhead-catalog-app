using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterOffersSummaryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static readonly DateTime CreatedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetSummaryByDomainAsync_ReturnsAllowedFieldsWithoutWebmasterContactsOrPrices()
    {
        // Arrange
        var offer = CreateOffer(Guid.NewGuid(), CreatedAt, 100m);
        offer.Status = SiteWebmasterOfferStatus.Inactive;
        offer.OutreachSenderRawText = "Outreach team";
        offer.LinkbuilderMailboxRawText = "alex@agency.example";
        offer.LinkPolicyText = "Dofollow only";
        offer.DfLinksRawText = "2 DF links";
        offer.SponsoredTagRawText = "No sponsored tag";
        offer.CommentText = "Editorial review\nOne revision included";
        offer.ClientRawText = "Client Alpha";
        offer.TermRawText = "24 months";
        offer.TermType = TermType.Finite;
        offer.TermValue = 2;
        offer.TermUnit = TermUnit.Year;
        offer.UpdatedBy = "editor@agency.example";
        offer.LinkbuilderMailboxes.Add(new SiteWebmasterOfferLinkbuilderMailbox
        {
            LinkbuilderMailbox = new LinkbuilderMailbox
            {
                Id = Guid.NewGuid(), Email = "alex@agency.example", DisplayName = "Alex",
                CreatedAtUtc = CreatedAt, UpdatedAtUtc = CreatedAt
            },
            Source = LinkbuilderMailboxOfferSource.Manual,
            CreatedAtUtc = CreatedAt
        });
        _context.SiteWebmasterOffers.Add(offer);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var service = new WebmasterOffersService(_context);

        // Act
        var result = await service.GetSummaryByDomainAsync("https://www.EXAMPLE.org/path");
        var serialized = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Assert
        Assert.True(result.SiteFound);
        Assert.Equal("example.org", result.Domain);
        var summary = Assert.Single(result.Offers);
        Assert.Equal(offer.Id, summary.Id);
        Assert.Equal(offer.Status, summary.Status);
        Assert.Equal(offer.OutreachSenderRawText, summary.OutreachSenderRawText);
        Assert.Equal(offer.LinkbuilderMailboxRawText, summary.LinkbuilderMailboxRawText);
        Assert.Equal(offer.LinkPolicyText, summary.LinkPolicyText);
        Assert.Equal(offer.DfLinksRawText, summary.DfLinksRawText);
        Assert.Equal(offer.SponsoredTagRawText, summary.SponsoredTagRawText);
        Assert.Equal(offer.CommentText, summary.CommentText);
        Assert.Equal(offer.ClientRawText, summary.ClientRawText);
        Assert.Equal(offer.TermRawText, summary.TermRawText);
        Assert.Equal(offer.TermType, summary.TermType);
        Assert.Equal(offer.TermValue, summary.TermValue);
        Assert.Equal(offer.TermUnit, summary.TermUnit);
        Assert.Equal("2 years", summary.TermLabel);
        Assert.Equal(offer.CreatedAtUtc, summary.CreatedAtUtc);
        Assert.Equal(offer.UpdatedAtUtc, summary.UpdatedAtUtc);
        Assert.Equal(offer.UpdatedBy, summary.UpdatedBy);
        var mailbox = Assert.Single(summary.LinkbuilderMailboxes);
        Assert.Equal("alex@agency.example", mailbox.Email);
        Assert.Equal("Alex", mailbox.DisplayName);
        Assert.Equal(LinkbuilderMailboxOfferSource.Manual, mailbox.Source);
        using var json = JsonDocument.Parse(serialized);
        var item = json.RootElement.GetProperty("offers")[0];
        foreach (var property in new[] { "primaryEmail", "contactRawText", "prices", "webmasterId", "webmaster", "importFingerprint" })
        {
            Assert.False(item.TryGetProperty(property, out _), property);
        }
        Assert.DoesNotContain("private-contact@publisher.example", serialized);
        Assert.DoesNotContain("private price details", serialized);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetSummaryByDomainAsync_SortsNewestFirstWithIdTieBreakWithoutUsingPrices()
    {
        // Arrange
        var earlierId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var laterId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var oldId = Guid.NewGuid();
        _context.SiteWebmasterOffers.AddRange(
            CreateOffer(laterId, CreatedAt.AddDays(1), 1m),
            CreateOffer(oldId, CreatedAt, 0.5m),
            CreateOffer(earlierId, CreatedAt.AddDays(1), 1000m));
        await _context.SaveChangesAsync();
        var service = new WebmasterOffersService(_context);

        // Act
        var result = await service.GetSummaryByDomainAsync("example.org");

        // Assert
        Assert.Equal([earlierId, laterId, oldId], result.Offers.Select(offer => offer.Id));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData("missing.example", "missing.example")]
    [InlineData("https://www.MISSING.example/path", "missing.example")]
    public async Task GetSummaryByDomainAsync_MissingOrEmptyDomain_ReturnsNotFound(string? input, string expectedDomain)
    {
        // Arrange
        var service = new WebmasterOffersService(_context);

        // Act
        var result = await service.GetSummaryByDomainAsync(input);

        // Assert
        Assert.False(result.SiteFound);
        Assert.Equal(expectedDomain, result.Domain);
        Assert.Empty(result.Offers);
    }

    [Fact]
    public async Task GetSummaryByDomainAsync_ExistingSiteWithoutOffers_ReturnsFoundWithEmptyOffers()
    {
        // Arrange
        _context.Sites.Add(CreateSite());
        await _context.SaveChangesAsync();
        var service = new WebmasterOffersService(_context);

        // Act
        var result = await service.GetSummaryByDomainAsync("example.org");

        // Assert
        Assert.True(result.SiteFound);
        Assert.Empty(result.Offers);
    }

    private SiteWebmasterOffer CreateOffer(Guid id, DateTime createdAt, decimal price)
    {
        var site = _context.Sites.Local.SingleOrDefault() ?? CreateSite();
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(), ContactRawText = "private-contact@publisher.example",
            NormalizedContactRawText = "private-contact@publisher.example", PrimaryEmail = "private-contact@publisher.example",
            CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt
        };
        var offer = new SiteWebmasterOffer
        {
            Id = id, SiteDomain = site.Domain, Site = site, Webmaster = webmaster,
            ContactRawText = "private-contact@publisher.example", ImportFingerprint = id.ToString("N").PadRight(64, 'a'),
            Status = SiteWebmasterOfferStatus.Active, CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt
        };
        offer.Prices.Add(new WebmasterOfferPrice
        {
            Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main,
            AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = price,
            WebmasterPriceDetails = "private price details", CreatedAtUtc = createdAt, UpdatedAtUtc = createdAt
        });
        _context.Sites.Add(site);
        return offer;
    }

    private static Site CreateSite() => new()
    {
        Domain = "example.org", CreatedAtUtc = CreatedAt, UpdatedAtUtc = CreatedAt
    };

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
