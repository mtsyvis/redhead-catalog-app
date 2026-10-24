using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class LinkDetectivePricesServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILinkDetectiveApiClient> _api = new(MockBehavior.Strict);
    private readonly TestClock _clock = new(Now);
    private readonly LinkDetectiveRequestCoordinator _coordinator;

    public LinkDetectivePricesServiceTests()
    {
        _context = new ApplicationDbContext(_dbOptions);
        _coordinator = new LinkDetectiveRequestCoordinator(_clock);
        _context.Sites.Add(new Site { Domain = "example.com" });
        _context.SaveChanges();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(29, false)]
    [InlineData(30, false)]
    [InlineData(31, true)]
    public async Task GetAsync_RefreshesOnlyWhenCacheIsOlderThanThirtyDays(int ageInDays, bool shouldRefresh)
    {
        // Arrange
        SeedCache(Now.AddDays(-ageInDays));
        if (shouldRefresh) SetupPrices();

        // Act
        var result = await CreateService().GetAsync("example.com", false, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(shouldRefresh ? 525m : 100m, result.MinimumUsd);
        Assert.Equal(shouldRefresh ? Now : Now.AddDays(-ageInDays), result.CheckedAtUtc);
        Assert.False(result.IsStale);
    }

    [Fact]
    public async Task GetAsync_FirstLookupNormalizesDomainAndPersistsOnlyPriceSnapshot()
    {
        // Arrange
        SetupPrices();

        // Act
        var result = await CreateService().GetAsync("https://www.EXAMPLE.com/path", false, CancellationToken.None);

        // Assert
        var saved = Assert.Single(await _context.LinkDetectivePriceCaches.AsNoTracking().ToListAsync());
        Assert.Equal("example.com", result!.Domain);
        Assert.Equal(525m, saved.MinimumUsd);
        Assert.Equal(558.67m, saved.AverageUsd);
        Assert.Equal(534m, saved.MedianUsd);
        Assert.Equal(Now, saved.CheckedAtUtc);
    }

    [Fact]
    public async Task GetAsync_ManualRefreshUpdatesFreshCache()
    {
        // Arrange
        SeedCache(Now.AddDays(-1));
        SetupPrices();

        // Act
        var result = await CreateService().GetAsync("example.com", true, CancellationToken.None);

        // Assert
        Assert.Equal(525m, result!.MinimumUsd);
        Assert.Equal(Now, result.CheckedAtUtc);
    }

    [Fact]
    public async Task GetAsync_FailedRefreshKeepsOldPricesAndTimestamp()
    {
        // Arrange
        SeedCache(Now.AddDays(-31));
        _api.Setup(api => api.GetPricesAsync("example.com", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LinkDetectiveApiException("Request limit reached.", Now.AddMinutes(5)));

        // Act
        var result = await CreateService().GetAsync("example.com", false, CancellationToken.None);

        // Assert
        Assert.Equal(100m, result!.MinimumUsd);
        Assert.Equal(Now.AddDays(-31), result.CheckedAtUtc);
        Assert.True(result.IsStale);
        Assert.Equal("Request limit reached.", result.Message);
        Assert.Equal(Now.AddMinutes(5), result.RetryAfterUtc);
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(0, 60)]
    [InlineData(1, 60)]
    [InlineData(60, 60)]
    [InlineData(300, 300)]
    public async Task GetAsync_ProviderRetryAfterCannotShortenMinimumCooldown(int providerSeconds, int expectedSeconds)
    {
        // Arrange
        SeedCache(Now.AddDays(-31));
        _api.Setup(api => api.GetPricesAsync("example.com", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LinkDetectiveApiException("Rate limited.", Now.AddSeconds(providerSeconds)));
        var service = CreateService();

        // Act
        var result = await service.GetAsync("example.com", true, CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(2));
        var repeated = await service.GetAsync("example.com", true, CancellationToken.None);

        // Assert
        Assert.Equal(Now.AddSeconds(expectedSeconds), result!.RetryAfterUtc);
        Assert.Equal(result.RetryAfterUtc, repeated!.RetryAfterUtc);
        Assert.Equal(100m, repeated.MinimumUsd);
        Assert.Single(_api.Invocations);
        Assert.Equal(result.RetryAfterUtc,
            (await _context.LinkDetectivePriceCaches.AsNoTracking().SingleAsync()).RetryAfterUtc);
    }

    [Fact]
    public async Task GetAsync_FreshCacheDoesNotWaitForAnotherDomainRefresh()
    {
        // Arrange
        const string cachedDomain = "cached.example";
        _context.Sites.Add(new Site { Domain = cachedDomain });
        _context.LinkDetectivePriceCaches.Add(new LinkDetectivePriceCache
        {
            SiteDomain = cachedDomain, MinimumUsd = 10m, AverageUsd = 12m, MedianUsd = 11m, CheckedAtUtc = Now
        });
        await _context.SaveChangesAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Setup(api => api.GetPricesAsync("example.com", It.IsAny<CancellationToken>())).Returns(async () =>
        {
            entered.SetResult();
            await release.Task;
            return new LinkDetectiveDomainPrices(525m, 558.67m, 534m);
        });
        using var secondContext = new ApplicationDbContext(_dbOptions);
        var secondService = CreateService(context: secondContext);

        // Act
        var updating = CreateService().GetAsync("example.com", false, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var cached = secondService.GetAsync(cachedDomain, false, CancellationToken.None);
        var firstCompleted = await Task.WhenAny(cached, Task.Delay(TimeSpan.FromSeconds(1)));
        release.SetResult();
        await Task.WhenAll(updating, cached);
        var cachedResult = await cached;

        // Assert
        Assert.Same(cached, firstCompleted);
        Assert.Equal(10m, cachedResult!.MinimumUsd);
        Assert.Equal(Now, cachedResult.CheckedAtUtc);
        Assert.Single(_api.Invocations);
    }

    [Fact]
    public async Task GetAsync_NotFoundIsCachedWithoutInventingZeroPrices()
    {
        // Arrange
        _api.Setup(api => api.GetPricesAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkDetectiveDomainPrices(null, null, null));
        var service = CreateService();

        // Act
        await service.GetAsync("example.com", false, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(1));
        var result = await service.GetAsync("example.com", false, CancellationToken.None);

        // Assert
        Assert.Null(result!.MinimumUsd);
        Assert.Null(result.AverageUsd);
        Assert.Null(result.MedianUsd);
        Assert.Equal(Now, result.CheckedAtUtc);
        Assert.Equal("No market prices found for this domain.", result.Message);
        Assert.Single(_api.Invocations);
    }

    [Fact]
    public async Task GetAsync_DuplicateManualRequestsSharePersistedCooldown()
    {
        // Arrange
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Setup(api => api.GetPricesAsync("example.com", It.IsAny<CancellationToken>())).Returns(async () =>
        {
            entered.SetResult();
            await release.Task;
            return new LinkDetectiveDomainPrices(525m, 558.67m, 534m);
        });
        using var secondContext = new ApplicationDbContext(_dbOptions);
        var secondService = CreateService(context: secondContext);

        // Act
        var first = CreateService().GetAsync("example.com", true, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = secondService.GetAsync("example.com", true, CancellationToken.None);
        release.SetResult();
        var results = await Task.WhenAll(first, second);

        // Assert
        Assert.All(results, result => Assert.Equal(525m, result!.MinimumUsd));
        Assert.Single(_api.Invocations);
    }

    [Fact]
    public async Task GetAsync_CooldownExpiresAndAllowsAnotherManualRefresh()
    {
        // Arrange
        SetupPrices();
        var service = CreateService();
        await service.GetAsync("example.com", true, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));

        // Act
        var result = await service.GetAsync("example.com", true, CancellationToken.None);

        // Assert
        Assert.Equal(Now.AddMinutes(1), result!.CheckedAtUtc);
        Assert.Equal(2, _api.Invocations.Count);
    }

    [Fact]
    public async Task GetAsync_NotConfiguredReturnsSavedPricesWithoutCallingProvider()
    {
        // Arrange
        SeedCache(Now.AddDays(-31));

        // Act
        var result = await CreateService(token: null).GetAsync("example.com", true, CancellationToken.None);

        // Assert
        Assert.Equal(100m, result!.MinimumUsd);
        Assert.True(result.IsStale);
        Assert.Contains("not configured", result.Message);
    }

    [Fact]
    public async Task GetAsync_NonCatalogDomainDoesNotSpendQuotaOrCreateCache()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetAsync("missing.example", true, CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Empty(await _context.LinkDetectivePriceCaches.ToListAsync());
    }

    [Theory]
    [InlineData(7, 7, false)]
    [InlineData(7, 8, true)]
    [InlineData(45, 31, false)]
    [InlineData(45, 46, true)]
    public async Task GetAsync_UsesConfiguredCacheLifetime(int lifetimeDays, int ageDays, bool shouldRefresh)
    {
        // Arrange
        SeedCache(Now.AddDays(-ageDays));
        if (shouldRefresh) SetupPrices();

        // Act
        var result = await CreateService(cacheLifetimeDays: lifetimeDays)
            .GetAsync("example.com", false, CancellationToken.None);

        // Assert
        Assert.Equal(shouldRefresh ? 525m : 100m, result!.MinimumUsd);
        Assert.False(result.IsStale);
        Assert.Equal(shouldRefresh ? 1 : 0, _api.Invocations.Count);
    }

    [Fact]
    public async Task GetAsync_UnconfiguredProviderMarksPricesStaleUsingConfiguredLifetime()
    {
        // Arrange
        SeedCache(Now.AddDays(-8));

        // Act
        var result = await CreateService(token: null, cacheLifetimeDays: 7)
            .GetAsync("example.com", false, CancellationToken.None);

        // Assert
        Assert.Equal(100m, result!.MinimumUsd);
        Assert.True(result.IsStale);
        Assert.Empty(_api.Invocations);
    }

    private LinkDetectivePricesService CreateService(string? token = "test-token", ApplicationDbContext? context = null,
        int cacheLifetimeDays = 30)
        => new(context ?? _context, _api.Object, Options.Create(new LinkDetectiveOptions
        {
            ApiToken = token, CacheLifetimeDays = cacheLifetimeDays
        }), _coordinator, _clock);

    private void SetupPrices()
        => _api.Setup(api => api.GetPricesAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkDetectiveDomainPrices(525m, 558.67m, 534m));

    private void SeedCache(DateTime checkedAt)
    {
        _context.LinkDetectivePriceCaches.Add(new LinkDetectivePriceCache
        {
            SiteDomain = "example.com", MinimumUsd = 100m, AverageUsd = 120m, MedianUsd = 110m, CheckedAtUtc = checkedAt
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        _context.Dispose();
    }

    private sealed class TestClock(DateTime now) : TimeProvider
    {
        private DateTime _now = now;
        public override DateTimeOffset GetUtcNow() => new(_now);
        public void Advance(TimeSpan time) => _now += time;
    }
}
