namespace Redhead.SitesCatalog.Domain.Constants;

public static class TableViewConstants
{
    public const string SitesTableKey = "sites";

    public const string SystemViewType = "system";
    public const string CustomViewType = "custom";
    public const string DefaultSystemViewKey = "default";

    public const int CustomViewNameMaxLength = 80;
    public const int CustomViewsPerUserTableLimit = 20;
    public const int SettingsJsonMaxLength = 16 * 1024;
    public const int SchemaVersion = 1;
}
