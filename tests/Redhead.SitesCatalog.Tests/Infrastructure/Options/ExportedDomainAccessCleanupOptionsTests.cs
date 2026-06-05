using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Options;

public sealed class ExportedDomainAccessCleanupOptionsTests
{
    [Fact]
    public void IsValid_WhenEnabledWithSafeDefaults_ReturnsTrue()
    {
        // Arrange
        var options = new ExportedDomainAccessCleanupOptions();

        // Act
        var isValid = ExportedDomainAccessCleanupOptions.IsValid(options);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(6, 1000, 24)]
    [InlineData(30, 0, 24)]
    [InlineData(30, 1000, 0)]
    public void IsValid_WhenEnabledWithUnsafeValues_ReturnsFalse(
        int retentionDays,
        int batchSize,
        int intervalHours)
    {
        // Arrange
        var options = new ExportedDomainAccessCleanupOptions
        {
            Enabled = true,
            RetentionDays = retentionDays,
            BatchSize = batchSize,
            IntervalHours = intervalHours
        };

        // Act
        var isValid = ExportedDomainAccessCleanupOptions.IsValid(options);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_WhenDisabledWithUnsafeValues_ReturnsTrue()
    {
        // Arrange
        var options = new ExportedDomainAccessCleanupOptions
        {
            Enabled = false,
            RetentionDays = 0,
            BatchSize = 0,
            IntervalHours = 0
        };

        // Act
        var isValid = ExportedDomainAccessCleanupOptions.IsValid(options);

        // Assert
        Assert.True(isValid);
    }
}
