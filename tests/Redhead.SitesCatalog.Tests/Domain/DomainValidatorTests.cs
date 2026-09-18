using Redhead.SitesCatalog.Domain;

namespace Redhead.SitesCatalog.Tests.Domain;

public sealed class DomainValidatorTests
{
    [Theory]
    [InlineData("example.com", true, true)]
    [InlineData("news.example.co.uk", true, true)]
    [InlineData("пример.рф", true, true)]
    [InlineData("xn--e1afmkfd.xn--p1ai", true, true)]
    [InlineData("e\u0301xample.com", true, true)]
    [InlineData("example\u3002com", false, true)]
    [InlineData("hello", false, false)]
    [InlineData("localhost", false, false)]
    [InlineData("127.0.0.1", true, false)]
    [InlineData("2001:db8::1", false, false)]
    [InlineData("name@example.com", false, false)]
    [InlineData("example.com:443", false, false)]
    [InlineData("bad_name.com", false, false)]
    [InlineData("bad..com", false, false)]
    [InlineData("-bad.com", false, false)]
    [InlineData("bad-.com", false, false)]
    [InlineData("example.123", true, false)]
    [InlineData("example.c", true, false)]
    [InlineData("example.com.", false, false)]
    [InlineData("https://example.com/path", false, false)]
    [InlineData("example .com", false, false)]
    [InlineData("", false, false)]
    [InlineData(null, false, false)]
    [MemberData(nameof(LengthCases))]
    public void Validation_PreservesBasicAndDnsRules(string? input, bool basicExpected, bool dnsExpected)
    {
        // Arrange
        var domain = input;

        // Act
        var basicValid = DomainValidator.IsValidNormalizedDomain(domain);
        var dnsValid = DomainValidator.IsValidDnsDomain(domain);

        // Assert
        Assert.Equal(basicExpected, basicValid);
        Assert.Equal(dnsExpected, dnsValid);
    }

    public static IEnumerable<object[]> LengthCases()
    {
        yield return [new string('a', 63) + ".com", true, true];
        yield return [new string('a', 64) + ".com", false, false];
        yield return [string.Join('.', Enumerable.Repeat(new string('a', 63), 3)) + "." + new string('b', 61), true, true];
        yield return [string.Join('.', Enumerable.Repeat(new string('a', 63), 3)) + "." + new string('b', 62), false, false];
        yield return [new string('я', 63) + ".рф", true, false];
    }
}
