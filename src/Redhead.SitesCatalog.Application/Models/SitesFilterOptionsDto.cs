namespace Redhead.SitesCatalog.Application.Models;

public sealed class SitesFilterOptionsDto
{
    public List<FilterOptionDto> Niches { get; set; } = [];
    public LocationFilterOptionsDto Locations { get; set; } = new();
    public List<TermFilterOptionDto> Terms { get; set; } = [];
}
