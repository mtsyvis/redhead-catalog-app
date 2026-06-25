namespace Redhead.SitesCatalog.Domain.Constants;

public static class AppPolicies
{
    public const string SitesBrowseAccess = "SitesBrowseAccess";
    public const string SitesMultiSearchAccess = "SitesMultiSearchAccess";
    public const string SitesEditAccess = "SitesEditAccess";
    public const string SitesExportAccess = "SitesExportAccess";
    public const string TableViewsManageAccess = "TableViewsManageAccess";
    public const string ImportsRunAccess = "ImportsRunAccess";
    public const string UsersReadAccess = "UsersReadAccess";
    public const string UsersManageAccess = "UsersManageAccess";
    public const string RoleSettingsReadAccess = "RoleSettingsReadAccess";
    public const string RoleSettingsManageAccess = "RoleSettingsManageAccess";
    public const string AnalyticsReadAccess = "AnalyticsReadAccess";
    public const string AhrefsSyncManageAccess = "AhrefsSyncManageAccess";

    public static readonly IReadOnlyDictionary<string, string> PermissionPolicies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AppPermissions.SitesBrowse] = SitesBrowseAccess,
            [AppPermissions.SitesMultiSearch] = SitesMultiSearchAccess,
            [AppPermissions.SitesEdit] = SitesEditAccess,
            [AppPermissions.SitesExport] = SitesExportAccess,
            [AppPermissions.TableViewsManage] = TableViewsManageAccess,
            [AppPermissions.ImportsRun] = ImportsRunAccess,
            [AppPermissions.UsersRead] = UsersReadAccess,
            [AppPermissions.UsersManage] = UsersManageAccess,
            [AppPermissions.RoleSettingsRead] = RoleSettingsReadAccess,
            [AppPermissions.RoleSettingsManage] = RoleSettingsManageAccess,
            [AppPermissions.AnalyticsRead] = AnalyticsReadAccess,
            [AppPermissions.AhrefsSyncManage] = AhrefsSyncManageAccess
        };
}
