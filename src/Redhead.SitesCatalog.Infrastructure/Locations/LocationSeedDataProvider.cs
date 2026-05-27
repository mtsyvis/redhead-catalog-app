using System.Text.Json;

namespace Redhead.SitesCatalog.Infrastructure.Locations;

public static class LocationSeedDataProvider
{
    public const string LocationsAndGroupsFileName = "locations-and-groups.cleaned.json";
    public const string LocationAliasesFileName = "location-aliases.cleaned.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static (LocationsAndGroupsSeedData LocationsAndGroups, LocationAliasesSeedData Aliases) Load()
    {
        var seedDirectory = ResolveSeedDirectory();

        return (
            LoadJson<LocationsAndGroupsSeedData>(Path.Combine(seedDirectory, LocationsAndGroupsFileName)),
            LoadJson<LocationAliasesSeedData>(Path.Combine(seedDirectory, LocationAliasesFileName)));
    }

    public static (LocationsAndGroupsSeedData LocationsAndGroups, LocationAliasesSeedData Aliases) LoadFromDirectory(
        string seedDirectory)
    {
        return (
            LoadJson<LocationsAndGroupsSeedData>(Path.Combine(seedDirectory, LocationsAndGroupsFileName)),
            LoadJson<LocationAliasesSeedData>(Path.Combine(seedDirectory, LocationAliasesFileName)));
    }

    private static T LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Location seed file was not found: {path}", path);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
               ?? throw new InvalidOperationException($"Location seed file could not be parsed: {path}");
    }

    private static string ResolveSeedDirectory()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Locations", "SeedData");
        if (Directory.Exists(outputPath))
        {
            return outputPath;
        }

        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var sourcePath = Path.Combine(
                currentDirectory.FullName,
                "src",
                "Redhead.SitesCatalog.Infrastructure",
                "Locations",
                "SeedData");
            if (Directory.Exists(sourcePath))
            {
                return sourcePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Location seed data directory was not found.");
    }
}
