using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Options;

public sealed class AhrefsSyncOptionsTests
{
    [Fact]
    public void IsValid_WhenDisabledStillValidatesOperationalSettings()
    {
        // Arrange
        var options = new AhrefsSyncOptions
        {
            Enabled = false,
            BatchSize = 0
        };

        // Act
        var valid = AhrefsSyncOptions.IsValid(options);

        // Assert
        Assert.False(valid);
    }

    [Theory]
    [InlineData("domain", "both", "monthly")]
    [InlineData("subdomain", "both", "monthly")]
    [InlineData("subdomains", "http", "monthly")]
    [InlineData("subdomains", "both", "daily")]
    public void IsValid_WhenTaskModesAreChanged_ReturnsFalse(
        string targetMode,
        string protocol,
        string volumeMode)
    {
        // Arrange
        var options = new AhrefsSyncOptions
        {
            TargetMode = targetMode,
            Protocol = protocol,
            VolumeMode = volumeMode
        };

        // Act
        var valid = AhrefsSyncOptions.IsValid(options);

        // Assert
        Assert.False(valid);
    }
}

public sealed class AhrefsOptionsTests
{
    [Theory]
    [InlineData("http://api.ahrefs.com/v3")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void IsValid_WhenBaseUrlIsNotAbsoluteHttps_ReturnsFalse(string baseUrl)
    {
        // Arrange
        var options = new AhrefsOptions { BaseUrl = baseUrl };

        // Act
        var valid = AhrefsOptions.IsValid(options);

        // Assert
        Assert.False(valid);
    }
}
