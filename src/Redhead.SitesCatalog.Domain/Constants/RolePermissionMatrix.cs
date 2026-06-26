namespace Redhead.SitesCatalog.Domain.Constants;

public static class RolePermissionMatrix
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> PermissionsByRole =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [AppRoles.SuperAdmin] = AppPermissions.All.ToHashSet(StringComparer.Ordinal),
            [AppRoles.Admin] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.SitesBrowse,
                AppPermissions.SitesMultiSearch,
                AppPermissions.SitesEdit,
                AppPermissions.SitesExport,
                AppPermissions.TableViewsManage,
                AppPermissions.ImportsRun,
                AppPermissions.UsersRead,
                AppPermissions.RoleSettingsRead,
                AppPermissions.AnalyticsRead,
                AppPermissions.AhrefsSyncManage
            },
            [AppRoles.Internal] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.SitesBrowse,
                AppPermissions.SitesMultiSearch,
                AppPermissions.SitesExport,
                AppPermissions.TableViewsManage
            },
            [AppRoles.Client] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.SitesBrowse,
                AppPermissions.SitesMultiSearch,
                AppPermissions.SitesExport,
                AppPermissions.TableViewsManage
            },
            [AppRoles.Lite] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.SitesMultiSearch,
                AppPermissions.TableViewsManage
            }
        };

    public static IReadOnlySet<string> GetPermissions(string role)
        => PermissionsByRole.TryGetValue(role, out var permissions)
            ? permissions
            : new HashSet<string>(StringComparer.Ordinal);

    public static bool HasPermission(string role, string permission)
        => PermissionsByRole.TryGetValue(role, out var permissions)
            && permissions.Contains(permission);

    public static string[] GetRolesForPermission(string permission)
        => AppRoles.All
            .Where(role => HasPermission(role, permission))
            .ToArray();
}
