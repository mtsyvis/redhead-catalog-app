namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Request body for POST /api/sites/multi-search
/// </summary>
public class MultiSearchRequest
{
    /// <summary>
    /// Domains or URLs separated by whitespace (spaces, newlines, tabs). Max 500 inputs.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Optional filters. Stop-list values are rejected; other filters are not applied by this endpoint.
    /// </summary>
    public SitesQueryRequest? Filters { get; set; }

    /// <summary>
    /// Not supported in multi-search mode. Requests with values are rejected.
    /// </summary>
    public List<string>? StopListDomains { get; set; }

    /// <summary>
    /// Optional sort metadata. Sorting is applied by the grid/export context, not by this endpoint.
    /// </summary>
    public SortDto? Sort { get; set; }
}

/// <summary>
/// Sort options accepted with multi-search request metadata.
/// </summary>
public class SortDto
{
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}
