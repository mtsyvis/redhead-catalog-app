using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Services;

/// <summary>
/// Authorization rules for admin user management.
/// SuperAdmin can create/modify any user; Admin can only view users.
/// </summary>
public static class AdminUsersAuthorization
{
    public static bool CanCreateRole(IList<string> currentUserRoles, string requestedRole)
    {
        _ = requestedRole;
        return currentUserRoles.Contains(AppRoles.SuperAdmin);
    }

    public static bool CanModifyUser(IList<string> currentUserRoles, IList<string> targetUserRoles)
    {
        _ = targetUserRoles;
        return currentUserRoles.Contains(AppRoles.SuperAdmin);
    }
}
