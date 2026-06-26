using Microsoft.AspNetCore.Authorization;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ControllerPolicyTests
{
    [Theory]
    [InlineData(typeof(SitesController), nameof(SitesController.SearchSites), AppPolicies.SitesBrowseAccess)]
    [InlineData(typeof(SitesController), nameof(SitesController.GetLocations), AppPolicies.SitesBrowseAccess)]
    [InlineData(typeof(SitesController), nameof(SitesController.GetFilterOptions), AppPolicies.SitesBrowseAccess)]
    [InlineData(typeof(SitesController), nameof(SitesController.MultiSearch), AppPolicies.SitesMultiSearchAccess)]
    [InlineData(typeof(SitesController), nameof(SitesController.UpdateSite), AppPolicies.SitesEditAccess)]
    [InlineData(typeof(MeSavedFilterSetsController), nameof(MeSavedFilterSetsController.GetFilterSets), AppPolicies.SitesBrowseAccess)]
    [InlineData(typeof(MeSavedFilterSetsController), nameof(MeSavedFilterSetsController.CreateFilterSet), AppPolicies.SitesBrowseAccess)]
    [InlineData(typeof(MeSavedFilterSetsController), nameof(MeSavedFilterSetsController.UpdateFilterSet), AppPolicies.SitesBrowseAccess)]
    [InlineData(typeof(MeSavedFilterSetsController), nameof(MeSavedFilterSetsController.DeleteFilterSet), AppPolicies.SitesBrowseAccess)]
    [InlineData(typeof(MeTableViewsController), nameof(MeTableViewsController.GetTableViews), AppPolicies.TableViewsManageAccess)]
    [InlineData(typeof(MeTableViewsController), nameof(MeTableViewsController.SetActiveView), AppPolicies.TableViewsManageAccess)]
    [InlineData(typeof(MeTableViewsController), nameof(MeTableViewsController.CreateCustomView), AppPolicies.TableViewsManageAccess)]
    [InlineData(typeof(MeTableViewsController), nameof(MeTableViewsController.UpdateCustomView), AppPolicies.TableViewsManageAccess)]
    [InlineData(typeof(MeTableViewsController), nameof(MeTableViewsController.DeleteCustomView), AppPolicies.TableViewsManageAccess)]
    [InlineData(typeof(ExportController), nameof(ExportController.ExportSitesFromBody), AppPolicies.SitesExportAccess)]
    [InlineData(typeof(ExportController), nameof(ExportController.ExportSitesMultiSearch), AppPolicies.SitesExportAccess)]
    [InlineData(typeof(ExportController), nameof(ExportController.ExportSitesToGoogleDrive), AppPolicies.SitesExportAccess)]
    [InlineData(typeof(GoogleDriveIntegrationController), nameof(GoogleDriveIntegrationController.Status), AppPolicies.SitesExportAccess)]
    [InlineData(typeof(GoogleDriveIntegrationController), nameof(GoogleDriveIntegrationController.StartConnection), AppPolicies.SitesExportAccess)]
    [InlineData(typeof(GoogleDriveIntegrationController), nameof(GoogleDriveIntegrationController.Disconnect), AppPolicies.SitesExportAccess)]
    [InlineData(typeof(ImportController), nameof(ImportController.ImportSites), AppPolicies.ImportsRunAccess)]
    [InlineData(typeof(ImportController), nameof(ImportController.ImportSitesUpdate), AppPolicies.ImportsRunAccess)]
    [InlineData(typeof(ImportController), nameof(ImportController.ImportAvailability), AppPolicies.ImportsRunAccess)]
    [InlineData(typeof(ImportController), nameof(ImportController.ImportLastPublished), AppPolicies.ImportsRunAccess)]
    [InlineData(typeof(AnalyticsController), nameof(AnalyticsController.GetBusinessDemand), AppPolicies.AnalyticsReadAccess)]
    [InlineData(typeof(AnalyticsController), nameof(AnalyticsController.GetExportActivity), AppPolicies.AnalyticsReadAccess)]
    [InlineData(typeof(AnalyticsController), nameof(AnalyticsController.GetExportLogDetails), AppPolicies.AnalyticsReadAccess)]
    [InlineData(typeof(AnalyticsController), nameof(AnalyticsController.GetClientOptions), AppPolicies.AnalyticsReadAccess)]
    public void Endpoint_UsesExpectedPolicy(Type controllerType, string methodName, string expectedPolicy)
    {
        // Arrange

        // Act
        var policies = GetPolicies(controllerType, methodName);

        // Assert
        Assert.Contains(expectedPolicy, policies);
    }

    [Theory]
    [InlineData(nameof(AdminUsersController.ListUsers), AppPolicies.UsersReadAccess)]
    [InlineData(nameof(AdminUsersController.GetUser), AppPolicies.UsersReadAccess)]
    [InlineData(nameof(AdminUsersController.CreateUser), AppPolicies.UsersManageAccess)]
    [InlineData(nameof(AdminUsersController.ResetPassword), AppPolicies.UsersManageAccess)]
    [InlineData(nameof(AdminUsersController.DisableUser), AppPolicies.UsersManageAccess)]
    [InlineData(nameof(AdminUsersController.UpdateUserRole), AppPolicies.UsersManageAccess)]
    [InlineData(nameof(AdminUsersController.ReactivateUser), AppPolicies.UsersManageAccess)]
    [InlineData(nameof(AdminUsersController.ReissueInvitation), AppPolicies.UsersManageAccess)]
    [InlineData(nameof(AdminUsersController.UpdateUserExportLimit), AppPolicies.UsersManageAccess)]
    [InlineData(nameof(AdminUsersController.UpdateUserSuperAdminNote), AppPolicies.UsersManageAccess)]
    public void AdminUsersEndpoint_UsesExpectedReadOrManagePolicy(string methodName, string expectedPolicy)
    {
        // Arrange

        // Act
        var policies = GetPolicies(typeof(AdminUsersController), methodName);

        // Assert
        Assert.Contains(expectedPolicy, policies);
    }

    [Theory]
    [InlineData(nameof(RoleSettingsController.GetRoleSettings), AppPolicies.RoleSettingsReadAccess)]
    [InlineData(nameof(RoleSettingsController.UpdateRoleSettings), AppPolicies.RoleSettingsManageAccess)]
    public void RoleSettingsEndpoint_UsesExpectedReadOrManagePolicy(
        string methodName,
        string expectedPolicy)
    {
        // Arrange

        // Act
        var policies = GetPolicies(typeof(RoleSettingsController), methodName);

        // Assert
        Assert.Contains(expectedPolicy, policies);
    }

    private static IReadOnlyList<string?> GetPolicies(Type controllerType, string methodName)
    {
        var controllerPolicies = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        var method = controllerType.GetMethod(methodName);
        Assert.NotNull(method);

        var methodPolicies = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        return controllerPolicies.Concat(methodPolicies).ToList();
    }
}
