using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.Services.Currencies;
using Redhead.SitesCatalog.Infrastructure.Integrations.Currencies;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class CurrencyReferenceRefreshServiceTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _database = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private readonly ApplicationDbContext _context;
    private readonly TestClock _clock = new();
    private readonly ExchangeRateApiOptions _options = new();

    public CurrencyReferenceRefreshServiceTests() => _context = new ApplicationDbContext(_database);
    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Refresh_PersistsDailyClaimBeforeRequestAndKeepsItAfterCancellation()
    {
        // Arrange
        _options.ApiKey = "synthetic-test-key";
        using var cancellation = new CancellationTokenSource();
        var client = new Mock<ICurrencyReferenceRateClient>(MockBehavior.Strict);
        client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken token) =>
        {
            await using var persisted = new ApplicationDbContext(_database);
            var state = await persisted.CurrencyReferenceRefreshStates.SingleAsync();
            Assert.True(state.NextAttemptAtUtc > _clock.GetUtcNow().UtcDateTime);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return new CurrencyRateTable(_clock.GetUtcNow().UtcDateTime, new Dictionary<string, decimal>());
        });

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CurrencyReferenceRefreshService(_context, client.Object, Options.Create(_options), _clock).TickAsync(cancellation.Token));
        await using var restarted = new ApplicationDbContext(_database);
        await new CurrencyReferenceRefreshService(restarted, client.Object, Options.Create(_options), _clock).TickAsync(CancellationToken.None);

        // Assert
        client.Verify(x => x.FetchAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False((await restarted.CurrencyReferenceRefreshStates.SingleAsync()).LastAttemptSucceeded);
        Assert.Empty(await restarted.CurrencyReferenceSnapshots.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_ReplacesRatesOnSuccessAndPreservesLastGoodRatesOnFailureWithoutTouchingOffers(bool fail)
    {
        // Arrange
        var offer = await SeedForeignOfferAsync("EUR", 25m, 1.25m);
        AddSnapshot(0.7m);
        await _context.SaveChangesAsync();
        _options.ApiKey = "synthetic-test-key";
        var client = new Mock<ICurrencyReferenceRateClient>(MockBehavior.Strict);
        if (fail)
        {
            client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("Provider failure"));
        }
        else
        {
            client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CurrencyRateTable(_clock.GetUtcNow().UtcDateTime, new Dictionary<string, decimal> { ["CAD"] = 0.8m }));
        }

        var before = offer.UpdatedAtUtc;
        var refresh = new CurrencyReferenceRefreshService(_context, client.Object, Options.Create(_options), _clock);

        // Act
        await refresh.TickAsync(CancellationToken.None);
        await using var verification = new ApplicationDbContext(_database);

        // Assert
        var snapshot = Assert.Single(await verification.CurrencyReferenceSnapshots.ToListAsync());
        var rates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(snapshot.UsdRatesJson)!;
        Assert.Equal(fail ? 0.7m : 0.8m, rates["CAD"]);
        Assert.Equal(!fail, (await verification.CurrencyReferenceRefreshStates.SingleAsync()).LastAttemptSucceeded);
        Assert.Equal(before, (await verification.SiteWebmasterOffers.SingleAsync()).UpdatedAtUtc);
        Assert.Equal(32m, (await verification.WebmasterOfferPrices.SingleAsync()).WebmasterPriceUsd);
        Assert.Empty(await verification.EntityChangeHistories.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_WaitsOneDayBetweenAttemptsAcrossRestart(bool fail)
    {
        // Arrange
        _options.ApiKey = "synthetic-test-key";
        var client = new Mock<ICurrencyReferenceRateClient>(MockBehavior.Strict);
        if (fail)
        {
            client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("Provider failure"));
        }
        else
        {
            client.Setup(x => x.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() =>
                new CurrencyRateTable(_clock.GetUtcNow().UtcDateTime, new Dictionary<string, decimal> { ["CAD"] = 0.8m }));
        }

        // Act
        await new CurrencyReferenceRefreshService(_context, client.Object, Options.Create(_options), _clock).TickAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(1).Subtract(TimeSpan.FromMinutes(1)));
        await using var restarted = new ApplicationDbContext(_database);
        await new CurrencyReferenceRefreshService(restarted, client.Object, Options.Create(_options), _clock).TickAsync(CancellationToken.None);
        var requestsBeforeDue = client.Invocations.Count;
        _clock.Advance(TimeSpan.FromMinutes(1));
        await using var due = new ApplicationDbContext(_database);
        await new CurrencyReferenceRefreshService(due, client.Object, Options.Create(_options), _clock).TickAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, requestsBeforeDue);
        client.Verify(x => x.FetchAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        var state = await due.CurrencyReferenceRefreshStates.SingleAsync();
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddDays(1), state.NextAttemptAtUtc);
        Assert.Equal(!fail, state.LastAttemptSucceeded);
        Assert.Equal(fail ? 0 : 1, await due.CurrencyReferenceSnapshots.CountAsync());
    }

    [Fact]
    public async Task Refresh_MissingConfigurationSkipsFetch_AndCancellationPropagates()
    {
        // Arrange
        var client = new Mock<ICurrencyReferenceRateClient>(MockBehavior.Strict);
        var refresh = new CurrencyReferenceRefreshService(_context, client.Object, Options.Create(_options), _clock);

        // Act
        await refresh.TickAsync(CancellationToken.None);
        _options.ApiKey = "synthetic-test-key";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = await Record.ExceptionAsync(() => refresh.TickAsync(cancellation.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Empty(_context.CurrencyReferenceSnapshots);
        client.VerifyNoOtherCalls();
    }

    private CurrencyReferenceSnapshot AddSnapshot(decimal cadRate)
        => OfferCurrencyTestData.AddSnapshot(_context, cadRate, _clock.GetUtcNow().UtcDateTime);

    private Task<SiteWebmasterOffer> SeedForeignOfferAsync(string currency, decimal amount, decimal rate)
        => OfferCurrencyTestData.SeedForeignOfferAsync(_context, currency, amount, rate, _clock.GetUtcNow().UtcDateTime);

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan interval) => _now += interval;
    }
}
