using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public class LiteMultiSearchUsageServiceTests
{
    [Fact]
    public async Task TryConsumeAsync_WhenRequestIsAllowed_ConsumesDomainCount()
    {
        // Arrange
        await using var db = CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero));
        var sut = new LiteMultiSearchUsageService(db, timeProvider);

        // Act
        var first = await sut.TryConsumeAsync("user-1", 3);
        var second = await sut.TryConsumeAsync("user-1", 2);

        // Assert
        Assert.Equal(LiteMultiSearchUsageStatus.Allowed, first.Status);
        Assert.Equal(3, first.DomainsUsed);
        Assert.Equal(LiteMultiSearchUsageStatus.Allowed, second.Status);
        Assert.Equal(5, second.DomainsUsed);

        var usage = await db.LiteMultiSearchUsages.SingleAsync();
        Assert.Equal("user-1", usage.UserId);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), usage.MonthStartUtc);
        Assert.Equal(5, usage.DomainsUsed);
    }

    [Fact]
    public async Task TryConsumeAsync_WhenRequestExceedsPerRequestLimit_RejectsWithoutSavingUsage()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = new LiteMultiSearchUsageService(
            db,
            new TestTimeProvider(new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero)));

        // Act
        var result = await sut.TryConsumeAsync(
            "user-1",
            LiteMultiSearchConstants.MaxDomainsPerRequest + 1);

        // Assert
        Assert.Equal(LiteMultiSearchUsageStatus.RequestLimitExceeded, result.Status);
        Assert.Equal(LiteMultiSearchConstants.MaxDomainsPerRequest + 1, result.DomainsRequested);
        Assert.Empty(db.LiteMultiSearchUsages);
    }

    [Fact]
    public async Task TryConsumeAsync_WhenMonthlyLimitWouldBeExceeded_RejectsWithoutIncrementingUsage()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.LiteMultiSearchUsages.Add(new LiteMultiSearchUsage
        {
            UserId = "user-1",
            MonthStartUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            DomainsUsed = 999,
            CreatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();
        var sut = new LiteMultiSearchUsageService(
            db,
            new TestTimeProvider(new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero)));

        // Act
        var result = await sut.TryConsumeAsync("user-1", 2);

        // Assert
        Assert.Equal(LiteMultiSearchUsageStatus.MonthlyLimitExceeded, result.Status);
        Assert.Equal(999, result.DomainsUsed);
        Assert.Equal(1, result.RemainingAfterRequest);

        var usage = await db.LiteMultiSearchUsages.SingleAsync();
        Assert.Equal(999, usage.DomainsUsed);
    }

    [Fact]
    public async Task TryConsumeAsync_UsesUtcCalendarMonthForReset()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.LiteMultiSearchUsages.Add(new LiteMultiSearchUsage
        {
            UserId = "user-1",
            MonthStartUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            DomainsUsed = LiteMultiSearchConstants.MonthlyDomainLimit,
            CreatedAtUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 5, 31, 23, 59, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();
        var sut = new LiteMultiSearchUsageService(
            db,
            new TestTimeProvider(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        // Act
        var result = await sut.TryConsumeAsync("user-1", 50);

        // Assert
        Assert.Equal(LiteMultiSearchUsageStatus.Allowed, result.Status);
        Assert.Equal(50, result.DomainsUsed);
        Assert.Equal(2, await db.LiteMultiSearchUsages.CountAsync());
        Assert.Equal(
            50,
            await db.LiteMultiSearchUsages
                .Where(usage => usage.MonthStartUtc == new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc))
                .Select(usage => usage.DomainsUsed)
                .SingleAsync());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
