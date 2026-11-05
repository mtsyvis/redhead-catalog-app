namespace Redhead.SitesCatalog.Domain.Constants;

/// <summary>
/// Default values for pagination
/// </summary>
public static class PaginationDefaults
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxTablePageSize = 100;
    // Catalog API requests have a separate upper limit; UI choices use PageSizes.
    public const int MaxPageSize = 1000;

    public static IReadOnlyList<int> PageSizes { get; } = Array.AsReadOnly([10, DefaultPageSize, 50, MaxTablePageSize]);
}
