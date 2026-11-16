using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class BulkOfferPricePreviewControllerTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.Editor, true)]
    [InlineData(AppRoles.Linkbuilder, false)]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Client, false)]
    [InlineData(AppRoles.Lite, false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public async Task Preview_RequiresFullReadAndManageAccess(string? role, bool allowed)
    {
        // Arrange
        var policies = typeof(BulkOfferPricePreviewController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Select(attribute => attribute.Policy!).ToArray();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var (permission, policy) in AppPolicies.PermissionPolicies)
            {
                options.AddPolicy(policy, builder => builder.RequireRole(RolePermissionMatrix.GetRolesForPermission(permission)));
            }
        });
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(role is null ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

        // Act
        var results = await Task.WhenAll(policies.Select(policy => provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, null, policy)));

        // Assert
        Assert.Contains(AppPolicies.WebmasterOffersReadAccess, policies);
        Assert.Contains(AppPolicies.WebmasterOffersManageAccess, policies);
        Assert.Equal(allowed, results.All(result => result.Succeeded));
        var cache = Assert.Single(typeof(BulkOfferPricePreviewController).GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>());
        Assert.True(cache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }
}
