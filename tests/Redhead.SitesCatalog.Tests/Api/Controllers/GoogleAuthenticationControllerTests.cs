using Redhead.SitesCatalog.Api.Controllers;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class GoogleAuthenticationControllerTests
{
    [Theory]
    [InlineData("/sites", "/sites")]
    [InlineData("/sites?mode=multi#results", "/sites?mode=multi#results")]
    [InlineData("https://evil.example", "/sites")]
    [InlineData("//evil.example", "/sites")]
    [InlineData("/\\evil.example", "/sites")]
    [InlineData("/sites\r\nLocation:https://evil.example", "/sites")]
    [InlineData(null, "/sites")]
    public void NormalizeReturnUrl_OnlyAllowsLocalPaths(string? returnUrl, string expected)
    {
        // Arrange

        // Act
        var result = GoogleAuthenticationController.NormalizeReturnUrl(returnUrl);

        // Assert
        Assert.Equal(expected, result);
    }
}
