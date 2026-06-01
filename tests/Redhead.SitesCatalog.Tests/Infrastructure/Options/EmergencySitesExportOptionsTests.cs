using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Options;

public sealed class EmergencySitesExportOptionsTests
{
    [Fact]
    public void IsValid_WhenDisabled_AllowsMissingExportSettings()
    {
        // Arrange
        var options = new EmergencySitesExportOptions
        {
            Enabled = false,
            ScheduleCron = "",
            GoogleDriveFolderId = "",
            ServiceAccountJsonPath = "",
            RetentionWeeks = 0,
            FilePrefix = "",
            UploadTimeoutMinutes = 0
        };

        // Act
        var isValid = EmergencySitesExportOptions.IsValid(options);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WhenEnabledAndScheduleIsMissing_ReturnsFalse()
    {
        // Arrange
        var options = new EmergencySitesExportOptions
        {
            Enabled = true,
            ScheduleCron = "",
            GoogleDriveFolderId = "folder-1",
            ServiceAccountJsonPath = "/run/secrets/google-service-account.json",
            RetentionWeeks = 8,
            FilePrefix = "redhead-sites-full",
            UploadTimeoutMinutes = 30
        };

        // Act
        var isValid = EmergencySitesExportOptions.IsValid(options);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_WhenEnabledAndRequiredSettingsArePresent_ReturnsTrue()
    {
        // Arrange
        var options = new EmergencySitesExportOptions
        {
            Enabled = true,
            ScheduleCron = "30 3 * * MON",
            GoogleDriveFolderId = "folder-1",
            ServiceAccountJsonPath = "/run/secrets/google-service-account.json",
            RetentionWeeks = 8,
            FilePrefix = "redhead-sites-full",
            UploadTimeoutMinutes = 30
        };

        // Act
        var isValid = EmergencySitesExportOptions.IsValid(options);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WhenEnabledAndUploadTimeoutIsNotPositive_ReturnsFalse()
    {
        // Arrange
        var options = new EmergencySitesExportOptions
        {
            Enabled = true,
            ScheduleCron = "30 3 * * MON",
            GoogleDriveFolderId = "folder-1",
            ServiceAccountJsonPath = "/run/secrets/google-service-account.json",
            RetentionWeeks = 8,
            FilePrefix = "redhead-sites-full",
            UploadTimeoutMinutes = 0
        };

        // Act
        var isValid = EmergencySitesExportOptions.IsValid(options);

        // Assert
        Assert.False(isValid);
    }
}
