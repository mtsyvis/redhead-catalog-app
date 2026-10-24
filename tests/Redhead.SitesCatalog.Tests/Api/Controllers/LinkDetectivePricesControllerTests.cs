using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class LinkDetectivePricesControllerTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.Editor, false)]
    [InlineData(AppRoles.Linkbuilder, false)]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Client, false)]
    [InlineData(AppRoles.Lite, false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public async Task Authorization_AllowsOnlyAdmins(string? role, bool allowed)
    {
        // Arrange
        var policy = Assert.Single(typeof(LinkDetectivePricesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var (permission, policyName) in AppPolicies.PermissionPolicies)
            {
                options.AddPolicy(policyName, builder => builder.RequireRole(
                    RolePermissionMatrix.GetRolesForPermission(permission)));
            }
        });
        using var provider = services.BuildServiceProvider();
        var user = new ClaimsPrincipal(role is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

        // Act
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy);

        // Assert
        Assert.Equal(AppPolicies.LinkDetectivePricesReadAccess, policy);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task GetPrices_NormalizesOneDomainAndForwardsManualRefresh()
    {
        // Arrange
        var service = new Mock<ILinkDetectivePricesService>(MockBehavior.Strict);
        var expected = new LinkDetectivePricesResult("example.com", 1m, 2m, 2m, DateTime.UtcNow, false, null, null);
        service.Setup(value => value.GetAsync("example.com", true, CancellationToken.None)).ReturnsAsync(expected);
        var controller = new LinkDetectivePricesController(service.Object);

        // Act
        var result = await controller.GetPrices(new LinkDetectivePricesRequest("https://www.EXAMPLE.com/path", true), CancellationToken.None);

        // Assert
        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("first.example second.example")]
    [InlineData("127.0.0.1")]
    public async Task GetPrices_RejectsInvalidOrMultipleDomains(string? domain)
    {
        // Arrange
        var service = new Mock<ILinkDetectivePricesService>(MockBehavior.Strict);
        var controller = new LinkDetectivePricesController(service.Object);

        // Act
        var result = await controller.GetPrices(new LinkDetectivePricesRequest(domain), CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
