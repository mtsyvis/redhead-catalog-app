using Microsoft.EntityFrameworkCore;
using Npgsql;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterOffersConcurrencyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType")]
    [InlineData("PK_SiteWebmasterOfferLinkbuilderMailboxes")]
    [InlineData("IX_Webmasters_NormalizedContactRawText")]
    public async Task UpdateAsync_SaveConflict_ReturnsConflictAndDiscardsPendingChanges(string? constraint)
    {
        // Arrange
        await using var context = CreateContext();
        var offer = await SeedOfferAsync(context);
        var originalVersion = offer.Version;
        context.SaveException = constraint is null
            ? new DbUpdateConcurrencyException("Concurrent save")
            : CreateUniqueViolation(constraint);
        var sut = new WebmasterOffersService(context);

        // Act
        var result = await sut.UpdateAsync(offer.Id, CreateRequest(offer), "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Conflict, result.Status);
        Assert.Null(result.Offer);
        Assert.Empty(context.ChangeTracker.Entries());
        context.SaveException = null;
        Assert.Equal(0, await context.SaveChangesAsync());
        Assert.Empty(await context.EntityChangeHistories.ToListAsync());
        var saved = await context.SiteWebmasterOffers.SingleAsync();
        Assert.Null(saved.CommentText);
        Assert.Equal(originalVersion, saved.Version);
    }

    [Fact]
    public async Task UpdateAsync_UnrelatedDatabaseError_IsNotReportedAsConflict()
    {
        // Arrange
        await using var context = CreateContext();
        var offer = await SeedOfferAsync(context);
        context.SaveException = CreateUniqueViolation("IX_UnrelatedTable_Key");
        var sut = new WebmasterOffersService(context);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            sut.UpdateAsync(offer.Id, CreateRequest(offer), "editor@example.com"));

        // Assert
        Assert.Same(context.SaveException, exception);
    }

    [Fact]
    public async Task UpdateAsync_ClockBehindStoredTimestamp_DoesNotUseTimestampForConcurrency()
    {
        // Arrange
        await using var context = CreateContext();
        var offer = await SeedOfferAsync(context);
        var futureTimestamp = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        offer.UpdatedAtUtc = futureTimestamp;
        offer.Version = 7;
        await context.SaveChangesAsync();
        var sut = new WebmasterOffersService(context);

        // Act
        var result = await sut.UpdateAsync(offer.Id, CreateRequest(offer), "editor@example.com");

        // Assert
        Assert.Equal(WebmasterOfferUpdateStatus.Success, result.Status);
        Assert.Equal(7U, result.Offer!.Version);
        Assert.True(result.Offer.UpdatedAtUtc < futureTimestamp);
        context.ChangeTracker.Clear();
        var saved = await context.SiteWebmasterOffers.SingleAsync();
        Assert.Equal(7U, saved.Version);
        Assert.True(saved.UpdatedAtUtc < futureTimestamp);
    }

    [Fact]
    public void NpgsqlModel_ProtectsOfferVersionAndPriceUniqueness()
    {
        // Arrange
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);

        // Act
        var offerType = context.Model.FindEntityType(typeof(SiteWebmasterOffer))!;
        var webmasterType = context.Model.FindEntityType(typeof(Webmaster))!;
        var priceType = context.Model.FindEntityType(typeof(WebmasterOfferPrice))!;

        // Assert
        Assert.False(offerType.FindProperty(nameof(SiteWebmasterOffer.UpdatedAtUtc))!.IsConcurrencyToken);
        Assert.False(webmasterType.FindProperty(nameof(Webmaster.UpdatedAtUtc))!.IsConcurrencyToken);
        Assert.True(offerType.FindProperty(nameof(SiteWebmasterOffer.Version))!.IsConcurrencyToken);
        Assert.True(webmasterType.FindProperty(nameof(Webmaster.Version))!.IsConcurrencyToken);
        Assert.Equal("xmin", offerType.FindProperty(nameof(SiteWebmasterOffer.Version))!.GetColumnName());
        Assert.Equal("xmin", webmasterType.FindProperty(nameof(Webmaster.Version))!.GetColumnName());
        var index = Assert.Single(priceType.GetIndexes(), item => item.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(WebmasterOfferPrice.SiteWebmasterOfferId), nameof(WebmasterOfferPrice.PriceType)]));
        Assert.True(index.IsUnique);
    }

    private static FailingSaveContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<SiteWebmasterOffer> SeedOfferAsync(ApplicationDbContext context)
    {
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(), SiteDomain = "concurrent.example", ImportFingerprint = new string('a', 64),
            ContactRawText = "contact@example.com", CreatedAtUtc = now, UpdatedAtUtc = now,
            Webmaster = new Webmaster
            {
                Id = Guid.NewGuid(), ContactRawText = "contact@example.com",
                NormalizedContactRawText = "contact@example.com", CreatedAtUtc = now, UpdatedAtUtc = now
            }
        };
        context.SiteWebmasterOffers.Add(offer);
        await context.SaveChangesAsync();
        return offer;
    }

    private static UpdateWebmasterOfferRequest CreateRequest(SiteWebmasterOffer offer) => new()
    {
        ExpectedVersion = offer.Version,
        ExpectedWebmasterVersion = offer.Webmaster.Version,
        ContactRawText = offer.ContactRawText,
        Status = SiteWebmasterOfferStatus.Active,
        CommentText = "Changed"
    };

    private static DbUpdateException CreateUniqueViolation(string constraint) => new("Unique violation",
        new PostgresException("Unique violation", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
            constraintName: constraint));

    private sealed class FailingSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public Exception? SaveException { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            SaveException is { } exception ? Task.FromException<int>(exception) : base.SaveChangesAsync(cancellationToken);
    }
}
