using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Domain;

public sealed class RolePermissionMatrixTests
{
    [Fact]
    public void GetPermissions_SuperAdminRole_ReturnsEveryKnownPermission()
    {
        // Arrange

        // Act
        var permissions = RolePermissionMatrix.GetPermissions(AppRoles.SuperAdmin);

        // Assert
        Assert.Equal(
            AppPermissions.All.OrderBy(permission => permission),
            permissions.OrderBy(permission => permission));
    }

    [Fact]
    public void GetPermissions_LiteRole_HasOnlyMultiSearchAndTableViews()
    {
        // Arrange

        // Act
        var permissions = RolePermissionMatrix.GetPermissions(AppRoles.Lite);

        // Assert
        Assert.Equal(
            [AppPermissions.SitesMultiSearch, AppPermissions.TableViewsManage],
            permissions.OrderBy(permission => permission));
    }

    [Fact]
    public void GetPermissions_AdminRole_CanReadAnalyticsWithoutManagePermissions()
    {
        // Arrange

        // Act
        var permissions = RolePermissionMatrix.GetPermissions(AppRoles.Admin);

        // Assert
        Assert.Contains(AppPermissions.AnalyticsRead, permissions);
        Assert.Contains(AppPermissions.UsersRead, permissions);
        Assert.Contains(AppPermissions.RoleSettingsRead, permissions);
        Assert.DoesNotContain(AppPermissions.UsersManage, permissions);
        Assert.DoesNotContain(AppPermissions.RoleSettingsManage, permissions);
    }

    [Fact]
    public void GetPermissions_ForEveryActiveRole_OnlyUsesKnownPermissions()
    {
        // Arrange

        // Act
        var unknownPermissionsByRole = AppRoles.All
            .Select(role => new
            {
                Role = role,
                UnknownPermissions = RolePermissionMatrix
                    .GetPermissions(role)
                    .Except(AppPermissions.All)
                    .ToArray()
            })
            .Where(rolePermissions => rolePermissions.UnknownPermissions.Length > 0)
            .ToArray();

        // Assert
        Assert.Empty(unknownPermissionsByRole);
    }

    [Fact]
    public void GetRolesForPermission_AnalyticsRead_ReturnsSuperAdminAndAdmin()
    {
        // Arrange

        // Act
        var roles = RolePermissionMatrix.GetRolesForPermission(AppPermissions.AnalyticsRead);

        // Assert
        Assert.Equal([AppRoles.SuperAdmin, AppRoles.Admin], roles);
    }

    [Fact]
    public void GetPermissions_EditorRole_ReturnsNoPermissionsUntilRoleIsActive()
    {
        // Arrange
        const string editorRole = "Editor";

        // Act
        var permissions = RolePermissionMatrix.GetPermissions(editorRole);

        // Assert
        Assert.DoesNotContain(editorRole, AppRoles.All);
        Assert.Empty(permissions);
    }
}
