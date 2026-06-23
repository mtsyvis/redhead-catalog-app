using Redhead.SitesCatalog.Application.Validation;

namespace Redhead.SitesCatalog.Tests.Api.Validation;

public class UserDisplayNameValidatorTests
{
    [Fact]
    public void Validate_WhenDisplayNameHasWhitespace_TrimsValue()
    {
        // Arrange
        const string displayName = "  José Иванова  ";

        // Act
        var result = UserDisplayNameValidator.Validate(displayName);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("José Иванова", result.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenDisplayNameIsMissing_ReturnsRequiredError(string? displayName)
    {
        // Arrange

        // Act
        var result = UserDisplayNameValidator.Validate(displayName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("displayName", result.Errors.Keys);
    }

    [Fact]
    public void Validate_WhenDisplayNameIsTooLong_ReturnsLengthError()
    {
        // Arrange
        var displayName = new string('a', UserDisplayNameValidator.MaxLength + 1);

        // Act
        var result = UserDisplayNameValidator.Validate(displayName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("displayName", result.Errors.Keys);
    }
}
