using System.Globalization;
using System.Text.Json;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportFiltersSnapshotParser
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(string? json, out FiltersSnapshot snapshot)
    {
        snapshot = new FiltersSnapshot([]);

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            // The snapshot envelope is stable; each filter value can still be an array,
            // boolean, scalar, or range object depending on the filter kind.
            var snapshotDocument = JsonSerializer.Deserialize<FiltersSnapshotDocument>(
                json,
                SnapshotJsonOptions);
            if (snapshotDocument?.Filters is null)
            {
                return false;
            }

            var filters = new List<FilterItem>();
            foreach (var filterDocument in snapshotDocument.Filters)
            {
                var filter = ParseFilter(filterDocument);
                if (filter != null)
                {
                    filters.Add(filter);
                }
            }

            snapshot = new FiltersSnapshot(filters);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static FilterItem? ParseFilter(FilterSnapshotItemDocument filterDocument)
    {
        if (string.IsNullOrWhiteSpace(filterDocument.Field))
        {
            return null;
        }

        var field = filterDocument.Field.Trim();
        var value = ReadFilterValue(filterDocument.Value);

        return new FilterItem(field, value.StringValues, value.BoolValue, value.Min, value.Max);
    }

    private static FilterValue ReadFilterValue(JsonElement valueElement)
    {
        if (valueElement.ValueKind == JsonValueKind.Undefined)
        {
            return new FilterValue([], null, null, null);
        }

        var stringValues = ReadStringValues(valueElement);
        var boolValue = valueElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? valueElement.GetBoolean()
            : (bool?)null;
        var (min, max) = ReadRange(valueElement);

        return new FilterValue(stringValues, boolValue, min, max);
    }

    private static IReadOnlyList<string> ReadStringValues(JsonElement valueElement)
    {
        if (valueElement.ValueKind == JsonValueKind.Array)
        {
            return valueElement.EnumerateArray()
                .Select(ReadStringValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToArray();
        }

        var singleValue = ReadStringValue(valueElement);
        return string.IsNullOrWhiteSpace(singleValue) ? [] : [singleValue.Trim()];
    }

    private static string? ReadStringValue(JsonElement valueElement)
    {
        return valueElement.ValueKind switch
        {
            JsonValueKind.String => valueElement.GetString(),
            JsonValueKind.Number => valueElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static (decimal? Min, decimal? Max) ReadRange(JsonElement valueElement)
    {
        if (valueElement.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        return (
            TryReadDecimalProperty(valueElement, "min", out var min) ? min : null,
            TryReadDecimalProperty(valueElement, "max", out var max) ? max : null);
    }

    private static bool TryReadDecimalProperty(JsonElement element, string propertyName, out decimal value)
    {
        value = default;
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetDecimal(out value);
        }

        return property.ValueKind == JsonValueKind.String &&
               decimal.TryParse(
                   property.GetString(),
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out value);
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

    private sealed class FiltersSnapshotDocument
    {
        public int SchemaVersion { get; set; }
        public IReadOnlyList<FilterSnapshotItemDocument>? Filters { get; set; }
    }

    private sealed class FilterSnapshotItemDocument
    {
        public string? Field { get; set; }
        public string? Kind { get; set; }
        public string? Operator { get; set; }
        public JsonElement Value { get; set; }
    }
}
