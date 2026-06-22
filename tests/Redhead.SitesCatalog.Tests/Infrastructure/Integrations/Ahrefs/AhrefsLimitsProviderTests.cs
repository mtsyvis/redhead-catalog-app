using Microsoft.Extensions.Caching.Memory;
using Moq;
using Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Integrations.Ahrefs;

public sealed class AhrefsLimitsProviderTests
{
    [Fact]
    public async Task GetAsync_UsesCacheUnlessRefreshIsRequested()
    {
        // Arrange
        var api = new Mock<IAhrefsApiClient>();
        api.SetupSequence(client => client.GetLimitsAndUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLimits(10))
            .ReturnsAsync(CreateLimits(20));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new AhrefsLimitsProvider(api.Object, cache);

        // Act
        var first = await sut.GetAsync(false, CancellationToken.None);
        var cached = await sut.GetAsync(false, CancellationToken.None);
        var refreshed = await sut.GetAsync(true, CancellationToken.None);

        // Assert
        Assert.Equal(10, first.Limits.UnitsUsageApiKey);
        Assert.Equal(10, cached.Limits.UnitsUsageApiKey);
        Assert.Equal(first.CheckedAt, cached.CheckedAt);
        Assert.Equal(20, refreshed.Limits.UnitsUsageApiKey);
        api.Verify(
            client => client.GetLimitsAndUsageAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static AhrefsLimitsAndUsage CreateLimits(long apiUsage)
        => new(1_000, 0, 975, apiUsage, DateTime.UtcNow.AddDays(1));
}
