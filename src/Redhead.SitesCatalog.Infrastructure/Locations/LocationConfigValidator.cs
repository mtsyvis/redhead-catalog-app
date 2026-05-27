using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Infrastructure.Locations;

public static class LocationConfigValidator
{
    public static void Validate(LocationsAndGroupsSeedData locationsAndGroups, LocationAliasesSeedData aliases)
    {
        ArgumentNullException.ThrowIfNull(locationsAndGroups);
        ArgumentNullException.ThrowIfNull(aliases);

        var locationKeys = locationsAndGroups.Locations
            .Select(location => location.Key)
            .ToArray();
        var locationKeySet = new HashSet<string>(locationKeys, StringComparer.OrdinalIgnoreCase);

        ThrowIfDuplicates(locationKeys, "canonical location key");
        ThrowIfDuplicates(locationsAndGroups.Groups.Select(group => group.Key), "location group key");

        var unknown = locationsAndGroups.Locations.SingleOrDefault(
            location => string.Equals(location.Key, LocationConstants.UnknownLocationKey, StringComparison.Ordinal));
        if (unknown is null)
        {
            throw new InvalidOperationException("Location config must include UNKNOWN.");
        }

        if (!string.Equals(unknown.DisplayName, "Unknown", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("UNKNOWN location must have DisplayName 'Unknown'.");
        }

        if (locationsAndGroups.Locations.Any(
                location => string.Equals(location.DisplayName, "Other", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(location.Key, "OTHER", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Other must not be configured as a canonical location.");
        }

        foreach (var group in locationsAndGroups.Groups)
        {
            foreach (var locationKey in group.LocationKeys)
            {
                if (!locationKeySet.Contains(locationKey))
                {
                    throw new InvalidOperationException(
                        $"Location group '{group.Key}' references unknown location key '{locationKey}'.");
                }
            }
        }

        foreach (var alias in aliases.Aliases)
        {
            if (!locationKeySet.Contains(alias.LocationKey))
            {
                throw new InvalidOperationException(
                    $"Location alias '{alias.Alias}' references unknown location key '{alias.LocationKey}'.");
            }
        }
    }

    private static void ThrowIfDuplicates(IEnumerable<string> values, string label)
    {
        var duplicate = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate {label}: {duplicate.Key}");
        }
    }
}
