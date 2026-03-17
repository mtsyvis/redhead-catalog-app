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
