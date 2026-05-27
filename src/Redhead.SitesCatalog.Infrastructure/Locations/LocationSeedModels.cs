using System.Text.Json.Serialization;

namespace Redhead.SitesCatalog.Infrastructure.Locations;

public sealed class LocationsAndGroupsSeedData
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("unknownLocationKey")]
    public string UnknownLocationKey { get; set; } = string.Empty;

    [JsonPropertyName("locations")]
    public List<LocationSeedRecord> Locations { get; set; } = [];

    [JsonPropertyName("groups")]
    public List<LocationGroupSeedRecord> Groups { get; set; } = [];
}

public sealed class LocationSeedRecord
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

public sealed class LocationGroupSeedRecord
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("locationKeys")]
    public List<string> LocationKeys { get; set; } = [];
}

public sealed class LocationAliasesSeedData
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("aliases")]
    public List<LocationAliasSeedRecord> Aliases { get; set; } = [];
}

public sealed class LocationAliasSeedRecord
{
    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("locationKey")]
    public string LocationKey { get; set; } = string.Empty;
}
