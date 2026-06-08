using System.Text.Json;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportSortSnapshotParser
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(string? json, out SortsSnapshot snapshot)
    {
        snapshot = new SortsSnapshot([]);

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var document = JsonSerializer.Deserialize<SortsSnapshotDocument>(
                json,
                SnapshotJsonOptions);
            if (document?.Sorts is null)
            {
                return false;
            }

            snapshot = new SortsSnapshot(
                document.Sorts
                    .Where(sort => !string.IsNullOrWhiteSpace(sort.Field))
                    .Select(sort => new SortItem(
                        sort.Field!.Trim(),
                        sort.Direction,
                        sort.Priority))
                    .OrderBy(sort => sort.Priority)
                    .ToArray());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

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

internal sealed record SortsSnapshot(IReadOnlyList<SortItem> Sorts);

internal sealed record SortItem(
    string Field,
    string? Direction,
    int Priority);
