using System.Globalization;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportActivitySnapshotSummaryFormatter
{
    private const int SummaryItemLimit = 3;
    private const string OtherLocationName = "Other";

    public static string FormatFilters(
        string? filtersSnapshotJson,
        string? searchSnapshotJson,
        BusinessDemandLocationLookups locationLookups)
    {
        var hasParsedFilters = ExportFiltersSnapshotParser.TryParse(filtersSnapshotJson, out var snapshot);
        var searchLabels = BuildSearchLabels(ExportAnalyticsSearchSnapshotParser.Parse(searchSnapshotJson));
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
        if (!ExportSortSnapshotParser.TryParse(sortSnapshotJson, out var snapshot))
        {
            return "Unavailable";
        }

        if (snapshot.Sorts.Count == 0)
        {
            return "—";
        }

        var firstSort = snapshot.Sorts[0];
        var label = $"{FormatSortField(firstSort.Field)} {FormatSortDirection(firstSort.Direction)}";
        return snapshot.Sorts.Count > 1
            ? $"{label} +{snapshot.Sorts.Count - 1} more"
            : label;
    }

    private static IReadOnlyList<string> BuildFilterLabels(
        FiltersSnapshot snapshot,
        BusinessDemandLocationLookups locationLookups)
    {
        var labels = new List<string>();

        AddLocationLabel(labels, snapshot, locationLookups);
        AddRangeLabels(labels, snapshot, ExportAnalyticsSnapshotSchema.Filters.Dr, QualityRangeFormatter.FormatDrRange);
        AddRangeLabels(labels, snapshot, ExportAnalyticsSnapshotSchema.Filters.Traffic, QualityRangeFormatter.FormatTrafficRange);
        AddRangeLabels(labels, snapshot, ExportAnalyticsSnapshotSchema.Filters.PriceUsd, QualityRangeFormatter.FormatPriceRange);
        AddValuesLabel(labels, snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Niche), singular: "Niche", plural: "niches");
        AddValuesLabel(labels, snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Categories), singular: "Category", plural: "categories");
        AddValuesLabel(labels, snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Language).Select(value => value.ToUpperInvariant()).ToArray(), singular: "Language", plural: "languages");
        AddValuesLabel(labels, snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.ExcludedNiche), singular: "Excluded niche", plural: "excluded niches");
        AddValuesLabel(labels, snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.ExcludedCategories), singular: "Excluded category", plural: "excluded categories");
        AddServiceLabels(labels, snapshot);

        if (snapshot.GetBooleanValue(ExportAnalyticsSnapshotSchema.Filters.StopList) == true)
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
        foreach (var locationKey in snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.LocationKey))
        {
            selectedLocationNames.Add(ResolveLocationName(locationKey, locationLookups));
        }

        foreach (var location in snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Location))
        {
            selectedLocationNames.Add(location);
        }

        if (snapshot.GetBooleanValue(ExportAnalyticsSnapshotSchema.Filters.LocationUnknown) == true)
        {
            selectedLocationNames.Add("Unknown");
        }

        if (snapshot.GetBooleanValue(ExportAnalyticsSnapshotSchema.Filters.LocationOther) == true)
        {
            selectedLocationNames.Add(OtherLocationName);
        }

        foreach (var groupKey in snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.LocationGroup))
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
        foreach (var service in ExportAnalyticsServiceFilters.All)
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

    private static IReadOnlyList<string>? BuildSearchLabels(ExportAnalyticsSearchSnapshot searchSnapshot)
    {
        if (!searchSnapshot.IsAvailable)
        {
            return null;
        }

        if (searchSnapshot.IsMultiSearch == true)
        {
            return [FormatMultiSearchLabel(searchSnapshot)];
        }

        if (!string.IsNullOrWhiteSpace(searchSnapshot.CatalogQuery))
        {
            return [$"Search {searchSnapshot.CatalogQuery}"];
        }

        return [];
    }

    private static string FormatMultiSearchLabel(ExportAnalyticsSearchSnapshot searchSnapshot)
    {
        if (!searchSnapshot.UniqueInputCount.HasValue)
        {
            return "Multi-search";
        }

        return $"Multi-search {searchSnapshot.UniqueInputCount.Value.ToString("N0", CultureInfo.GetCultureInfo("en-US"))} domains";
    }

    private static string FormatFilterCount(int count)
        => count == 1
            ? "1 filter"
            : $"{count.ToString("N0", CultureInfo.GetCultureInfo("en-US"))} filters";

    internal static string FormatSortField(string field)
        => field.Trim().ToLowerInvariant() switch
        {
            ExportAnalyticsSnapshotSchema.Sort.Domain => "Domain",
            ExportAnalyticsSnapshotSchema.Sort.Dr => "DR",
            ExportAnalyticsSnapshotSchema.Sort.Traffic => "Traffic",
            ExportAnalyticsSnapshotSchema.Sort.Location => "Location",
            "priceusd" => "Price",
            "pricecasino" => "Casino price",
            "pricecrypto" => "Crypto price",
            "pricelinkinsert" => "Link insert price",
            "pricelinkinsertcasino" => "Link insert casino price",
            "pricedating" => "Dating price",
            "numberdflinks" => "DF links",
            ExportAnalyticsSnapshotSchema.Sort.Term => "Term",
            "createdat" => "Created",
            "updatedat" => "Updated",
            "lastpublisheddate" => "Last published",
            _ => field.Trim()
        };

    internal static string FormatSortDirection(string? direction)
        => string.Equals(direction, SortingDefaults.Descending, StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";

    internal static string FormatSortDirectionLong(string? direction)
        => string.Equals(direction, SortingDefaults.Descending, StringComparison.OrdinalIgnoreCase)
            ? "descending"
            : "ascending";
}
