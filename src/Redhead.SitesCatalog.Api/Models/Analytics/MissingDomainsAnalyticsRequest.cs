using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Models.Analytics;

public sealed class MissingDomainsAnalyticsRequest
{
    public string? From { get; set; }
    public string? To { get; set; }
    public bool AllTime { get; set; }
    public string? Role { get; set; }
    public string? Domain { get; set; }
    public string? CatalogStatus { get; set; }
    public int Page { get; set; } = PaginationDefaults.DefaultPage;
    public int PageSize { get; set; } = PaginationDefaults.DefaultPageSize;
}
