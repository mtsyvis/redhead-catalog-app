using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class WebmasterPriceComparisonControllerTests
{
    [Theory]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.Editor, false)]
    [InlineData(AppRoles.Linkbuilder, false)]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Client, false)]
    [InlineData(AppRoles.Lite, false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public async Task Authorization_AllowsOnlyAdmins(string? role, bool expected)
    {
        // Arrange
        var policy = Assert.Single(typeof(WebmasterPriceComparisonController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var (permission, name) in AppPolicies.PermissionPolicies)
            {
                options.AddPolicy(name, builder => builder.RequireRole(RolePermissionMatrix.GetRolesForPermission(permission)));
            }
        });
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(role is null ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

        // Act
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy);

        // Assert
        Assert.Equal(AppPolicies.WebmasterPriceComparisonReadAccess, policy);
        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.example b.example")]
    [InlineData("127.0.0.1")]
    public async Task GetByDomain_RejectsInvalidOrMultipleDomains(string? domain)
    {
        // Arrange
        var service = new Mock<IWebmasterPriceComparisonService>(MockBehavior.Strict);
        var controller = new WebmasterPriceComparisonController(service.Object);

        // Act
        var result = await controller.GetByDomain(domain, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetByDomain_NormalizesDomainAndDisablesResponseCaching()
    {
        // Arrange
        var expected = new WebmasterPriceComparisonResult("example.com", true, null, [], []);
        var service = new Mock<IWebmasterPriceComparisonService>(MockBehavior.Strict);
        service.Setup(value => value.GetByDomainAsync("example.com", CancellationToken.None)).ReturnsAsync(expected);
        var controller = new WebmasterPriceComparisonController(service.Object);

        // Act
        var result = await controller.GetByDomain("https://www.EXAMPLE.com/path", CancellationToken.None);

        // Assert
        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        var cache = Assert.Single(typeof(WebmasterPriceComparisonController)
            .GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>());
        Assert.True(cache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }
}
