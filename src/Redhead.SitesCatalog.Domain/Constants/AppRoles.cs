namespace Redhead.SitesCatalog.Domain.Constants;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Internal = "Internal";
    public const string Client = "Client";

    public static readonly string[] All = [SuperAdmin, Admin, Internal, Client];
}
