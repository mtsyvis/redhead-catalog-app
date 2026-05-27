namespace Redhead.SitesCatalog.Application.Models;

public sealed class LocationFilterOptionsDto
{
    public List<LocationGroupFilterOptionDto> Groups { get; set; } = [];
    public List<LocationFilterOptionDto> Locations { get; set; } = [];
    public LocationSpecialFilterOptionsDto Special { get; set; } = new();
}

public sealed class LocationGroupFilterOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty;
    public int LocationCount { get; set; }
}

public sealed class LocationFilterOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class LocationSpecialFilterOptionsDto
{
    public LocationFilterOptionDto Unknown { get; set; } = new();
    public LocationFilterOptionDto? Other { get; set; }
}
