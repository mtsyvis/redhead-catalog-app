namespace Redhead.SitesCatalog.Domain.Constants;

/// <summary>
/// Default values for pagination
/// </summary>
public static class PaginationDefaults
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 1000;

    public static IReadOnlyList<int> AnalyticsPageSizes { get; } = Array.AsReadOnly([10, 25, 50, 100]);
}
