using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Locations;

public class LocationNormalizerTests
{
    private readonly LocationNormalizer _normalizer = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyValue_ReturnsUnknown(string? value)
    {
        // Arrange

        // Act
        var result = _normalizer.Normalize(value);

        // Assert
        Assert.Equal(LocationNormalizationStatus.Unknown, result.Status);
        Assert.Equal(LocationConstants.UnknownLocationKey, result.LocationKey);
    }

    [Theory]
    [InlineData("US", "US")]
    [InlineData("us", "US")]
    [InlineData("USA", "US")]
    [InlineData("United States", "US")]
    [InlineData("United States of America", "US")]
    [InlineData("GB", "GB")]
    [InlineData("UK", "GB")]
    [InlineData("United Kingdom", "GB")]
    [InlineData("Great Britain", "GB")]
    [InlineData("UAE", "AE")]
    [InlineData("United Arab Emirates", "AE")]
    [InlineData("Korea", "KR")]
    [InlineData("South Korea", "KR")]
    [InlineData("VC", "VC")]
    public void Normalize_KnownValues_ReturnsExpectedLocationKey(string value, string expectedLocationKey)
    {
        // Arrange

        // Act
        var result = _normalizer.Normalize(value);

        // Assert
        Assert.Equal(LocationNormalizationStatus.Known, result.Status);
        Assert.Equal(expectedLocationKey, result.LocationKey);
    }

    [Fact]
    public void Normalize_UnmappedNonEmptyValue_ReturnsUnmapped()
    {
        // Arrange
        const string value = "not a real location value";

        // Act
        var result = _normalizer.Normalize(value);

        // Assert
        Assert.Equal(LocationNormalizationStatus.Unmapped, result.Status);
        Assert.Null(result.LocationKey);
        Assert.Equal(value, result.RawValue);
    }
}
