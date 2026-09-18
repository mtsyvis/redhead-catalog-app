namespace Redhead.SitesCatalog.Application.Services.Analytics.ExportAnalytics;

internal sealed record ExportAnalyticsLogRow(
    string UserId,
    int RequestedRowsCount,
    int ExportedRowsCount,
    string? FiltersSnapshotJson);

internal sealed record BusinessDemandLocationLookups(
    IReadOnlyDictionary<string, string> LocationNamesByKey,
    IReadOnlyDictionary<string, string[]> GroupLocationKeys);
