namespace Redhead.SitesCatalog.Domain.Constants;

public static class AppPolicies
{
    // SuperAdmin only
    public const string SuperAdminOnly = "SuperAdminOnly";
    
    // SuperAdmin + Admin
    public const string AdminAccess = "AdminAccess";
    
    // Internal + Client (read-only roles)
    public const string ReadOnlyAccess = "ReadOnlyAccess";
}
