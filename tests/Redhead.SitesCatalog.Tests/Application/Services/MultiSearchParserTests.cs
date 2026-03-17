using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Tests;

public class MultiSearchParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("\t\n  \r\n")]
    public void Parse_WithNullOrWhitespace_ReturnsEmptyResult(string? queryText)
    {
        var result = MultiSearchParser.Parse(queryText);

        Assert.Empty(result.UniqueDomains);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void Parse_SplitsBySpaces()
    {
        var result = MultiSearchParser.Parse("a.com  b.com c.com");

        Assert.Equal(3, result.UniqueDomains.Count);
        Assert.Contains("a.com", result.UniqueDomains);
        Assert.Contains("b.com", result.UniqueDomains);
        Assert.Contains("c.com", result.UniqueDomains);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void Parse_SplitsByNewlinesAndTabs()
    {
        var result = MultiSearchParser.Parse("x.com\ny.com\tz.com\r\nw.com");

        Assert.Equal(4, result.UniqueDomains.Count);
        Assert.Contains("x.com", result.UniqueDomains);
        Assert.Contains("y.com", result.UniqueDomains);
        Assert.Contains("z.com", result.UniqueDomains);
        Assert.Contains("w.com", result.UniqueDomains);
    }

    [Fact]
    public void Parse_ConsecutiveWhitespace_ProducesSingleInputs()
    {
        var result = MultiSearchParser.Parse("  a.com   b.com  ");

        Assert.Equal(2, result.UniqueDomains.Count);
        Assert.Contains("a.com", result.UniqueDomains);
        Assert.Contains("b.com", result.UniqueDomains);
    }

    [Fact]
    public void Parse_MoreThan500Inputs_ThrowsRequestValidationException()
    {
        var tokens = Enumerable.Range(0, 501).Select(i => $"domain{i}.com").ToArray();
        var queryText = string.Join(' ', tokens);

        var ex = Assert.Throws<RequestValidationException>(() => MultiSearchParser.Parse(queryText));

        Assert.Contains(MultiSearchConstants.MaxInputs.ToString(), ex.Message);
        Assert.Contains("501", ex.Message);
    }

    [Fact]
    public void Parse_Exactly500Inputs_DoesNotThrow()
    {
        var tokens = Enumerable.Range(0, 500).Select(i => $"domain{i}.com").ToArray();
        var queryText = string.Join(' ', tokens);

        var result = MultiSearchParser.Parse(queryText);

        Assert.Equal(500, result.UniqueDomains.Count);
    }

    [Fact]
    public void Parse_NormalizesEachInput()
    {
        var result = MultiSearchParser.Parse("https://www.Example.COM/path  HTTP://b.com/");

        Assert.Equal(2, result.UniqueDomains.Count);
        Assert.Contains("example.com", result.UniqueDomains);
        Assert.Contains("b.com", result.UniqueDomains);
    }

    [Fact]
    public void Parse_ExactMatchOnly_NoSubstringMatching()
    {
        var result = MultiSearchParser.Parse("example.com example example.com.org");

        Assert.Equal(3, result.UniqueDomains.Count);
        Assert.Contains("example.com", result.UniqueDomains);
        Assert.Contains("example", result.UniqueDomains);
        Assert.Contains("example.com.org", result.UniqueDomains);
    }

    [Fact]
    public void Parse_DetectsDuplicatesAfterNormalization()
    {
        var result = MultiSearchParser.Parse("a.com b.com a.com https://A.COM/ b.com");

        Assert.Equal(2, result.UniqueDomains.Count);
        Assert.Contains("a.com", result.UniqueDomains);
        Assert.Contains("b.com", result.UniqueDomains);

        Assert.Equal(2, result.Duplicates.Count);
        Assert.Contains("a.com", result.Duplicates);
        Assert.Contains("b.com", result.Duplicates);
    }

    [Fact]
    public void Parse_DuplicatesListIsUnique()
    {
        var result = MultiSearchParser.Parse("x.com x.com x.com");

        Assert.Single(result.UniqueDomains);
        Assert.Single(result.Duplicates);
        Assert.Equal("x.com", result.Duplicates[0]);
    }

    [Fact]
    public void Parse_EmptyAfterNormalization_ExcludedFromUniqueAndDuplicates()
    {
        var result = MultiSearchParser.Parse("https:///path  \t  ");

        Assert.Empty(result.UniqueDomains);
        Assert.Empty(result.Duplicates);
    }
}
