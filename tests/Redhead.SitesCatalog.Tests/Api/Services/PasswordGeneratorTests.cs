using System.Text.RegularExpressions;
using Redhead.SitesCatalog.Api.Services;

namespace Redhead.SitesCatalog.Tests.Api.Services;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generate_DefaultLength_Returns12Characters()
    {
        var password = PasswordGenerator.Generate();
        Assert.Equal(12, password.Length);
    }

    [Fact]
    public void Generate_CustomLength_ReturnsRequestedLength()
    {
        var password = PasswordGenerator.Generate(8);
        Assert.Equal(8, password.Length);
    }

    [Fact]
    public void Generate_ContainsDigit()
    {
        var password = PasswordGenerator.Generate();
        Assert.Matches(@"\d", password);
    }

    [Fact]
    public void Generate_ContainsUppercase()
    {
        var password = PasswordGenerator.Generate();
        Assert.Matches(@"[A-Z]", password);
    }

    [Fact]
    public void Generate_ContainsLowercase()
    {
        var password = PasswordGenerator.Generate();
        Assert.Matches(@"[a-z]", password);
    }

    [Fact]
    public void Generate_ContainsSpecialCharacter()
    {
        var password = PasswordGenerator.Generate();
        Assert.Matches(@"[!@#$%&*]", password);
    }

    [Fact]
    public void Generate_MultipleCalls_ProduceDifferentPasswords()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            var password = PasswordGenerator.Generate();
            set.Add(password);
        }
        Assert.True(set.Count > 1, "Generator should produce different passwords");
    }

    [Fact]
    public void Generate_MeetsIdentityComplexity_AllRequirements()
    {
        for (var i = 0; i < 20; i++)
        {
            var password = PasswordGenerator.Generate();
            Assert.True(password.Length >= 8, "Length >= 8");
            Assert.True(Regex.IsMatch(password, @"\d"), "Contains digit");
            Assert.True(Regex.IsMatch(password, @"[A-Z]"), "Contains upper");
            Assert.True(Regex.IsMatch(password, @"[a-z]"), "Contains lower");
            Assert.True(Regex.IsMatch(password, @"[^a-zA-Z0-9]"), "Contains special");
        }
    }
}
