namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Response for POST /api/sites/multi-search
/// </summary>
public class MultiSearchResponse
{
    public List<SiteResponse> Found { get; set; } = [];
    public List<string> NotFound { get; set; } = [];
    public List<string> Duplicates { get; set; } = [];
}
