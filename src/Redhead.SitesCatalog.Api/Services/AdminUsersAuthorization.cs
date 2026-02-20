using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Services;

/// <summary>
/// Authorization rules for admin user management. SuperAdmin can create/modify any user;
/// Admin can create Admin/Internal/Client and modify only non-Admin, non-SuperAdmin users.
/// </summary>
public static class AdminUsersAuthorization
{
    public static bool CanCreateRole(IList<string> currentUserRoles, string requestedRole)
    {
        var isSuperAdmin = currentUserRoles.Contains(AppRoles.SuperAdmin);
        if (isSuperAdmin)
        {
            return true;
        }
        return requestedRole != AppRoles.SuperAdmin;
    }

    public static bool CanModifyUser(IList<string> currentUserRoles, IList<string> targetUserRoles)
    {
        var isSuperAdmin = currentUserRoles.Contains(AppRoles.SuperAdmin);
        if (isSuperAdmin)
        {
            return true;
        }
        return !targetUserRoles.Contains(AppRoles.SuperAdmin) && !targetUserRoles.Contains(AppRoles.Admin);
    }
}
