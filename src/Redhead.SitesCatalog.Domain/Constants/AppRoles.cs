namespace Redhead.SitesCatalog.Domain.Constants;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Linkbuilder = "Linkbuilder";
    public const string Internal = "Internal";
    public const string Client = "Client";
    public const string Lite = "Lite";

    public static readonly string[] All = [SuperAdmin, Admin, Editor, Linkbuilder, Internal, Client, Lite];
    public static readonly string[] NonSuperAdmin = [Admin, Editor, Linkbuilder, Internal, Client, Lite];
    public static readonly string[] ClientLike = [Client, Lite];
    public static readonly string[] ExportAlwaysDisabled = [Editor, Linkbuilder, Lite];
}
