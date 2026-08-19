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
    public void GetPermissions_LinkbuilderRole_HasSitesAndWebmasterOfferReadOnlyPermissions()
    {
        // Arrange

        // Act
        var permissions = RolePermissionMatrix.GetPermissions(AppRoles.Linkbuilder);

        // Assert
        Assert.Equal(
            [
                AppPermissions.SitesBrowse,
                AppPermissions.SitesMultiSearch,
                AppPermissions.TableViewsManage,
                AppPermissions.WebmasterOffersRead
            ],
            permissions.OrderBy(permission => permission));
        Assert.DoesNotContain(AppPermissions.WebmasterOffersImport, permissions);
        Assert.DoesNotContain(AppPermissions.SitesExport, permissions);
    }

    [Fact]
    public void GetRolesForPermission_WebmasterOffersRead_ReturnsAdminEditorSuperAdminAndLinkbuilder()
    {
        // Arrange

        // Act
        var roles = RolePermissionMatrix.GetRolesForPermission(AppPermissions.WebmasterOffersRead);

        // Assert
        Assert.Equal([AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Editor, AppRoles.Linkbuilder], roles);
    }

    [Fact]
    public void GetRolesForPermission_WebmasterOffersImport_ReturnsOnlySuperAdminAndAdmin()
    {
        // Arrange

        // Act
        var roles = RolePermissionMatrix.GetRolesForPermission(AppPermissions.WebmasterOffersImport);

        // Assert
        Assert.Equal([AppRoles.SuperAdmin, AppRoles.Admin], roles);
    }

    [Fact]
    public void GetRolesForPermission_WebmasterOffersManage_ReturnsSuperAdminAdminAndEditor()
    {
        // Arrange

        // Act
        var roles = RolePermissionMatrix.GetRolesForPermission(AppPermissions.WebmasterOffersManage);

        // Assert
        Assert.Equal([AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Editor], roles);
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
    public void GetPermissions_EditorRole_HasCatalogAndWebmasterOfferEditPermissions()
    {
        // Arrange

        // Act
        var permissions = RolePermissionMatrix.GetPermissions(AppRoles.Editor);

        // Assert
        Assert.Contains(AppRoles.Editor, AppRoles.All);
        Assert.Equal(
            [
                AppPermissions.SitesBrowse,
                AppPermissions.SitesEdit,
                AppPermissions.SitesMultiSearch,
                AppPermissions.TableViewsManage,
                AppPermissions.WebmasterOffersManage,
                AppPermissions.WebmasterOffersRead
            ],
            permissions.OrderBy(permission => permission));
        Assert.DoesNotContain(AppPermissions.WebmasterOffersImport, permissions);
    }
}
