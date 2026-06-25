namespace Redhead.SitesCatalog.Domain.Constants;

public static class AppPermissions
{
    public const string SitesBrowse = "SitesBrowse";
    public const string SitesMultiSearch = "SitesMultiSearch";
    public const string SitesEdit = "SitesEdit";
    public const string SitesExport = "SitesExport";
    public const string TableViewsManage = "TableViewsManage";
    public const string ImportsRun = "ImportsRun";
    public const string UsersRead = "UsersRead";
    public const string UsersManage = "UsersManage";
    public const string RoleSettingsRead = "RoleSettingsRead";
    public const string RoleSettingsManage = "RoleSettingsManage";
    public const string AnalyticsRead = "AnalyticsRead";
    public const string AhrefsSyncManage = "AhrefsSyncManage";

    public static readonly string[] All =
    [
        SitesBrowse,
        SitesMultiSearch,
        SitesEdit,
        SitesExport,
        TableViewsManage,
        ImportsRun,
        UsersRead,
        UsersManage,
        RoleSettingsRead,
        RoleSettingsManage,
        AnalyticsRead,
        AhrefsSyncManage
    ];
}
