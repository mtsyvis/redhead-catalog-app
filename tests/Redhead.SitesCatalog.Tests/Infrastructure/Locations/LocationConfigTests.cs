using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Locations;

public class LocationConfigTests
{
    private readonly LocationsAndGroupsSeedData _locationsAndGroups;
    private readonly LocationAliasesSeedData _aliases;

    public LocationConfigTests()
    {
        (_locationsAndGroups, _aliases) = LocationSeedDataProvider.Load();
    }

    [Fact]
    public void Validate_SuppliedConfig_Passes()
    {
        // Arrange

        // Act
        var exception = Record.Exception(() => LocationConfigValidator.Validate(_locationsAndGroups, _aliases));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Groups_AllLocationKeysExist()
    {
        // Arrange
        var canonicalKeys = _locationsAndGroups.Locations
            .Select(location => location.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var missingKeys = _locationsAndGroups.Groups
            .SelectMany(group => group.LocationKeys)
            .Where(locationKey => !canonicalKeys.Contains(locationKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Assert
        Assert.Empty(missingKeys);
    }

    [Fact]
    public void Aliases_AllLocationKeysExist()
    {
        // Arrange
        var canonicalKeys = _locationsAndGroups.Locations
            .Select(location => location.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var missingKeys = _aliases.Aliases
            .Where(alias => !canonicalKeys.Contains(alias.LocationKey))
            .Select(alias => alias.LocationKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Assert
        Assert.Empty(missingKeys);
    }

    [Fact]
    public void UnknownLocation_ExistsWithExpectedDisplayName()
    {
        // Arrange

        // Act
        var unknown = Assert.Single(
            _locationsAndGroups.Locations,
            location => location.Key == LocationConstants.UnknownLocationKey);

        // Assert
        Assert.Equal("Unknown", unknown.DisplayName);
    }

    [Fact]
    public void Other_IsNotCanonicalLocation()
    {
        // Arrange

        // Act
        var otherLocations = _locationsAndGroups.Locations
            .Where(location => string.Equals(location.Key, "OTHER", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(location.DisplayName, "Other", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        Assert.Empty(otherLocations);
    }

    [Fact]
    public void Groups_IncludeRegionAndBusinessGroups()
    {
        // Arrange
        var groupsByKey = _locationsAndGroups.Groups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);

        // Act
        var regionGroups = _locationsAndGroups.Groups.Where(group => group.Kind == "Region").ToArray();
        var businessGroups = _locationsAndGroups.Groups.Where(group => group.Kind == "Business").ToArray();

        // Assert
        Assert.NotEmpty(regionGroups);
        Assert.NotEmpty(businessGroups);
        Assert.Equal("Business", groupsByKey["first-world"].Kind);
        Assert.Equal("Business", groupsByKey["mena"].Kind);
        Assert.Equal("Business", groupsByKey["latam"].Kind);
        Assert.Equal("Business", groupsByKey["arab-countries"].Kind);
    }
}
