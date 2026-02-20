using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Services;

public class AdminUsersAuthorizationTests
{
    [Fact]
    public void CanCreateRole_SuperAdmin_CanCreateSuperAdmin()
    {
        var roles = new List<string> { AppRoles.SuperAdmin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.SuperAdmin));
    }

    [Fact]
    public void CanCreateRole_SuperAdmin_CanCreateAdmin()
    {
        var roles = new List<string> { AppRoles.SuperAdmin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Admin));
    }

    [Fact]
    public void CanCreateRole_SuperAdmin_CanCreateInternal()
    {
        var roles = new List<string> { AppRoles.SuperAdmin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Internal));
    }

    [Fact]
    public void CanCreateRole_SuperAdmin_CanCreateClient()
    {
        var roles = new List<string> { AppRoles.SuperAdmin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Client));
    }

    [Fact]
    public void CanCreateRole_Admin_CannotCreateSuperAdmin()
    {
        var roles = new List<string> { AppRoles.Admin };
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.SuperAdmin));
    }

    [Fact]
    public void CanCreateRole_Admin_CanCreateAdmin()
    {
        var roles = new List<string> { AppRoles.Admin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Admin));
    }

    [Fact]
    public void CanCreateRole_Admin_CanCreateInternal()
    {
        var roles = new List<string> { AppRoles.Admin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Internal));
    }

    [Fact]
    public void CanCreateRole_Admin_CanCreateClient()
    {
        var roles = new List<string> { AppRoles.Admin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Client));
    }

    [Fact]
    public void CanCreateRole_Internal_CannotCreateSuperAdmin()
    {
        var roles = new List<string> { AppRoles.Internal };
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.SuperAdmin));
    }

    [Fact]
    public void CanCreateRole_EmptyRoles_CannotCreateSuperAdmin()
    {
        var roles = new List<string>();
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.SuperAdmin));
    }

    [Fact]
    public void CanModifyUser_SuperAdmin_CanModifySuperAdmin()
    {
        var current = new List<string> { AppRoles.SuperAdmin };
        var target = new List<string> { AppRoles.SuperAdmin };
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, target));
    }

    [Fact]
    public void CanModifyUser_SuperAdmin_CanModifyAdmin()
    {
        var current = new List<string> { AppRoles.SuperAdmin };
        var target = new List<string> { AppRoles.Admin };
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, target));
    }

    [Fact]
    public void CanModifyUser_Admin_CannotModifySuperAdmin()
    {
        var current = new List<string> { AppRoles.Admin };
        var target = new List<string> { AppRoles.SuperAdmin };
        Assert.False(AdminUsersAuthorization.CanModifyUser(current, target));
    }

    [Fact]
    public void CanModifyUser_Admin_CannotModifyAdmin()
    {
        var current = new List<string> { AppRoles.Admin };
        var target = new List<string> { AppRoles.Admin };
        Assert.False(AdminUsersAuthorization.CanModifyUser(current, target));
    }

    [Fact]
    public void CanModifyUser_Admin_CanModifyInternal()
    {
        var current = new List<string> { AppRoles.Admin };
        var target = new List<string> { AppRoles.Internal };
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, target));
    }

    [Fact]
    public void CanModifyUser_Admin_CanModifyClient()
    {
        var current = new List<string> { AppRoles.Admin };
        var target = new List<string> { AppRoles.Client };
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, target));
    }
}
