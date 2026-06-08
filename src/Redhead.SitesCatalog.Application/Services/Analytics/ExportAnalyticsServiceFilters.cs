namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportAnalyticsServiceFilters
{
    public static ExportAnalyticsServiceFilterDefinition Casino { get; } =
        new(ExportAnalyticsSnapshotSchema.Filters.PriceCasinoAvailability, "Casino");

    public static ExportAnalyticsServiceFilterDefinition Crypto { get; } =
        new(ExportAnalyticsSnapshotSchema.Filters.PriceCryptoAvailability, "Crypto");

    public static ExportAnalyticsServiceFilterDefinition LinkInsert { get; } =
        new(ExportAnalyticsSnapshotSchema.Filters.PriceLinkInsertAvailability, "Link insert");

    public static ExportAnalyticsServiceFilterDefinition LinkInsertCasino { get; } =
        new(ExportAnalyticsSnapshotSchema.Filters.PriceLinkInsertCasinoAvailability, "Link insert casino");

    public static ExportAnalyticsServiceFilterDefinition Dating { get; } =
        new(ExportAnalyticsSnapshotSchema.Filters.PriceDatingAvailability, "Dating");

    public static IReadOnlyList<ExportAnalyticsServiceFilterDefinition> All { get; } =
    [
        Casino,
        Crypto,
        LinkInsert,
        LinkInsertCasino,
        Dating
    ];
}

internal sealed record ExportAnalyticsServiceFilterDefinition(string Field, string DisplayName);
