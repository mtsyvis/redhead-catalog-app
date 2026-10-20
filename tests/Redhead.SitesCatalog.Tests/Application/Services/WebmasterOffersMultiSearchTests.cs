using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterOffersMultiSearchTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static readonly DateTime Now = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MultiSearchAsync_NormalizesDeduplicatesAndSeparatesMissingDomainsInInputOrder()
    {
        // Arrange
        var expensive = CreateOffer("first.example", 200m);
        var cheap = CreateOffer("first.example", 50m);
        cheap.Status = SiteWebmasterOfferStatus.Inactive;
        _context.Sites.AddRange(CreateSite("first.example"), CreateSite("second.example"), CreateSite("empty.example"));
        _context.SiteWebmasterOffers.AddRange(expensive, cheap, CreateOffer("second.example", 100m));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var service = new WebmasterOffersService(_context);

        // Act
        var result = await service.MultiSearchAsync(
            "https://www.SECOND.example/path first.example empty.example missing.example first.example other-missing.example");

        // Assert
        Assert.Equal(6, result.InputCount);
        Assert.Equal(5, result.UniqueDomainCount);
        Assert.Equal(["first.example"], result.Duplicates);
        Assert.Equal(["second.example", "first.example"], result.Results.Select(group => group.Domain));
        Assert.Equal([cheap.Id, expensive.Id], result.Results[1].Offers.Select(offer => offer.Id));
        Assert.Equal(SiteWebmasterOfferStatus.Inactive, result.Results[1].Offers[0].Status);
        Assert.Equal(["empty.example"], result.DomainsWithoutOffers);
        Assert.Equal(["missing.example", "other-missing.example"], result.NotFoundDomains);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MultiSearchSummaryAsync_DoesNotExposeSensitiveFieldsOrUsePriceOrdering()
    {
        // Arrange
        var cheap = CreateOffer("example.org", 1m);
        var newest = CreateOffer("example.org", 200m);
        newest.CreatedAtUtc = Now.AddDays(1);
        _context.Sites.AddRange(CreateSite("example.org"), CreateSite("empty.example"));
        _context.SiteWebmasterOffers.AddRange(cheap, newest);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var service = new WebmasterOffersService(_context);

        // Act
        var result = await service.MultiSearchSummaryAsync("https://www.EXAMPLE.org/path empty.example missing.example example.org");
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Assert
        Assert.Equal(3, result.UniqueDomainCount);
        Assert.Equal(["example.org"], result.Duplicates);
        Assert.Equal(["empty.example"], result.DomainsWithoutOffers);
        Assert.Equal(["missing.example"], result.NotFoundDomains);
        Assert.Equal([newest.Id, cheap.Id], Assert.Single(result.Results).Offers.Select(offer => offer.Id));
        using var document = JsonDocument.Parse(json);
        foreach (var offer in document.RootElement.GetProperty("results")[0].GetProperty("offers").EnumerateArray())
        {
            foreach (var field in new[] { "primaryEmail", "contactRawText", "prices", "webmasterId", "importFingerprint" })
            {
                Assert.False(offer.TryGetProperty(field, out _), field);
            }
        }
        Assert.DoesNotContain("private@publisher.example", json);
        Assert.DoesNotContain("Private pricing", json);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiSearch_EnforcesRawInputLimitBeforeDeduplication(bool summary)
    {
        // Arrange
        var service = new WebmasterOffersService(_context);
        var query = string.Join('\n', Enumerable.Repeat("example.org", MultiSearchConstants.MaxInputs + 1));

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(async () =>
        {
            if (summary)
            {
                await service.MultiSearchSummaryAsync(query);
            }
            else
            {
                await service.MultiSearchAsync(query);
            }
        });

        // Assert
        Assert.Contains("5000", exception.Message);
    }

    [Fact]
    public async Task MultiSearchAsync_AcceptsFiveThousandUniqueDomainsAndMatchesExactly()
    {
        // Arrange
        _context.Sites.Add(CreateSite("prefix-site0.example"));
        await _context.SaveChangesAsync();
        var service = new WebmasterOffersService(_context);
        var domains = Enumerable.Range(0, MultiSearchConstants.MaxInputs).Select(index => $"site{index}.example").ToArray();

        // Act
        var result = await service.MultiSearchAsync(string.Join(' ', domains));

        // Assert
        Assert.Equal(5000, result.UniqueDomainCount);
        Assert.Equal(domains, result.NotFoundDomains);
        Assert.Empty(result.Results);
        Assert.Empty(result.DomainsWithoutOffers);
    }

    private static Site CreateSite(string domain) => new() { Domain = domain, CreatedAtUtc = Now, UpdatedAtUtc = Now };

    private static SiteWebmasterOffer CreateOffer(string domain, decimal amount)
    {
        var id = Guid.NewGuid();
        return new SiteWebmasterOffer
        {
            Id = id, SiteDomain = domain,
            ContactRawText = "private@publisher.example",
            ImportFingerprint = id.ToString("N").PadRight(64, 'a'),
            Status = SiteWebmasterOfferStatus.Active, CreatedAtUtc = Now, UpdatedAtUtc = Now,
            Webmaster = new Webmaster
            {
                Id = Guid.NewGuid(), ContactRawText = "private@publisher.example",
                NormalizedContactRawText = id.ToString(), PrimaryEmail = "private@publisher.example",
                CreatedAtUtc = Now, UpdatedAtUtc = Now
            },
            Prices = [new WebmasterOfferPrice
            {
                Id = Guid.NewGuid(), PriceType = WebmasterOfferPriceType.Main,
                AvailabilityStatus = ServiceAvailabilityStatus.Available, WebmasterPriceUsd = amount,
                WebmasterPriceDetails = "Private pricing", CreatedAtUtc = Now, UpdatedAtUtc = Now
            }]
        };
    }

    public void Dispose() => _context.Dispose();
}
