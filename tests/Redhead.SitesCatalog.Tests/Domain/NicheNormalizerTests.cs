using Redhead.SitesCatalog.Domain;

namespace Redhead.SitesCatalog.Tests.Domain;

public class NicheNormalizerTests
{
    [Theory]
    [InlineData("Crypto, Finance, crypto", new[] { "crypto", "finance" })]
    [InlineData(" Mental health , Web development ", new[] { "mental health", "web development" })]
    [InlineData("Crypto   News,  Web    development", new[] { "crypto news", "web development" })]
    public void NormalizeTokens_ReturnsNormalizedDistinctTokens(string input, string[] expected)
    {
        var result = NicheNormalizer.NormalizeTokens(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("N/A")]
    [InlineData("NA")]
    [InlineData("-")]
    [InlineData("None")]
    [InlineData("null")]
    public void NormalizeTokens_InvalidEmptyMarkers_ReturnEmpty(string? input)
    {
        var result = NicheNormalizer.NormalizeTokens(input);

        Assert.Empty(result);
    }
}
