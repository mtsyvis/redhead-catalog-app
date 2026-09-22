namespace Redhead.SitesCatalog.Domain.Constants;

public static class DisabledAccountResponseConstants
{
    public const string CatalogAutoDisabledCode = "AccountAutoDisabled";
    public const string CatalogAutoDisabledMessage =
        "Your account has been disabled due to suspicious catalog activity. Please contact support.";

    public const string ManuallyDisabledCode = "AccountDisabled";
    public const string ManuallyDisabledMessage =
        "Your account has been disabled. Please contact an administrator.";
}
