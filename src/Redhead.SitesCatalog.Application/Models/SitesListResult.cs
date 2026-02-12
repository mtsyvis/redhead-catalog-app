namespace Redhead.SitesCatalog.Application.Models;

/// <summary>
/// Result of sites listing query
/// </summary>
public class SitesListResult
{
    public List<SiteDto> Items { get; set; } = [];
    public int Total { get; set; }
}
