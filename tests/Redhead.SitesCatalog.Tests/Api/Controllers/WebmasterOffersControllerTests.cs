using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class WebmasterOffersControllerTests
{
    [Theory]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Linkbuilder, true)]
    [InlineData(AppRoles.Editor, true)]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.SuperAdmin, true)]
    public async Task GetByDomain_ReturnsOnlyTheResponseAllowedByTheRole(string role, bool fullAccess)
    {
        // Arrange
        var service = new Mock<IWebmasterOffersService>(MockBehavior.Strict);
        object expected;
        if (fullAccess)
        {
            var response = new WebmasterOffersSearchResult { Domain = "example.org", SiteFound = true };
            service.Setup(x => x.GetByDomainAsync("example.org", CancellationToken.None)).ReturnsAsync(response);
            expected = response;
        }
        else
        {
            var response = new WebmasterOffersSummarySearchResult { Domain = "example.org", SiteFound = true };
            service.Setup(x => x.GetSummaryByDomainAsync("example.org", CancellationToken.None)).ReturnsAsync(response);
            expected = response;
        }
        var controller = new WebmasterOffersController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(role) }
            }
        };

        // Act
        var result = await controller.GetByDomain("example.org", CancellationToken.None);

        // Assert
        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersSummaryReadAccess, true)]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersReadAccess, false)]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersManageAccess, false)]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersImportAccess, false)]
    [InlineData(AppRoles.Client, AppPolicies.WebmasterOffersSummaryReadAccess, false)]
    [InlineData(AppRoles.Lite, AppPolicies.WebmasterOffersSummaryReadAccess, false)]
    public async Task Authorization_EnforcesTheConfiguredPermissionPolicies(string role, string policy, bool expected)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var (permission, policyName) in AppPolicies.PermissionPolicies)
            {
                options.AddPolicy(policyName, builder => builder.RequireRole(RolePermissionMatrix.GetRolesForPermission(permission)));
            }
        });
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        // Act
        var result = await authorization.AuthorizeAsync(CreatePrincipal(role), null, policy);

        // Assert
        Assert.Equal(expected, result.Succeeded);
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));
}
