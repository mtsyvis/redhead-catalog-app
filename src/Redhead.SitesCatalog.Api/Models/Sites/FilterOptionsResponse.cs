using System.Text.Json.Serialization;

namespace Redhead.SitesCatalog.Api.Models.Sites;

public sealed class FilterOptionsResponse
{
    public List<FilterOptionResponse> Niches { get; set; } = [];
    public LocationFilterOptionsResponse? Locations { get; set; }
}

public sealed class FilterOptionResponse
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class LocationFilterOptionsResponse
{
    public List<LocationGroupFilterOptionResponse> Groups { get; set; } = [];
    public List<LocationFilterOptionResponse> Locations { get; set; } = [];
    public LocationSpecialFilterOptionsResponse Special { get; set; } = new();
}

public sealed class LocationGroupFilterOptionResponse
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty;
    public int LocationCount { get; set; }
}

public sealed class LocationFilterOptionResponse
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class LocationSpecialFilterOptionsResponse
{
    public LocationFilterOptionResponse Unknown { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LocationFilterOptionResponse? Other { get; set; }
}
