using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Services;

public class AdminUsersAuthorizationTests
{
    [Fact]
    public void CanCreateRole_SuperAdmin_CanCreateAnyRole()
    {
        var roles = new List<string> { AppRoles.SuperAdmin };
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.SuperAdmin));
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Admin));
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Internal));
        Assert.True(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Client));
    }

    [Fact]
    public void CanCreateRole_Admin_CannotCreateAnyRole()
    {
        var roles = new List<string> { AppRoles.Admin };
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.SuperAdmin));
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Admin));
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Internal));
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Client));
    }

    [Fact]
    public void CanCreateRole_NonSuperAdmin_CannotCreate()
    {
        var roles = new List<string> { AppRoles.Internal };
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.SuperAdmin));
        Assert.False(AdminUsersAuthorization.CanCreateRole(roles, AppRoles.Client));
    }

    [Fact]
    public void CanModifyUser_SuperAdmin_CanModifyAnyUser()
    {
        var current = new List<string> { AppRoles.SuperAdmin };
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.SuperAdmin }));
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.Admin }));
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.Internal }));
        Assert.True(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.Client }));
    }

    [Fact]
    public void CanModifyUser_Admin_CannotModifyAnyUser()
    {
        var current = new List<string> { AppRoles.Admin };
        Assert.False(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.SuperAdmin }));
        Assert.False(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.Admin }));
        Assert.False(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.Internal }));
        Assert.False(AdminUsersAuthorization.CanModifyUser(current, new List<string> { AppRoles.Client }));
    }
}
