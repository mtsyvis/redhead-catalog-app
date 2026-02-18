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
    /// Optional filters (accepted; in 10A not applied to multi-search results)
    /// </summary>
    public SitesQueryRequest? Filters { get; set; }

    /// <summary>
    /// Optional sort (accepted; in 10A not applied to multi-search results)
    /// </summary>
    public SortDto? Sort { get; set; }
}

/// <summary>
/// Sort options (accepted in multi-search; applied in later commits)
/// </summary>
public class SortDto
{
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}
