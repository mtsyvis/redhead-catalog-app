using System.Globalization;
using System.Text.Json;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportActivitySnapshotSummaryFormatter
{
    private const int SummaryItemLimit = 3;
    private const string OtherLocationName = "Other";

    private static readonly ServiceFilterDefinition[] ServiceFilters =
    [
        new("priceCasinoAvailability", "Casino"),
        new("priceCryptoAvailability", "Crypto"),
        new("priceLinkInsertAvailability", "Link insert"),
        new("priceLinkInsertCasinoAvailability", "Link insert casino"),
        new("priceDatingAvailability", "Dating")
    ];

    public static string FormatFilters(
        string? filtersSnapshotJson,
        string? searchSnapshotJson,
        BusinessDemandLocationLookups locationLookups)
    {
        var hasParsedFilters = ExportFiltersSnapshotParser.TryParse(filtersSnapshotJson, out var snapshot);
        var searchLabels = ParseSearchLabels(searchSnapshotJson);
        if (!hasParsedFilters && searchLabels == null)
        {
            return "Unavailable";
        }

        var labels = new List<string>();
        if (hasParsedFilters)
        {
            labels.AddRange(BuildFilterLabels(snapshot, locationLookups));
        }

        if (searchLabels is { Count: > 0 })
        {
            labels.AddRange(searchLabels);
        }

        var totalFilterCount = hasParsedFilters ? snapshot.Filters.Count : 0;
        var totalSummaryItems = totalFilterCount + (searchLabels?.Count ?? 0);
        if (totalSummaryItems == 0)
        {
            return "No filters";
        }

        if (labels.Count == 0)
        {
            return FormatFilterCount(totalSummaryItems);
        }

        var visibleLabels = labels.Take(SummaryItemLimit).ToArray();
        if (labels.Count <= SummaryItemLimit && totalSummaryItems <= SummaryItemLimit)
        {
            return string.Join(", ", visibleLabels);
        }

        var hiddenCount = Math.Max(totalSummaryItems - visibleLabels.Length, 0);
        return hiddenCount > 0
            ? $"{string.Join(", ", visibleLabels)}, +{hiddenCount} more"
            : string.Join(", ", visibleLabels);
    }

    public static string FormatSort(string? sortSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(sortSnapshotJson))
        {
            return "Unavailable";
        }

        try
        {
            var document = JsonSerializer.Deserialize<SortsSnapshotDocument>(
                sortSnapshotJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (document?.Sorts is null)
            {
                return "Unavailable";
            }

            if (document.Sorts.Count == 0)
            {
                return "—";
            }

            var firstSort = document.Sorts
                .OrderBy(sort => sort.Priority)
                .FirstOrDefault();
            if (firstSort == null || string.IsNullOrWhiteSpace(firstSort.Field))
            {
                return "Unavailable";
            }

            var label = $"{FormatSortField(firstSort.Field)} {FormatSortDirection(firstSort.Direction)}";
            return document.Sorts.Count > 1
                ? $"{label} +{document.Sorts.Count - 1} more"
                : label;
        }
        catch (JsonException)
        {
            return "Unavailable";
        }
    }

    private static IReadOnlyList<string> BuildFilterLabels(
        FiltersSnapshot snapshot,
        BusinessDemandLocationLookups locationLookups)
    {
        var labels = new List<string>();

        AddLocationLabel(labels, snapshot, locationLookups);
        AddRangeLabels(labels, snapshot, "dr", QualityRangeFormatter.FormatDrRange);
        AddRangeLabels(labels, snapshot, "traffic", QualityRangeFormatter.FormatTrafficRange);
        AddRangeLabels(labels, snapshot, "priceUsd", QualityRangeFormatter.FormatPriceRange);
        AddValuesLabel(labels, snapshot.GetStringValues("niche"), singular: "Niche", plural: "niches");
        AddValuesLabel(labels, snapshot.GetStringValues("categories"), singular: "Category", plural: "categories");
        AddValuesLabel(labels, snapshot.GetStringValues("language").Select(value => value.ToUpperInvariant()).ToArray(), singular: "Language", plural: "languages");
        AddValuesLabel(labels, snapshot.GetStringValues("excludedNiche"), singular: "Excluded niche", plural: "excluded niches");
        AddValuesLabel(labels, snapshot.GetStringValues("excludedCategories"), singular: "Excluded category", plural: "excluded categories");
        AddServiceLabels(labels, snapshot);

        if (snapshot.GetBooleanValue("stopList") == true)
        {
            labels.Add("Stop list");
        }

        return labels;
    }

    private static void AddLocationLabel(
        List<string> labels,
        FiltersSnapshot snapshot,
        BusinessDemandLocationLookups locationLookups)
    {
        var selectedLocationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var locationKey in snapshot.GetStringValues("locationKey"))
        {
            selectedLocationNames.Add(ResolveLocationName(locationKey, locationLookups));
        }

        foreach (var location in snapshot.GetStringValues("location"))
        {
            selectedLocationNames.Add(location);
        }

        if (snapshot.GetBooleanValue("locationUnknown") == true)
        {
            selectedLocationNames.Add("Unknown");
        }

        if (snapshot.GetBooleanValue("locationOther") == true)
        {
            selectedLocationNames.Add(OtherLocationName);
        }

        foreach (var groupKey in snapshot.GetStringValues("locationGroup"))
        {
            selectedLocationNames.Add($"{groupKey} group");
        }

        if (selectedLocationNames.Count == 0)
        {
            return;
        }

        AddValuesLabel(labels, selectedLocationNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(), "Location", "locations");
    }

    private static string ResolveLocationName(
        string locationKey,
        BusinessDemandLocationLookups locationLookups)
    {
        if (locationLookups.LocationNamesByKey.TryGetValue(locationKey, out var displayName))
        {
            return displayName;
        }

        return string.Equals(locationKey, LocationConstants.UnknownLocationKey, StringComparison.OrdinalIgnoreCase)
            ? "Unknown"
            : locationKey;
    }

    private static void AddRangeLabels(
        List<string> labels,
        FiltersSnapshot snapshot,
        string field,
        Func<RangeValue, string?> formatter)
    {
        foreach (var rangeLabel in snapshot.GetRanges(field)
                     .Select(formatter)
                     .Where(label => !string.IsNullOrWhiteSpace(label)))
        {
            labels.Add(rangeLabel!);
        }
    }

    private static void AddValuesLabel(
        List<string> labels,
        IReadOnlyCollection<string> values,
        string singular,
        string plural)
    {
        var activeValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (activeValues.Length == 0)
        {
            return;
        }

        labels.Add(activeValues.Length == 1
            ? $"{singular} {activeValues[0]}"
            : $"{activeValues.Length.ToString("N0", CultureInfo.GetCultureInfo("en-US"))} {plural}");
    }

    private static void AddServiceLabels(List<string> labels, FiltersSnapshot snapshot)
    {
        foreach (var service in ServiceFilters)
        {
            var selected = snapshot.GetStringValues(service.Field)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (selected.Count == 0)
            {
                continue;
            }

            if (selected.Contains("available") || selected.Contains("availableWithUnknownPrice"))
            {
                labels.Add($"{service.DisplayName} available");
            }

            if (selected.Contains("notAvailable"))
            {
                labels.Add($"{service.DisplayName} no");
            }
        }
    }

    private static IReadOnlyList<string>? ParseSearchLabels(string? searchSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(searchSnapshotJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(searchSnapshotJson);
            var root = document.RootElement;
            if (!TryGetProperty(root, "mode", out var modeElement) ||
                modeElement.ValueKind != JsonValueKind.String)
            {
                return [];
            }

            var mode = modeElement.GetString();
            if (string.Equals(mode, "multiSearch", StringComparison.OrdinalIgnoreCase))
            {
                return [FormatMultiSearchLabel(root)];
            }

            if (string.Equals(mode, "catalogSearch", StringComparison.OrdinalIgnoreCase) &&
                TryGetProperty(root, "query", out var queryElement) &&
                queryElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(queryElement.GetString()))
            {
                return [$"Search {queryElement.GetString()!.Trim()}"];
            }

            return [];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatMultiSearchLabel(JsonElement root)
    {
        if (!TryGetProperty(root, "uniqueInputCount", out var uniqueInputCountElement) ||
            uniqueInputCountElement.ValueKind != JsonValueKind.Number ||
            !uniqueInputCountElement.TryGetInt32(out var uniqueInputCount))
        {
            return "Multi-search";
        }

        return $"Multi-search {uniqueInputCount.ToString("N0", CultureInfo.GetCultureInfo("en-US"))} domains";
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        var pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        return element.TryGetProperty(pascalName, out value);
    }

    private static string FormatFilterCount(int count)
        => count == 1
            ? "1 filter"
            : $"{count.ToString("N0", CultureInfo.GetCultureInfo("en-US"))} filters";

    private static string FormatSortField(string field)
        => field.Trim().ToLowerInvariant() switch
        {
            "domain" => "Domain",
            "dr" => "DR",
            "traffic" => "Traffic",
            "location" => "Location",
            "priceusd" => "Price",
            "pricecasino" => "Casino price",
            "pricecrypto" => "Crypto price",
            "pricelinkinsert" => "Link insert price",
            "pricelinkinsertcasino" => "Link insert casino price",
            "pricedating" => "Dating price",
            "numberdflinks" => "DF links",
            "term" => "Term",
            "createdat" => "Created",
            "updatedat" => "Updated",
            "lastpublisheddate" => "Last published",
            _ => field.Trim()
        };

    private static string FormatSortDirection(string? direction)
        => string.Equals(direction, SortingDefaults.Descending, StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";

    private sealed record ServiceFilterDefinition(string Field, string DisplayName);

    private sealed class SortsSnapshotDocument
    {
        public int SchemaVersion { get; set; }
        public IReadOnlyList<SortSnapshotItemDocument>? Sorts { get; set; }
    }

    private sealed class SortSnapshotItemDocument
    {
        public string? Field { get; set; }
        public string? Direction { get; set; }
        public int Priority { get; set; }
    }
}
