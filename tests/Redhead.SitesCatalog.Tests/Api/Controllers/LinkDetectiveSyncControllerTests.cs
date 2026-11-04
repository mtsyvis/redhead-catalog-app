using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class LinkDetectiveSyncControllerTests
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
    public async Task Monitoring_IsReadOnlyAndAuthorizedOnlyForAdmins(string? role, bool expected)
    {
        // Arrange
        var policy = Assert.Single(typeof(LinkDetectiveSyncController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy(policy, builder => builder.RequireAuthenticatedUser()
            .RequireRole(RolePermissionMatrix.GetRolesForPermission(AppPermissions.LinkDetectiveSyncRead))));
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(role is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

        // Act
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy);

        // Assert
        Assert.Equal(AppPolicies.LinkDetectiveSyncReadAccess, policy);
        Assert.Equal(expected, result.Succeeded);
        Assert.All(typeof(LinkDetectiveSyncController).GetMethods().Where(method => method.DeclaringType == typeof(LinkDetectiveSyncController)),
            method => Assert.DoesNotContain(method.GetCustomAttributes(true), attribute => attribute.GetType().Name is "HttpPostAttribute" or "HttpPutAttribute" or "HttpDeleteAttribute"));
    }
}
