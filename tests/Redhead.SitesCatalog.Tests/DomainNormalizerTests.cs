using Redhead.SitesCatalog.Domain;

namespace Redhead.SitesCatalog.Tests;

public class DomainNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  \t\n  ", "")]
    public void Normalize_WithNullOrWhitespace_ReturnsEmpty(string? input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com", "example.com")]
    [InlineData("https://example.com", "example.com")]
    [InlineData("HTTP://example.com", "example.com")]
    [InlineData("HTTPS://example.com", "example.com")]
    [InlineData("HtTp://example.com", "example.com")]
    [InlineData("HtTpS://example.com", "example.com")]
    public void Normalize_WithScheme_RemovesScheme(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("www.example.com", "example.com")]
    [InlineData("WWW.example.com", "example.com")]
    [InlineData("WwW.example.com", "example.com")]
    [InlineData("https://www.example.com", "example.com")]
    [InlineData("http://www.example.com", "example.com")]
    [InlineData("www.subdomain.example.com", "subdomain.example.com")]
    public void Normalize_WithWww_RemovesWww(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("example.com/", "example.com")]
    [InlineData("example.com//", "example.com")]
    [InlineData("https://example.com/", "example.com")]
    [InlineData("https://www.example.com/", "example.com")]
    public void Normalize_WithTrailingSlash_RemovesTrailingSlash(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("example.com/path", "example.com")]
    [InlineData("example.com/path/to/page", "example.com")]
    [InlineData("https://example.com/path", "example.com")]
    [InlineData("https://www.example.com/path/to/page", "example.com")]
    [InlineData("example.com/path?query=value", "example.com")]
    public void Normalize_WithPath_KeepsOnlyHost(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("example.com?query=value", "example.com")]
    [InlineData("example.com?q=1&param=2", "example.com")]
    [InlineData("https://example.com?query=value", "example.com")]
    [InlineData("https://www.example.com?query=value", "example.com")]
    public void Normalize_WithQuery_KeepsOnlyHost(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("example.com#fragment", "example.com")]
    [InlineData("example.com#section1", "example.com")]
    [InlineData("https://example.com#fragment", "example.com")]
    [InlineData("https://www.example.com#fragment", "example.com")]
    public void Normalize_WithFragment_KeepsOnlyHost(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Example.COM", "example.com")]
    [InlineData("EXAMPLE.COM", "example.com")]
    [InlineData("ExAmPlE.CoM", "example.com")]
    [InlineData("HTTPS://WWW.EXAMPLE.COM/PATH", "example.com")]
    public void Normalize_WithMixedCase_ConvertsToLowercase(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  example.com  ", "example.com")]
    [InlineData("\texample.com\t", "example.com")]
    [InlineData("\nexample.com\n", "example.com")]
    [InlineData("  https://www.example.com/path  ", "example.com")]
    public void Normalize_WithWhitespace_TrimsWhitespace(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://www.Example.com/path?q=1", "example.com")]
    [InlineData("HTTP://WWW.EXAMPLE.COM/PATH?QUERY=VALUE#FRAGMENT", "example.com")]
    [InlineData("  https://www.Example.COM/path/to/page?q=1&p=2#section  ", "example.com")]
    [InlineData("HtTpS://WwW.ExAmPlE.cOm/", "example.com")]
    public void Normalize_WithComplexInput_NormalizesCorrectly(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("subdomain.example.com", "subdomain.example.com")]
    [InlineData("sub.domain.example.com", "sub.domain.example.com")]
    public void Normalize_WithAlreadyNormalizedDomain_RemainsUnchanged(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("example.co.uk", "example.co.uk")]
    [InlineData("example.com.au", "example.com.au")]
    [InlineData("https://www.example.co.uk/path", "example.co.uk")]
    public void Normalize_WithComplexTld_HandlesCorrectly(string input, string expected)
    {
        // Act
        var result = DomainNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        // Arrange
        var input = "https://www.Example.com/path?q=1";
        
        // Act
        var firstNormalization = DomainNormalizer.Normalize(input);
        var secondNormalization = DomainNormalizer.Normalize(firstNormalization);
        
        // Assert
        Assert.Equal(firstNormalization, secondNormalization);
        Assert.Equal("example.com", firstNormalization);
    }
}
