using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class PricingControllerTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.Editor, false)]
    [InlineData(AppRoles.Linkbuilder, false)]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Client, false)]
    [InlineData(AppRoles.Lite, false)]
    [InlineData(null, false)]
    public async Task Pricing_ReadAndWriteRequireAdminPermissions(string? role, bool expected)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var policy in AppPolicies.PermissionPolicies)
                options.AddPolicy(policy.Value, builder => builder.RequireAuthenticatedUser().RequireRole(RolePermissionMatrix.GetRolesForPermission(policy.Key)));
        });
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(role is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));
        var readPolicies = typeof(PricingController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Select(attribute => attribute.Policy!).ToList();
        var writePolicy = Assert.Single(typeof(PricingController).GetMethod(nameof(PricingController.Save))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy!;

        // Act
        var results = await Task.WhenAll(readPolicies.Append(writePolicy).Select(policy =>
            provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy)));

        // Assert
        Assert.Contains(AppPolicies.PricingReadAccess, readPolicies);
        Assert.Contains(AppPolicies.LinkDetectivePricesReadAccess, readPolicies);
        Assert.Equal(AppPolicies.PricingEditAccess, writePolicy);
        Assert.All(results, result => Assert.Equal(expected, result.Succeeded));
    }
}
