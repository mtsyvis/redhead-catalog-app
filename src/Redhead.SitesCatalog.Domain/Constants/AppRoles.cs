namespace Redhead.SitesCatalog.Domain.Constants;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Internal = "Internal";
    public const string Client = "Client";
    public const string Lite = "Lite";

    public static readonly string[] All = [SuperAdmin, Admin, Internal, Client, Lite];
    public static readonly string[] NonSuperAdmin = [Admin, Internal, Client, Lite];
    public static readonly string[] ClientLike = [Client, Lite];
}
