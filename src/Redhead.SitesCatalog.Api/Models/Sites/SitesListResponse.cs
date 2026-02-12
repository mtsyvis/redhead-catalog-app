namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Paginated response for sites listing
/// </summary>
public class SitesListResponse
{
    public List<SiteResponse> Items { get; set; } = [];
    public int Total { get; set; }
}
