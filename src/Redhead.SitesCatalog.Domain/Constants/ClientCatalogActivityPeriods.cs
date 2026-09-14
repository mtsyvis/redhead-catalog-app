namespace Redhead.SitesCatalog.Domain.Constants;

public static class ClientCatalogActivityPeriods
{
    public static readonly IReadOnlyList<(string Label, TimeSpan Duration)> All = Array.AsReadOnly(new[]
    {
        ("Last hour", TimeSpan.FromHours(1)),
        ("Last 24 hours", TimeSpan.FromDays(1)),
        ("Last 7 days", TimeSpan.FromDays(7))
    });
}
