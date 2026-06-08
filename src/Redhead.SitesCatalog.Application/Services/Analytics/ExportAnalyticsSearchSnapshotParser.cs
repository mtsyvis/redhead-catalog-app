using System.Text.Json;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportAnalyticsSearchSnapshotParser
{
    public static ExportAnalyticsSearchSnapshot Parse(string? searchSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(searchSnapshotJson))
        {
            return ExportAnalyticsSearchSnapshot.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(searchSnapshotJson);
            var root = document.RootElement;
            if (!TryGetProperty(root, ExportAnalyticsSnapshotSchema.Search.Mode, out var modeElement) ||
                modeElement.ValueKind != JsonValueKind.String)
            {
                return ExportAnalyticsSearchSnapshot.Empty;
            }

            var mode = modeElement.GetString();
            if (string.Equals(mode, ExportAnalyticsSnapshotSchema.Search.MultiSearchMode, StringComparison.OrdinalIgnoreCase))
            {
                return ExportAnalyticsSearchSnapshot.ForMultiSearch(
                    TryReadInt(root, ExportAnalyticsSnapshotSchema.Search.InputCount),
                    TryReadInt(root, ExportAnalyticsSnapshotSchema.Search.UniqueInputCount),
                    TryReadInt(root, ExportAnalyticsSnapshotSchema.Search.FoundCount),
                    TryReadInt(root, ExportAnalyticsSnapshotSchema.Search.NotFoundCount));
            }

            if (string.Equals(mode, ExportAnalyticsSnapshotSchema.Search.CatalogSearchMode, StringComparison.OrdinalIgnoreCase) &&
                TryGetProperty(root, ExportAnalyticsSnapshotSchema.Search.Query, out var queryElement) &&
                queryElement.ValueKind == JsonValueKind.String)
            {
                return ExportAnalyticsSearchSnapshot.ForCatalogSearch(queryElement.GetString());
            }

            return ExportAnalyticsSearchSnapshot.Empty;
        }
        catch (JsonException)
        {
            return ExportAnalyticsSearchSnapshot.Unavailable;
        }
    }

    private static int? TryReadInt(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            return null;
        }

        return value;
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
}

internal sealed record ExportAnalyticsSearchSnapshot(
    bool IsAvailable,
    bool? IsMultiSearch,
    string? CatalogQuery,
    int? InputCount,
    int? UniqueInputCount,
    int? FoundCount,
    int? NotFoundCount)
{
    public static ExportAnalyticsSearchSnapshot Empty { get; } = new(
        IsAvailable: true,
        IsMultiSearch: false,
        CatalogQuery: null,
        InputCount: null,
        UniqueInputCount: null,
        FoundCount: null,
        NotFoundCount: null);

    public static ExportAnalyticsSearchSnapshot Unavailable { get; } = new(
        IsAvailable: false,
        IsMultiSearch: null,
        CatalogQuery: null,
        InputCount: null,
        UniqueInputCount: null,
        FoundCount: null,
        NotFoundCount: null);

    public static ExportAnalyticsSearchSnapshot ForCatalogSearch(string? query)
        => new(
            IsAvailable: true,
            IsMultiSearch: false,
            CatalogQuery: string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            InputCount: null,
            UniqueInputCount: null,
            FoundCount: null,
            NotFoundCount: null);

    public static ExportAnalyticsSearchSnapshot ForMultiSearch(
        int? inputCount,
        int? uniqueInputCount,
        int? foundCount,
        int? notFoundCount)
        => new(
            IsAvailable: true,
            IsMultiSearch: true,
            CatalogQuery: null,
            InputCount: inputCount,
            UniqueInputCount: uniqueInputCount,
            FoundCount: foundCount,
            NotFoundCount: notFoundCount);
}
