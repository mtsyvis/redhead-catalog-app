using System.Globalization;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportLogDetailsMapper
{
    private const string NoFilter = "No filter";
    private const string Unavailable = "Unavailable";
    private const string OtherLocationName = "Other";

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public static ExportLogDetailsDto Map(
        ExportLogDetailsSource source,
        string? displayName,
        BusinessDemandLocationLookups locationLookups)
    {
        var filtersParsed = ExportFiltersSnapshotParser.TryParse(
            source.FiltersSnapshotJson,
            out var filtersSnapshot);
        if (!filtersParsed)
        {
            filtersSnapshot = new FiltersSnapshot([]);
        }

        var searchDetails = ExportAnalyticsSearchSnapshotParser.Parse(source.SearchSnapshotJson);

        return new ExportLogDetailsDto(
            Id: source.Id,
            TimestampUtc: source.TimestampUtc,
            UserId: source.UserId,
            Email: source.UserEmail,
            DisplayName: displayName,
            Destination: source.Destination,
            Status: GetStatus(source.BlockedReason, source.WasTruncated),
            RequestedRows: source.RequestedRows,
            ExportedRows: source.ExportedRows,
            BlockedReason: source.BlockedReason,
            OutcomeReason: ExportActivityReasonFormatter.Format(
                source.BlockedReason,
                source.WasTruncated,
                source.ExportLimitRows,
                source.ExportedRows),
            ExportMode: source.ExportMode,
            AppliedFilters: BuildAppliedFilters(
                filtersSnapshot,
                filtersParsed,
                source.FiltersSnapshotJson,
                searchDetails,
                locationLookups),
            Sort: BuildSort(source.SortSnapshotJson),
            TechnicalDetails: BuildTechnicalDetails(
                source.FiltersSnapshotJson,
                source.SortSnapshotJson,
                source.SearchSnapshotJson));
    }

    private static IReadOnlyList<ExportLogDetailsSectionDto> BuildAppliedFilters(
        FiltersSnapshot snapshot,
        bool filtersParsed,
        string? filtersSnapshotJson,
        ExportAnalyticsSearchSnapshot searchDetails,
        BusinessDemandLocationLookups locationLookups)
    {
        var sections = new List<ExportLogDetailsSectionDto>();
        if (!filtersParsed && !string.IsNullOrWhiteSpace(filtersSnapshotJson))
        {
            sections.Add(new ExportLogDetailsSectionDto(
                "Snapshot",
                [new ExportLogDetailsRowDto("Filter snapshot", Unavailable)]));
        }

        sections.Add(new ExportLogDetailsSectionDto(
            "Locations",
            [
                new ExportLogDetailsRowDto("Locations", FormatLocations(snapshot, locationLookups)),
                new ExportLogDetailsRowDto("Excluded locations", FormatExcludedLocations(snapshot, locationLookups))
            ]));

        sections.Add(new ExportLogDetailsSectionDto(
            "Quality and price",
            [
                new ExportLogDetailsRowDto("DR", FormatRanges(snapshot, ExportAnalyticsSnapshotSchema.Filters.Dr, FormatPlainNumber)),
                new ExportLogDetailsRowDto("Traffic", FormatRanges(snapshot, ExportAnalyticsSnapshotSchema.Filters.Traffic, FormatWholeNumber)),
                new ExportLogDetailsRowDto("Price USD", FormatRanges(snapshot, ExportAnalyticsSnapshotSchema.Filters.PriceUsd, FormatUsd))
            ]));

        sections.Add(new ExportLogDetailsSectionDto(
            "Status",
            [
                new ExportLogDetailsRowDto("Status", FormatQuarantine(snapshot)),
                new ExportLogDetailsRowDto("Last published", FormatLastPublished(snapshot)),
                new ExportLogDetailsRowDto("Stop list", snapshot.GetBooleanValue(ExportAnalyticsSnapshotSchema.Filters.StopList) == true ? "Active" : NoFilter)
            ]));

        sections.Add(new ExportLogDetailsSectionDto(
            "Topic and language",
            [
                new ExportLogDetailsRowDto("Languages", FormatValues(
                    snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Language).Select(value => value.ToUpperInvariant()))),
                new ExportLogDetailsRowDto("Niches", FormatValues(snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Niche))),
                new ExportLogDetailsRowDto("Categories", FormatValues(snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Categories))),
                new ExportLogDetailsRowDto("Topic fit", FormatTopicFit(snapshot)),
                new ExportLogDetailsRowDto("Excluded niches", FormatValues(snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.ExcludedNiche))),
                new ExportLogDetailsRowDto("Excluded categories", FormatValues(snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.ExcludedCategories)))
            ]));

        sections.Add(new ExportLogDetailsSectionDto(
            "Optional services",
            ExportAnalyticsServiceFilters.All
                .Select(service => new ExportLogDetailsRowDto(
                    service.DisplayName,
                    FormatAvailabilityValues(snapshot.GetStringValues(service.Field))))
                .ToArray()));

        sections.Add(new ExportLogDetailsSectionDto(
            "Multi-search",
            [
                new ExportLogDetailsRowDto("Enabled", FormatBoolean(searchDetails.IsMultiSearch)),
                new ExportLogDetailsRowDto("Input domains count", FormatInteger(searchDetails.InputCount)),
                new ExportLogDetailsRowDto("Unique input domains count", FormatInteger(searchDetails.UniqueInputCount)),
                new ExportLogDetailsRowDto("Found count", FormatInteger(searchDetails.FoundCount)),
                new ExportLogDetailsRowDto("Not found count", FormatInteger(searchDetails.NotFoundCount)),
                new ExportLogDetailsRowDto("Filters active", filtersParsed ? FormatBoolean(snapshot.Filters.Count > 0) : Unavailable),
                new ExportLogDetailsRowDto("Catalog search", searchDetails.CatalogQuery ?? NoFilter)
            ]));

        return sections;
    }

    private static ExportLogSortDetailsDto BuildSort(string? sortSnapshotJson)
    {
        if (!ExportSortSnapshotParser.TryParse(sortSnapshotJson, out var snapshot))
        {
            return string.IsNullOrWhiteSpace(sortSnapshotJson)
                ? new ExportLogSortDetailsDto("No sort", [])
                : new ExportLogSortDetailsDto(Unavailable, []);
        }

        if (snapshot.Sorts.Count == 0)
        {
            return new ExportLogSortDetailsDto("No sort", []);
        }

        var rows = snapshot.Sorts
            .Select(sort => new ExportLogDetailsRowDto(
                ExportActivitySnapshotSummaryFormatter.FormatSortField(sort.Field),
                ExportActivitySnapshotSummaryFormatter.FormatSortDirectionLong(sort.Direction)))
            .ToArray();

        return new ExportLogSortDetailsDto(
            string.Join(
                ", ",
                rows.Select(row => $"{row.Label} {row.Value}")),
            rows);
    }

    private static ExportLogTechnicalDetailsDto? BuildTechnicalDetails(
        string? filtersSnapshotJson,
        string? sortSnapshotJson,
        string? searchSnapshotJson)
    {
        var hasFilters = !string.IsNullOrWhiteSpace(filtersSnapshotJson);
        var hasSort = !string.IsNullOrWhiteSpace(sortSnapshotJson);
        var hasSearch = !string.IsNullOrWhiteSpace(searchSnapshotJson);

        return hasFilters || hasSort || hasSearch
            ? new ExportLogTechnicalDetailsDto(
                hasFilters ? filtersSnapshotJson : null,
                hasSort ? sortSnapshotJson : null,
                hasSearch ? searchSnapshotJson : null)
            : null;
    }

    private static string FormatLocations(
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

        foreach (var groupKey in snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.LocationGroup))
        {
            selectedLocationNames.Add($"{groupKey} group");
        }

        if (snapshot.GetBooleanValue(ExportAnalyticsSnapshotSchema.Filters.LocationUnknown) == true)
        {
            selectedLocationNames.Add("Unknown");
        }

        if (snapshot.GetBooleanValue(ExportAnalyticsSnapshotSchema.Filters.LocationOther) == true)
        {
            selectedLocationNames.Add(OtherLocationName);
        }

        return FormatValues(selectedLocationNames);
    }

    private static string FormatExcludedLocations(
        FiltersSnapshot snapshot,
        BusinessDemandLocationLookups locationLookups)
        => FormatValues(snapshot
            .GetStringValues(ExportAnalyticsSnapshotSchema.Filters.ExcludedLocationKey)
            .Select(locationKey => ResolveLocationName(locationKey, locationLookups)));

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

    private static string FormatRanges(
        FiltersSnapshot snapshot,
        string field,
        Func<decimal, string> formatValue)
    {
        var labels = snapshot.GetRanges(field)
            .Select(range => FormatRange(range, formatValue))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return labels.Length == 0 ? NoFilter : string.Join(" / ", labels);
    }

    private static string? FormatRange(RangeValue range, Func<decimal, string> formatValue)
        => (range.Min, range.Max) switch
        {
            (decimal minValue, decimal maxValue) => $"{formatValue(minValue)}-{formatValue(maxValue)}",
            (decimal minValue, null) => $"From {formatValue(minValue)}",
            (null, decimal maxValue) => $"Up to {formatValue(maxValue)}",
            _ => null
        };

    private static string FormatQuarantine(FiltersSnapshot snapshot)
    {
        var value = snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.Quarantine).FirstOrDefault();
        return value?.Trim().ToLowerInvariant() switch
        {
            QuarantineFilterValues.Exclude => "Available",
            QuarantineFilterValues.Only => "Unavailable",
            QuarantineFilterValues.All => "All sites",
            _ => NoFilter
        };
    }

    private static string FormatLastPublished(FiltersSnapshot snapshot)
    {
        var values = snapshot.GetObjectValues(ExportAnalyticsSnapshotSchema.Filters.LastPublishedDate);
        if (values.Count == 0)
        {
            return NoFilter;
        }

        var labels = values
            .Select(FormatMonthObject)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();

        return labels.Length == 0 ? NoFilter : string.Join(" / ", labels);
    }

    private static string? FormatMonthObject(IReadOnlyDictionary<string, string> value)
    {
        value.TryGetValue("minMonth", out var minMonth);
        value.TryGetValue("maxMonth", out var maxMonth);
        value.TryGetValue("month", out var month);

        if (!string.IsNullOrWhiteSpace(minMonth) && !string.IsNullOrWhiteSpace(maxMonth))
        {
            return $"{minMonth}-{maxMonth}";
        }

        if (!string.IsNullOrWhiteSpace(minMonth))
        {
            return $"From {minMonth}";
        }

        if (!string.IsNullOrWhiteSpace(maxMonth))
        {
            return $"Up to {maxMonth}";
        }

        return string.IsNullOrWhiteSpace(month) ? null : month;
    }

    private static string FormatTopicFit(FiltersSnapshot snapshot)
    {
        var value = snapshot.GetStringValues(ExportAnalyticsSnapshotSchema.Filters.TopicFitMode).FirstOrDefault();
        return value?.Trim().ToLowerInvariant() switch
        {
            TopicFitModeValues.Expand => "Expand",
            TopicFitModeValues.Narrow => "Narrow",
            _ => NoFilter
        };
    }

    private static string FormatAvailabilityValues(IReadOnlyList<string> values)
    {
        var labels = values
            .Select(FormatAvailabilityValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return labels.Length == 0 ? NoFilter : string.Join(", ", labels);
    }

    private static string FormatAvailabilityValue(string value)
        => value.Trim() switch
        {
            "available" => "Has price",
            "availableWithUnknownPrice" => "YES",
            "notAvailable" => "NO",
            "unknown" => "Unknown",
            _ => value.Trim()
        };

    private static string FormatValues(IEnumerable<string> values)
    {
        var activeValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return activeValues.Length == 0 ? NoFilter : string.Join(", ", activeValues);
    }

    private static string FormatInteger(int? value)
        => value.HasValue ? value.Value.ToString("N0", EnUs) : NoFilter;

    private static string FormatBoolean(bool? value)
        => value switch
        {
            true => "Yes",
            false => "No",
            _ => Unavailable
        };

    private static string GetStatus(string? blockedReason, bool wasTruncated)
        => blockedReason != null
            ? AnalyticsExportStatusLabels.Blocked
            : wasTruncated
                ? AnalyticsExportStatusLabels.Partial
                : AnalyticsExportStatusLabels.Successful;

    private static string FormatPlainNumber(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatWholeNumber(decimal value)
        => decimal.Round(value, 0).ToString("N0", EnUs);

    private static string FormatUsd(decimal value)
        => "$" + value.ToString(value % 1 == 0 ? "N0" : "N2", EnUs);

}

internal sealed record ExportLogDetailsSource(
    Guid Id,
    DateTime TimestampUtc,
    string UserId,
    string UserEmail,
    string? Destination,
    int RequestedRows,
    int ExportedRows,
    bool WasTruncated,
    int? ExportLimitRows,
    string? BlockedReason,
    string? ExportMode,
    string? FiltersSnapshotJson,
    string? SortSnapshotJson,
    string? SearchSnapshotJson);
