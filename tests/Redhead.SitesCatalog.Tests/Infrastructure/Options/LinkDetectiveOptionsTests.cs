using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Options;

public sealed class LinkDetectiveOptionsTests
{
    [Fact]
    public void CacheLifetimeDays_DefaultsToThirty()
    {
        // Arrange
        var options = new LinkDetectiveOptions();

        // Act
        var lifetime = options.CacheLifetimeDays;

        // Assert
        Assert.Equal(30, lifetime);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(int.MaxValue, true)]
    public void IsValid_RequiresPositiveCacheLifetime(int lifetimeDays, bool expected)
    {
        // Arrange
        var options = new LinkDetectiveOptions { CacheLifetimeDays = lifetimeDays };

        // Act
        var valid = LinkDetectiveOptions.IsValid(options);

        // Assert
        Assert.Equal(expected, valid);
    }
}
