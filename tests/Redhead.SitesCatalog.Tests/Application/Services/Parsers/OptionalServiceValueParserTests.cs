using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Application.Services.Parsers;

public class OptionalServiceValueParserTests
{
    [Theory]
    [InlineData("100", 100)]
    [InlineData(" 100.50 ", 100.50)]
    public void Parse_NumericValue_ReturnsAvailable(string rawValue, decimal expected)
    {
        var result = OptionalServiceValueParser.Parse(rawValue);

        Assert.True(result.IsValid);
        Assert.Equal(ServiceAvailabilityStatus.Available, result.Status);
        Assert.Equal(expected, result.Price);
    }

    [Theory]
    [InlineData("YES")]
    [InlineData("yes")]
    [InlineData(" Yes ")]
    public void Parse_YesValue_ReturnsAvailableWithUnknownPrice(string rawValue)
    {
        // Arrange
        // Act
        var result = OptionalServiceValueParser.Parse(rawValue);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ServiceAvailabilityStatus.AvailableWithUnknownPrice, result.Status);
        Assert.Null(result.Price);
    }

    [Theory]
    [InlineData("Y")]
    [InlineData("+")]
    [InlineData("AVAILABLE")]
    [InlineData("OK")]
    public void Parse_UnsupportedYesLikeValue_ReturnsError(string rawValue)
    {
        // Arrange
        // Act
        var result = OptionalServiceValueParser.Parse(rawValue);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Parse_ZeroPrice_ReturnsError()
    {
        // Arrange
        // Act
        var result = OptionalServiceValueParser.Parse("0");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Optional service price must be greater than 0.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyValue_ReturnsUnknown(string? rawValue)
    {
        var result = OptionalServiceValueParser.Parse(rawValue);

        Assert.True(result.IsValid);
        Assert.Equal(ServiceAvailabilityStatus.Unknown, result.Status);
        Assert.Null(result.Price);
    }

    [Theory]
    [InlineData("NO")]
    [InlineData("No")]
    [InlineData("n/a")]
    [InlineData("-")]
    public void Parse_NotAvailableMarker_ReturnsNotAvailable(string rawValue)
    {
        var result = OptionalServiceValueParser.Parse(rawValue);

        Assert.True(result.IsValid);
        Assert.Equal(ServiceAvailabilityStatus.NotAvailable, result.Status);
        Assert.Null(result.Price);
    }

    [Fact]
    public void Parse_InvalidValue_ReturnsError()
    {
        var result = OptionalServiceValueParser.Parse("abc");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }
}
