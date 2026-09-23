namespace Redhead.SitesCatalog.Domain.Constants;

public static class ClientCatalogLimits
{
    public const int DefaultSelectionLimit = 100;
    public const int MaxSelectionLimit = 100;
    public const string RateLimitPolicy = "ClientCatalog";
    public const int DefaultUniqueSitesPerFiveMinutes = 2000;
    public const int DefaultAutoBanUniqueSitesPer24Hours = 20_000;
    public static readonly TimeSpan BurstWindow = TimeSpan.FromMinutes(5);

    public static int Resolve(int? value) => Math.Clamp(value ?? DefaultSelectionLimit, 1, MaxSelectionLimit);
}
