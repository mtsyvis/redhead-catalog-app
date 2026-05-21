using Redhead.SitesCatalog.Application.Validation;

namespace Redhead.SitesCatalog.Tests.Api.Validation;

public sealed class UserProfileNameValidatorTests
{
    [Fact]
    public void Validate_WithValidUnicodeNames_TrimsAndReturnsSuccess()
    {
        // Arrange
        var firstName = "  José  ";
        var lastName = "  Иванова  ";

        // Act
        var result = UserProfileNameValidator.Validate(firstName, lastName);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("José", result.FirstName);
        Assert.Equal("Иванова", result.LastName);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null, "Smith", "firstName")]
    [InlineData("   ", "Smith", "firstName")]
    [InlineData("Jane", null, "lastName")]
    [InlineData("Jane", "   ", "lastName")]
    public void Validate_WithMissingOrWhitespaceName_ReturnsFieldError(
        string? firstName,
        string? lastName,
        string expectedField)
    {
        // Arrange

        // Act
        var result = UserProfileNameValidator.Validate(firstName, lastName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(expectedField, result.Errors.Keys);
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    public void Validate_WithTooLongName_ReturnsFieldError(string expectedField)
    {
        // Arrange
        var longName = new string('a', UserProfileNameValidator.MaxNameLength + 1);
        var firstName = expectedField == "firstName" ? longName : "Jane";
        var lastName = expectedField == "lastName" ? longName : "Smith";

        // Act
        var result = UserProfileNameValidator.Validate(firstName, lastName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(expectedField, result.Errors.Keys);
    }

    [Fact]
    public void IsProfileComplete_WithNullExistingNames_ReturnsFalse()
    {
        // Arrange

        // Act
        var complete = UserProfileNameValidator.IsProfileComplete(null, null);

        // Assert
        Assert.False(complete);
    }
}
