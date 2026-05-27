using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Infrastructure.Locations;

public sealed class LocationNormalizer : ILocationNormalizer
{
    private readonly IReadOnlyDictionary<string, string> _locationKeysByLookupValue;

    public LocationNormalizer()
        : this(LocationSeedDataProvider.Load())
    {
    }

    public LocationNormalizer(
        (LocationsAndGroupsSeedData LocationsAndGroups, LocationAliasesSeedData Aliases) seedData)
    {
        LocationConfigValidator.Validate(seedData.LocationsAndGroups, seedData.Aliases);
        _locationKeysByLookupValue = BuildLookup(seedData.LocationsAndGroups, seedData.Aliases);
    }

    public LocationNormalizationResult Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new LocationNormalizationResult(
                LocationNormalizationStatus.Unknown,
                LocationConstants.UnknownLocationKey,
                null);
        }

        var lookupValue = LocationTextNormalizer.NormalizeLookupValue(value);
        if (_locationKeysByLookupValue.TryGetValue(lookupValue, out var locationKey))
        {
            return new LocationNormalizationResult(
                LocationNormalizationStatus.Known,
                locationKey,
                value);
        }

        return new LocationNormalizationResult(LocationNormalizationStatus.Unmapped, null, value);
    }

    private static IReadOnlyDictionary<string, string> BuildLookup(
        LocationsAndGroupsSeedData locationsAndGroups,
        LocationAliasesSeedData aliases)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var location in locationsAndGroups.Locations)
        {
            AddLookup(lookup, location.Key, location.Key);
            AddLookup(lookup, location.DisplayName, location.Key);
        }

        foreach (var alias in aliases.Aliases)
        {
            AddLookup(lookup, alias.Alias, alias.LocationKey);
        }

        return lookup;
    }

    private static void AddLookup(Dictionary<string, string> lookup, string value, string locationKey)
    {
        var normalized = LocationTextNormalizer.NormalizeLookupValue(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (lookup.TryGetValue(normalized, out var existingLocationKey)
            && !string.Equals(existingLocationKey, locationKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Location lookup value '{value}' maps to both '{existingLocationKey}' and '{locationKey}'.");
        }

        lookup[normalized] = locationKey;
    }
}
