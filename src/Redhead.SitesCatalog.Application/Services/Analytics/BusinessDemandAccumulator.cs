using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal sealed class BusinessDemandAccumulator
{
    private const int TopListLimit = 10;
    private const string OtherLocationName = "Other";

    private static readonly ServiceFilterDefinition[] ServiceFilters =
    [
        new("priceCasinoAvailability", "Casino"),
        new("priceCryptoAvailability", "Crypto"),
        new("priceLinkInsertAvailability", "Link insert"),
        new("priceLinkInsertCasinoAvailability", "Link insert casino"),
        new("priceDatingAvailability", "Dating")
    ];

    private readonly BusinessDemandLocationLookups _locationLookups;
    private readonly Dictionary<string, int> _topLocations = CreateCounter();
    private readonly Dictionary<string, int> _topNiches = CreateCounter();
    private readonly Dictionary<string, int> _topCategories = CreateCounter();
    private readonly Dictionary<string, int> _topLanguages = CreateCounter();
    private readonly Dictionary<string, int> _drRanges = CreateCounter();
    private readonly Dictionary<string, int> _trafficRanges = CreateCounter();
    private readonly Dictionary<string, int> _priceRanges = CreateCounter();
    private readonly Dictionary<string, ServiceDemandCounter> _serviceCounters;

    private int _noFilters;
    private int _broadExports;
    private int _filteredExports;

    public BusinessDemandAccumulator(BusinessDemandLocationLookups locationLookups)
    {
        _locationLookups = locationLookups;
        _serviceCounters = ServiceFilters.ToDictionary(
            service => service.Field,
            service => new ServiceDemandCounter(service.DisplayName),
            StringComparer.Ordinal);
    }

    public void Add(ExportAnalyticsLogRow row, FiltersSnapshot snapshot)
    {
        CountLocationDemand(snapshot);
        CountMultiSelectValues(snapshot, "niche", _topNiches);
        CountMultiSelectValues(snapshot, "categories", _topCategories);
        CountMultiSelectValues(snapshot, "language", _topLanguages);
        CountServiceDemand(snapshot);
        CountRangeDemand(snapshot, "dr", QualityRangeFormatter.FormatDrRange, _drRanges);
        CountRangeDemand(snapshot, "traffic", QualityRangeFormatter.FormatTrafficRange, _trafficRanges);
        CountRangeDemand(snapshot, "priceUsd", QualityRangeFormatter.FormatPriceRange, _priceRanges);
        CountStrictness(row, snapshot);
    }

    public BusinessDemandAnalyticsDto ToDto(IReadOnlyList<ExportAnalyticsLogRow> rows)
        => new(
            Summary: new BusinessDemandSummaryDto(
                ExportRequests: rows.Count,
                ClientsWithExportActivity: rows
                    .Where(row => !string.IsNullOrWhiteSpace(row.UserId))
                    .Select(row => row.UserId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                RequestedRows: rows.Sum(row => (long)row.RequestedRowsCount),
                ExportedDomains: rows.Sum(row => (long)row.ExportedRowsCount)),
            TopLocations: ToTopList(_topLocations),
            TopNiches: ToTopList(_topNiches),
            TopCategories: ToTopList(_topCategories),
            TopLanguages: ToTopList(_topLanguages),
            ServiceDemand: _serviceCounters.Values
                .Where(counter => counter.WantedOrAvailableRequests > 0 || counter.ExplicitlyNoRequests > 0)
                .OrderBy(counter => counter.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(counter => new ServiceDemandDto(
                    counter.DisplayName,
                    counter.WantedOrAvailableRequests,
                    counter.ExplicitlyNoRequests))
                .ToArray(),
            QualityDemand: new QualityDemandDto(
                DrRanges: ToTopList(_drRanges),
                TrafficRanges: ToTopList(_trafficRanges),
                PriceRanges: ToTopList(_priceRanges)),
            FilterStrictness: new FilterStrictnessDto(
                NoFilters: _noFilters,
                BroadExports: _broadExports,
                FilteredExports: _filteredExports,
                BroadExportThreshold: FilterStrictnessClassifier.BroadExportThreshold));

    private void CountLocationDemand(FiltersSnapshot snapshot)
    {
        var selectedLocationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var locationKey in snapshot.GetStringValues("locationKey"))
        {
            selectedLocationKeys.Add(locationKey);
        }

        if (snapshot.GetBooleanValue("locationUnknown") == true)
        {
            selectedLocationKeys.Add(LocationConstants.UnknownLocationKey);
        }

        foreach (var groupKey in snapshot.GetStringValues("locationGroup"))
        {
            if (!_locationLookups.GroupLocationKeys.TryGetValue(groupKey, out var groupMembers))
            {
                continue;
            }

            foreach (var locationKey in groupMembers)
            {
                selectedLocationKeys.Add(locationKey);
            }
        }

        selectedLocationKeys.ExceptWith(snapshot.GetStringValues("excludedLocationKey"));

        var selectedNames = selectedLocationKeys
            .Select(ResolveLocationName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (snapshot.GetBooleanValue("locationOther") == true)
        {
            selectedNames.Add(OtherLocationName);
        }

        if (selectedNames.Count == 0)
        {
            selectedNames.UnionWith(snapshot.GetStringValues("location"));
        }

        foreach (var name in selectedNames)
        {
            Increment(_topLocations, name);
        }
    }

    private string ResolveLocationName(string locationKey)
    {
        if (_locationLookups.LocationNamesByKey.TryGetValue(locationKey, out var displayName))
        {
            return displayName;
        }

        return string.Equals(locationKey, LocationConstants.UnknownLocationKey, StringComparison.OrdinalIgnoreCase)
            ? "Unknown"
            : locationKey;
    }

    private static void CountMultiSelectValues(
        FiltersSnapshot snapshot,
        string field,
        Dictionary<string, int> counts)
    {
        foreach (var value in snapshot.GetStringValues(field).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Increment(counts, value);
        }
    }

    private void CountServiceDemand(FiltersSnapshot snapshot)
    {
        foreach (var service in ServiceFilters)
        {
            var selectedValues = snapshot.GetStringValues(service.Field)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (selectedValues.Count == 0)
            {
                continue;
            }

            var counter = _serviceCounters[service.Field];
            if (selectedValues.Contains("available") ||
                selectedValues.Contains("availableWithUnknownPrice"))
            {
                counter.WantedOrAvailableRequests++;
            }

            if (selectedValues.Contains("notAvailable"))
            {
                counter.ExplicitlyNoRequests++;
            }
        }
    }

    private static void CountRangeDemand(
        FiltersSnapshot snapshot,
        string field,
        Func<RangeValue, string?> formatRange,
        Dictionary<string, int> counts)
    {
        foreach (var range in snapshot.GetRanges(field))
        {
            var label = formatRange(range);
            if (!string.IsNullOrWhiteSpace(label))
            {
                Increment(counts, label);
            }
        }
    }

    private void CountStrictness(ExportAnalyticsLogRow row, FiltersSnapshot snapshot)
    {
        switch (FilterStrictnessClassifier.Classify(snapshot, row.RequestedRowsCount))
        {
            case BusinessDemandFilterStrictness.NoFilters:
                _noFilters++;
                break;
            case BusinessDemandFilterStrictness.BroadExport:
                _broadExports++;
                break;
            case BusinessDemandFilterStrictness.FilteredExport:
                _filteredExports++;
                break;
        }
    }

    private static Dictionary<string, int> CreateCounter()
        => new(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<BusinessDemandCountDto> ToTopList(Dictionary<string, int> counts)
        => counts
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Take(TopListLimit)
            .Select(item => new BusinessDemandCountDto(item.Key, item.Value))
            .ToArray();

    private static void Increment(Dictionary<string, int> counts, string value)
    {
        var key = value.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    private sealed record ServiceFilterDefinition(string Field, string DisplayName);

    private sealed class ServiceDemandCounter
    {
        public ServiceDemandCounter(string displayName)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; }
        public int WantedOrAvailableRequests { get; set; }
        public int ExplicitlyNoRequests { get; set; }
    }
}
