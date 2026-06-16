using System.Text.Json.Serialization;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models.Sites;

public sealed class FilterOptionsResponse
{
    public List<FilterOptionResponse> Niches { get; set; } = [];
    public LocationFilterOptionsResponse? Locations { get; set; }
    public List<TermFilterOptionResponse> Terms { get; set; } = [];
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
    public List<LocationFilterOptionResponse> Locations { get; set; } = [];
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

public sealed class TermFilterOptionResponse
{
    public string TermKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }
}
