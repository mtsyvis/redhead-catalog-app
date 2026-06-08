namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class FilterStrictnessClassifier
{
    public const int BroadExportThreshold = 5000;

    public static BusinessDemandFilterStrictness Classify(
        FiltersSnapshot snapshot,
        int requestedRowsCount)
    {
        if (!HasMeaningfulFilters(snapshot))
        {
            return BusinessDemandFilterStrictness.NoFilters;
        }

        return requestedRowsCount > BroadExportThreshold
            ? BusinessDemandFilterStrictness.BroadExport
            : BusinessDemandFilterStrictness.FilteredExport;
    }

    private static bool HasMeaningfulFilters(FiltersSnapshot snapshot)
    {
        foreach (var filter in snapshot.Filters)
        {
            // Topic fit only changes how Niche and Categories combine. It is not demand by itself.
            if (string.Equals(filter.Field, ExportAnalyticsSnapshotSchema.Filters.TopicFitMode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // The Sites page defaults to excluding quarantined rows, so this default should not
            // make an otherwise unfiltered export look like a filtered request.
            if (string.Equals(filter.Field, ExportAnalyticsSnapshotSchema.Filters.Quarantine, StringComparison.OrdinalIgnoreCase) &&
                filter.StringValues.Any(value => string.Equals(value, "exclude", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Any remaining selected value, boolean flag, or min/max range represents a real
            // client-selected filter for the strictness buckets.
            if (filter.BoolValue == true || filter.StringValues.Count > 0 || filter.Min.HasValue || filter.Max.HasValue)
            {
                return true;
            }
        }

        return false;
    }
}

internal enum BusinessDemandFilterStrictness
{
    NoFilters,
    BroadExport,
    FilteredExport
}
