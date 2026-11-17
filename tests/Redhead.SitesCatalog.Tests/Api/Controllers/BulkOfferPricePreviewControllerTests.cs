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
    [InlineData(AppRoles.Editor, false)]
    [InlineData(AppRoles.Linkbuilder, false)]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Client, false)]
    [InlineData(AppRoles.Lite, false)]
    [InlineData(null, false)]
    public async Task ReportExports_RequireExportInAdditionToWorkflowPolicies_EvenForDirectOperationIds(string? role, bool allowed)
    {
        // Arrange
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
        var controller = typeof(BulkOfferPricePreviewController);
        var methods = new[] { nameof(BulkOfferPricePreviewController.UpdateReport), nameof(BulkOfferPricePreviewController.CatalogCalculation),
            nameof(BulkOfferPricePreviewController.UpdateReportToGoogleDrive), nameof(BulkOfferPricePreviewController.CatalogCalculationToGoogleDrive) };

        // Act
        foreach (var method in methods)
        {
            var policies = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
                .Concat(controller.GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>())
                .Select(attribute => attribute.Policy!).ToArray();
            var results = await Task.WhenAll(policies.Select(policy => provider.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(principal, null, policy)));

            // Assert
            Assert.Contains(AppPolicies.WebmasterOffersReadAccess, policies);
            Assert.Contains(AppPolicies.WebmasterOffersManageAccess, policies);
            Assert.Contains(AppPolicies.WebmasterOffersExportAccess, policies);
            Assert.Equal(allowed, results.All(result => result.Succeeded));
        }
    }

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
    public async Task BulkWorkflow_RequiresFullReadAndManageAccess(string? role, bool allowed)
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
        foreach (var method in typeof(BulkOfferPricePreviewController).GetMethods()
            .Where(method => method.DeclaringType == typeof(BulkOfferPricePreviewController)))
        {
            Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        }
    }
}
