using Microsoft.AspNetCore.Authorization;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class AnalyticsControllerTests
{
    [Fact]
    public void AnalyticsController_UsesSuperAdminOnlyAuthorizationPolicy()
    {
        // Arrange
        var controllerType = typeof(AnalyticsController);
        var methods = new[]
        {
            controllerType.GetMethod(nameof(AnalyticsController.GetBusinessDemand)),
            controllerType.GetMethod(nameof(AnalyticsController.GetClientOptions))
        };

        // Act
        var controllerPolicies = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToList();

        // Assert
        Assert.Contains(AppPolicies.SuperAdminOnly, controllerPolicies);
        Assert.DoesNotContain(AppPolicies.AdminAccess, controllerPolicies);
        foreach (var method in methods)
        {
            Assert.NotNull(method);
            Assert.False(method!
                .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
                .Any());
        }
    }
}
