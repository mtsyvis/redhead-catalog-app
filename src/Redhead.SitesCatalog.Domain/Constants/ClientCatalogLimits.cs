namespace Redhead.SitesCatalog.Domain.Constants;

public static class ClientCatalogLimits
{
    public const int DefaultSelectionLimit = 100;
    public const int BurstProtectionMaxSelectionLimit = 100;
    public const int MaxSelectionLimit = 5000;
    public const string RateLimitPolicy = "ClientCatalog";
    public const int DefaultUniqueSitesPerFiveMinutes = 2000;
    public static readonly TimeSpan BurstWindow = TimeSpan.FromMinutes(5);

    public static int Resolve(int? value) => Math.Clamp(value ?? DefaultSelectionLimit, 1, MaxSelectionLimit);
    public static bool IsBurstExempt(int selectionLimit) => selectionLimit > BurstProtectionMaxSelectionLimit;
}
