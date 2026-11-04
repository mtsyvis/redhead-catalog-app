using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.LinkDetective;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Concurrency;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Coordination;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Application.LinkDetective;

public sealed class LinkDetectiveSyncServiceTests : IDisposable
{
    private static readonly DateTime InitialNow = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private readonly DbContextOptions<ApplicationDbContext> _db = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private readonly Mock<ILinkDetectiveApiClient> _api = new(MockBehavior.Strict);
    private readonly TestClock _clock = new(InitialNow);
    private readonly LinkDetectiveRequestCoordinator _coordinator;
    private readonly LinkDetectiveSyncOptions _options = new() { Enabled = true };
    private readonly Mock<ILinkDetectiveSyncLock> _syncLock = new();

    public LinkDetectiveSyncServiceTests()
    {
        _coordinator = new(_clock);
        _syncLock.Setup(value => value.TryAcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new LockHandle());
    }

    [Fact]
    public async Task Tick_SelectsMissingAndStaleIncludingQuarantineButKeepsExactlyThirtyDaysFresh()
    {
        // Arrange
        await SeedAsync(context =>
        {
            context.Sites.AddRange(new Site { Domain = "missing.example", IsQuarantined = true },
                new Site { Domain = "stale.example" }, new Site { Domain = "fresh.example" }, new Site { Domain = "no-price.example" });
            context.LinkDetectivePriceCaches.AddRange(Cache("stale.example", InitialNow.AddDays(-31)),
                Cache("fresh.example", InitialNow.AddDays(-30)), new LinkDetectivePriceCache { SiteDomain = "no-price.example", CheckedAtUtc = InitialNow });
        });
        SetupQuota();
        SetupBatch(domains => domains.ToDictionary(domain => domain, _ => new LinkDetectivePrices(100, 120, 110)));

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var run = Assert.Single(await context.LinkDetectiveSyncRuns.ToListAsync());
        Assert.Equal(2, run.SelectedCount);
        Assert.Equal(2, run.PricesFoundCount);
        Assert.Equal(LinkDetectiveSyncRunStatus.Completed, run.Status);
        Assert.Equal(["missing.example", "stale.example"], await context.LinkDetectiveSyncRunItems.OrderBy(row => row.Domain).Select(row => row.Domain).ToArrayAsync());
        Assert.All(await context.Sites.ToListAsync(), site => Assert.Equal(default, site.UpdatedAtUtc));
    }

    [Fact]
    public async Task Tick_ConfirmedNotFoundReplacesOldPricesAndCountsAsSuccessfulCheck()
    {
        // Arrange
        await SeedOneAsync();
        SetupQuota();
        SetupBatch(domains => domains.ToDictionary(domain => domain, _ => new LinkDetectivePrices(null, null, null)));

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var cache = await context.LinkDetectivePriceCaches.SingleAsync();
        Assert.Null(cache.MinimumUsd);
        Assert.Equal(InitialNow, cache.CheckedAtUtc);
        Assert.Equal(1, (await context.LinkDetectiveSyncRuns.SingleAsync()).NotFoundCount);
        Assert.Equal(LinkDetectiveSyncItemStatus.NotFound, (await context.LinkDetectiveSyncRunItems.SingleAsync()).Status);
    }

    [Fact]
    public async Task Tick_ReserveStopsPaidRequestsAndPersistsRunUntilProviderReset()
    {
        // Arrange
        await SeedOneAsync();
        SetupQuota(remaining: 10000);

        // Act
        var next = await TickAsync();
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var run = await context.LinkDetectiveSyncRuns.SingleAsync();
        Assert.Equal(LinkDetectiveSyncRunStatus.Waiting, run.Status);
        Assert.Equal(InitialNow.Date.AddDays(1), next);
        Assert.Equal(0, (await context.LinkDetectiveSyncRunItems.SingleAsync()).Attempts);
        Assert.Single(_api.Invocations); // The second tick uses the durable wait, not another preview.
    }

    [Fact]
    public async Task Tick_ShrinksBatchToBudgetAboveReserve()
    {
        // Arrange
        await SeedAsync(context => context.Sites.AddRange(new Site { Domain = "a.example" }, new Site { Domain = "b.example" }));
        SetupQuota(remaining: 10001);
        SetupBatch(domains => domains.ToDictionary(domain => domain, _ => new LinkDetectivePrices(10, 20, 15)), 10000);

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        Assert.Equal(1, (await context.LinkDetectiveSyncRuns.SingleAsync()).ProcessedCount);
        Assert.Single(await context.LinkDetectivePriceCaches.ToListAsync());
        var call = Assert.Single(_api.Invocations, call => call.Method.Name == nameof(ILinkDetectiveApiClient.GetBatchPricesAsync));
        Assert.Single((IReadOnlyCollection<string>)call.Arguments[0]);
    }

    [Fact]
    public async Task Tick_PartialResponseKeepsOldPricesAndRetriesOnlyUnresolvedDomain()
    {
        // Arrange
        await SeedAsync(context =>
        {
            context.Sites.AddRange(new Site { Domain = "good.example" }, new Site { Domain = "omitted.example" });
            context.LinkDetectivePriceCaches.Add(Cache("omitted.example", InitialNow.AddDays(-31)));
        });
        SetupQuota();
        _api.Setup(api => api.GetBatchPricesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkDetectiveBatchPricesResponse(new Dictionary<string, LinkDetectivePrices> { ["good.example"] = new(10, 20, 15) }, true, Quota()));

        // Act
        await TickAsync();
        _clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var old = await context.LinkDetectivePriceCaches.SingleAsync(row => row.SiteDomain == "omitted.example");
        Assert.Equal(100, old.MinimumUsd);
        Assert.Equal(InitialNow.AddDays(-31), old.CheckedAtUtc);
        var calls = _api.Invocations.Where(call => call.Method.Name == nameof(ILinkDetectiveApiClient.GetBatchPricesAsync)).ToArray();
        Assert.Equal(2, calls.Length);
        Assert.Equal("omitted.example", Assert.Single((IReadOnlyCollection<string>)calls[1].Arguments[0]));
        Assert.Equal(1, (await context.LinkDetectiveSyncRuns.SingleAsync()).ProcessedCount);
    }

    [Fact]
    public async Task Tick_ExhaustedRetriesCompleteWithErrorsWithoutRefreshingOldSnapshot()
    {
        // Arrange
        await SeedOneAsync();
        _options.MaxAttempts = 1;
        SetupQuota();
        SetupBatch(_ => new Dictionary<string, LinkDetectivePrices>());

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var run = await context.LinkDetectiveSyncRuns.SingleAsync();
        Assert.Equal(LinkDetectiveSyncRunStatus.CompletedWithErrors, run.Status);
        Assert.Equal(1, run.FailedCount);
        Assert.Equal(InitialNow.AddDays(-31), (await context.LinkDetectivePriceCaches.SingleAsync()).CheckedAtUtc);
    }

    [Fact]
    public async Task Tick_AfterRestartReusesManualRefreshInsteadOfCheckingAgain()
    {
        // Arrange
        await SeedOneAsync();
        SetupQuota(10000);
        await TickAsync();
        await SeedAsync(context =>
        {
            var cache = context.LinkDetectivePriceCaches.Single();
            cache.CheckedAtUtc = InitialNow.AddMinutes(1);
        });
        _clock.Advance(TimeSpan.FromDays(1));

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var run = await context.LinkDetectiveSyncRuns.SingleAsync();
        Assert.Equal(1, run.ReusedCount);
        Assert.Equal(LinkDetectiveSyncRunStatus.Completed, run.Status);
        Assert.Single(_api.Invocations);
    }

    [Fact]
    public async Task Tick_InterruptedRequestPersistsAttemptAndResumesInNewScope()
    {
        // Arrange
        await SeedOneAsync();
        SetupQuota();
        _api.Setup(api => api.GetBatchPricesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => TickAsync());
        await using (var context = new ApplicationDbContext(_db))
        {
            Assert.Equal(LinkDetectiveSyncItemStatus.InFlight, (await context.LinkDetectiveSyncRunItems.SingleAsync()).Status);
        }

        _clock.Advance(TimeSpan.FromMinutes(5));
        SetupBatch(domains => domains.ToDictionary(domain => domain, _ => new LinkDetectivePrices(25, 30, 28)));

        // Act
        await TickAsync();

        // Assert
        await using var result = new ApplicationDbContext(_db);
        Assert.Equal(2, (await result.LinkDetectiveSyncRunItems.SingleAsync()).Attempts);
        Assert.Equal(25, (await result.LinkDetectivePriceCaches.SingleAsync()).MinimumUsd);
        Assert.Single(await result.LinkDetectiveSyncRuns.ToListAsync());
    }

    [Fact]
    public async Task Tick_DisabledOrUnconfiguredDoesNotCreateRunsOrCallProvider()
    {
        // Arrange
        await SeedOneAsync();
        _options.Enabled = false;

        // Act
        await TickAsync();
        _options.Enabled = true;
        await TickAsync(token: null);

        // Assert
        await using var context = new ApplicationDbContext(_db);
        Assert.Empty(await context.LinkDetectiveSyncRuns.ToListAsync());
        Assert.Empty(_api.Invocations);
    }

    [Fact]
    public async Task Monitoring_ReportsFreshNoPriceAndFiltersBeforePaging()
    {
        // Arrange
        await SeedAsync(context => context.Sites.AddRange(new Site { Domain = "a.example" }, new Site { Domain = "b.example" }));
        SetupQuota();
        SetupBatch(domains => domains.ToDictionary(domain => domain, _ => new LinkDetectivePrices(null, null, null)));
        await TickAsync();
        await using var context = new ApplicationDbContext(_db);
        var monitoring = new LinkDetectiveSyncMonitoring(context, Options.Create(new LinkDetectiveOptions()), Options.Create(_options), _clock);
        var id = (await context.LinkDetectiveSyncRuns.SingleAsync()).Id;

        // Act
        var status = await monitoring.GetStatusAsync(CancellationToken.None);
        var details = await monitoring.GetRunAsync(id, 1, 1, " B.EXAMPLE ", LinkDetectiveSyncItemStatus.NotFound, CancellationToken.None);

        // Assert
        Assert.Equal(2, status.FreshCount);
        Assert.Equal(0, status.OutdatedCount);
        Assert.Equal(1, details!.Results.TotalCount);
        Assert.Equal("b.example", Assert.Single(details.Results.Items).Domain);
    }

    [Fact]
    public async Task Tick_ContinuesSameRunAfterQuotaReset()
    {
        // Arrange
        await SeedOneAsync();
        SetupQuota(10000);
        await TickAsync();
        _clock.Advance(TimeSpan.FromDays(1));
        SetupQuota(50000);
        SetupBatch(domains => domains.ToDictionary(domain => domain, _ => new LinkDetectivePrices(10, 20, 15)));

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var run = Assert.Single(await context.LinkDetectiveSyncRuns.ToListAsync());
        Assert.Equal(LinkDetectiveSyncRunStatus.Completed, run.Status);
        Assert.Equal(1, run.PricesFoundCount);
    }

    [Fact]
    public async Task Tick_UnknownQuotaPeriodWithoutResetUsesBoundedRecheckInsteadOfInventedDailyReset()
    {
        // Arrange
        await SeedOneAsync();
        _api.Setup(api => api.GetQuotaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkDetectiveQuota(50000, 40000, 10000, "billing-period", null));

        // Act
        var next = await TickAsync();

        // Assert
        Assert.Equal(InitialNow.AddMinutes(15), next);
        await using var context = new ApplicationDbContext(_db);
        Assert.Null((await context.LinkDetectiveSyncStates.SingleAsync()).QuotaResetAtUtc);
    }

    [Fact]
    public async Task Tick_DifferentPreviewPoolDoesNotAuthorizePaidDomainChecks()
    {
        // Arrange
        await SeedOneAsync();
        await SeedAsync(context => context.LinkDetectiveSyncStates.Add(new()
        {
            QuotaLimit = 50000, QuotaRemaining = 20000, QuotaUsed = 30000, QuotaPeriod = "day",
            QuotaObservedAtUtc = InitialNow, QuotaResetAtUtc = InitialNow.Date.AddDays(1)
        }));
        _api.Setup(api => api.GetQuotaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkDetectiveQuota(500000, 0, 500000, "billing-period", null));

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        Assert.Equal(50000, (await context.LinkDetectiveSyncStates.SingleAsync()).QuotaLimit);
        Assert.Contains("quota pools differ", (await context.LinkDetectiveSyncRuns.SingleAsync()).WaitingReason);
        Assert.Single(_api.Invocations);
    }

    [Fact]
    public async Task TickAndMonitoring_AcceptMaximumConfiguredCacheLifetime()
    {
        // Arrange
        await SeedOneAsync();
        await using var context = new ApplicationDbContext(_db);
        var monitoring = new LinkDetectiveSyncMonitoring(context,
            Options.Create(new LinkDetectiveOptions { CacheLifetimeDays = LinkDetectiveOptions.MaxCacheLifetimeDays }), Options.Create(_options), _clock);

        // Act
        await TickAsync(lifetimeDays: LinkDetectiveOptions.MaxCacheLifetimeDays);
        var status = await monitoring.GetStatusAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, status.FreshCount);
        Assert.Empty(await context.LinkDetectiveSyncRuns.ToListAsync());
        Assert.Empty(_api.Invocations);
    }

    [Fact]
    public async Task Tick_ExpiredExplicitResetIsRecheckedSoonInsteadOfWaitingAnotherDay()
    {
        // Arrange
        await SeedOneAsync();
        _api.Setup(api => api.GetQuotaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkDetectiveQuota(50000, 50000, 0, "day", InitialNow.AddMinutes(-1)));

        // Act
        var next = await TickAsync();

        // Assert
        Assert.Equal(InitialNow.AddMinutes(15), next);
    }

    [Fact]
    public async Task Tick_ScheduleChangeUpdatesPersistedFutureSelection()
    {
        // Arrange
        var original = InitialNow.Date.AddDays(1).AddMinutes(10);
        await SeedAsync(context => context.LinkDetectiveSyncStates.Add(new() { NextSelectionAtUtc = original }));
        _options.DailySelectionTimeUtc = "12:30";

        // Act
        var next = await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        Assert.Equal(InitialNow.Date.AddDays(1).AddHours(12).AddMinutes(30), next);
        Assert.Equal(next, (await context.LinkDetectiveSyncStates.SingleAsync()).NextSelectionAtUtc);
        Assert.Empty(_api.Invocations);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(750)]
    public async Task Tick_UsesConfiguredBatchSizeIncludingValuesAboveFiveHundred(int batchSize)
    {
        // Arrange
        _options.BatchSize = batchSize;
        await SeedAsync(context => context.Sites.AddRange(
            Enumerable.Range(1, batchSize + 1).Select(index => new Site { Domain = $"site{index}.example" })));
        SetupQuota();
        SetupBatch(domains => domains.ToDictionary(domain => domain, _ => new LinkDetectivePrices(10, 20, 15)));

        // Act
        await TickAsync();

        // Assert
        await using var context = new ApplicationDbContext(_db);
        var run = await context.LinkDetectiveSyncRuns.SingleAsync();
        Assert.Equal(batchSize + 1, run.SelectedCount);
        Assert.Equal(batchSize, run.ProcessedCount);
        Assert.Equal(batchSize, await context.LinkDetectivePriceCaches.CountAsync());
        Assert.Single(await context.LinkDetectiveSyncRunItems
            .Where(row => row.Status == LinkDetectiveSyncItemStatus.Pending).ToListAsync());
    }

    private async Task<DateTime> TickAsync(string? token = "test-token", int lifetimeDays = 30)
    {
        await using var context = new ApplicationDbContext(_db);
        var service = new LinkDetectiveSyncService(context, _api.Object, _syncLock.Object, _coordinator,
            Options.Create(new LinkDetectiveOptions { ApiToken = token, CacheLifetimeDays = lifetimeDays }), Options.Create(_options), _clock);
        return await service.TickAsync(CancellationToken.None);
    }

    private async Task SeedAsync(Action<ApplicationDbContext> seed)
    {
        await using var context = new ApplicationDbContext(_db);
        seed(context);
        await context.SaveChangesAsync();
    }

    private Task SeedOneAsync() => SeedAsync(context =>
    {
        context.Sites.Add(new Site { Domain = "example.com" });
        context.LinkDetectivePriceCaches.Add(Cache("example.com", InitialNow.AddDays(-31)));
    });
    private static LinkDetectivePriceCache Cache(string domain, DateTime checkedAt) => new()
        { SiteDomain = domain, MinimumUsd = 100, AverageUsd = 120, MedianUsd = 110, CheckedAtUtc = checkedAt };
    private LinkDetectiveQuota Quota(long remaining = 49000) => new(50000, 50000 - remaining, remaining, "day", _clock.GetUtcNow().UtcDateTime.Date.AddDays(1));
    private void SetupQuota(long remaining = 49000) => _api.Setup(api => api.GetQuotaAsync(It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(Quota(remaining)));
    private void SetupBatch(Func<IReadOnlyCollection<string>, IReadOnlyDictionary<string, LinkDetectivePrices>> results, long remaining = 48000) =>
        _api.Setup(api => api.GetBatchPricesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<string> domains, CancellationToken _) => Task.FromResult(new LinkDetectiveBatchPricesResponse(results(domains), false, Quota(remaining))));
    public void Dispose() => _coordinator.Dispose();
    private sealed class LockHandle : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    private sealed class TestClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
        public void Advance(TimeSpan amount) => now += amount;
    }
}
