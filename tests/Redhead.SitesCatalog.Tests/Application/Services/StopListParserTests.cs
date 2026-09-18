using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class StopListParserTests
{
    [Fact]
    public void Parse_NormalizesAndDeduplicates_WithoutApplyingStricterDnsRules()
    {
        // Arrange
        string[] input = ["https://www.Example.com/path", "example.com", "127.0.0.1", "example.c", "пример.рф"];

        // Act
        var result = StopListParser.Parse(input);

        // Assert
        Assert.Equal(["127.0.0.1", "example.c", "example.com", "пример.рф"], result);
    }

    [Theory]
    [InlineData("bad..com")]
    [InlineData("bad_name.com")]
    [InlineData("name@example.com")]
    public void Parse_InvalidDomainAlongsideValidDomain_RejectsTheList(string invalidDomain)
    {
        // Arrange
        string[] input = ["example.com", invalidDomain];

        // Act
        var exception = Assert.Throws<RequestValidationException>(() => StopListParser.Parse(input));

        // Assert
        Assert.Contains($"Invalid stop-list domain '{invalidDomain}'", exception.Message);
    }
}
