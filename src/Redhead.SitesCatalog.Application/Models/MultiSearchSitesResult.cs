namespace Redhead.SitesCatalog.Application.Models;

/// <summary>
/// Result of a multi-search request: found sites, not found domains, and duplicated inputs
/// </summary>
public class MultiSearchSitesResult
{
    public List<SiteDto> Found { get; set; } = [];
    public List<string> NotFound { get; set; } = [];
    public List<string> Duplicates { get; set; } = [];
}
