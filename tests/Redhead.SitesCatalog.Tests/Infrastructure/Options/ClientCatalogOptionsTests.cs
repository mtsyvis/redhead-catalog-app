using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Options;

public sealed class ClientCatalogOptionsTests
{
    [Fact]
    public void Defaults_Use2000SitesPerFiveMinutes()
    {
        // Arrange
        var options = new ClientCatalogOptions();

        // Act
        var limit = options.UniqueSitesPerFiveMinutes;

        // Assert
        Assert.Equal(2000, limit);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(5000, true)]
    public void IsValid_RequiresPositiveHourlyAlertThreshold(int limit, bool expected)
    {
        // Arrange
        var options = new ClientCatalogOptions { AlertUniqueSitesPerHour = limit };

        // Act
        var valid = ClientCatalogOptions.IsValid(options);

        // Assert
        Assert.Equal(expected, valid);
    }
}
