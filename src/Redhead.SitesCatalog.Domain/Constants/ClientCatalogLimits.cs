namespace Redhead.SitesCatalog.Domain.Constants;

public static class ClientCatalogLimits
{
    public const int DefaultSelectionLimit = 100;
    public const int MaxSelectionLimit = 5000;
    public const string RateLimitPolicy = "ClientCatalog";

    public static int Resolve(int? value) => Math.Clamp(value ?? DefaultSelectionLimit, 1, MaxSelectionLimit);
}
