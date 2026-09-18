using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Models.Analytics;

public sealed class MissingDomainsAnalyticsQuery
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Role { get; init; }
    public string? Domain { get; init; }
    public bool? IsInCatalog { get; init; }
    public int Page { get; init; } = PaginationDefaults.DefaultPage;
    public int PageSize { get; init; } = PaginationDefaults.DefaultPageSize;
}

public sealed record MissingDomainsAnalyticsDto(
    int UniqueDomains, long Searches, int UniqueUsers,
    IReadOnlyList<MissingDomainAnalyticsRow> Items, int Page, int PageSize);

public sealed record MissingDomainAnalyticsRow(
    string Domain, long Searches, int UniqueUsers,
    DateTime FirstSearchedAtUtc, DateTime LastSearchedAtUtc, bool IsInCatalog);
