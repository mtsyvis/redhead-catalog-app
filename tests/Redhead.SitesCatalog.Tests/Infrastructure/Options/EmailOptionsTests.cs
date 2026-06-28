using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Options;

public sealed class EmailOptionsTests
{
    [Fact]
    public void IsValid_WhenDisabled_DoesNotRequireSmtpSettings()
    {
        // Arrange
        var options = new EmailOptions { Enabled = false };

        // Act
        var result = EmailOptions.IsValid(options);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null, 587, "noreply@redheaddigital.agency", "Redhead Catalog", 10)]
    [InlineData("smtp-relay.gmail.com", 0, "noreply@redheaddigital.agency", "Redhead Catalog", 10)]
    [InlineData("smtp-relay.gmail.com", 587, "not-an-email", "Redhead Catalog", 10)]
    [InlineData("smtp-relay.gmail.com", 587, "noreply@redheaddigital.agency", "", 10)]
    [InlineData("smtp-relay.gmail.com", 587, "noreply@redheaddigital.agency", "Redhead Catalog", 0)]
    public void IsValid_WhenEnabledConfigurationIsInvalid_ReturnsFalse(
        string? host,
        int port,
        string fromAddress,
        string fromName,
        int timeoutSeconds)
    {
        // Arrange
        var options = new EmailOptions
        {
            Enabled = true,
            SmtpHost = host ?? string.Empty,
            SmtpPort = port,
            FromAddress = fromAddress,
            FromName = fromName,
            SendTimeoutSeconds = timeoutSeconds
        };

        // Act
        var result = EmailOptions.IsValid(options);

        // Assert
        Assert.False(result);
    }
}
