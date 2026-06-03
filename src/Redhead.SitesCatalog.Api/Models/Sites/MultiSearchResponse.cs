namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Response for POST /api/sites/multi-search
/// </summary>
public class MultiSearchResponse
{
    public List<MultiSearchResultResponse> Results { get; set; } = [];
    public List<SiteResponse> Found { get; set; } = [];
    public List<string> NotFound { get; set; } = [];
    public List<string> Duplicates { get; set; } = [];
}

/// <summary>
/// Ordered multi-search result item matching the normalized input order.
/// </summary>
public class MultiSearchResultResponse
{
    public string Domain { get; set; } = string.Empty;
    public bool Found { get; set; }
    public SiteResponse? Site { get; set; }
}
