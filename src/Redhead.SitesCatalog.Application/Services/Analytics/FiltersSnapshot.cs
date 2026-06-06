namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal sealed class FiltersSnapshot
{
    public FiltersSnapshot(IReadOnlyList<FilterItem> filters)
    {
        Filters = filters;
    }

    public IReadOnlyList<FilterItem> Filters { get; }

    public IReadOnlyList<string> GetStringValues(string field)
        => Filters
            .Where(filter => string.Equals(filter.Field, field, StringComparison.OrdinalIgnoreCase))
            .SelectMany(filter => filter.StringValues)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

    public bool? GetBooleanValue(string field)
        => Filters
            .FirstOrDefault(filter => string.Equals(filter.Field, field, StringComparison.OrdinalIgnoreCase))
            ?.BoolValue;

    public IReadOnlyList<RangeValue> GetRanges(string field)
        => Filters
            .Where(filter => string.Equals(filter.Field, field, StringComparison.OrdinalIgnoreCase))
            .Where(filter => filter.Min.HasValue || filter.Max.HasValue)
            .Select(filter => new RangeValue(filter.Min, filter.Max))
            .ToArray();
}

internal sealed record FilterItem(
    string Field,
    IReadOnlyList<string> StringValues,
    bool? BoolValue,
    decimal? Min,
    decimal? Max);

internal sealed record FilterValue(
    IReadOnlyList<string> StringValues,
    bool? BoolValue,
    decimal? Min,
    decimal? Max);

internal sealed record RangeValue(decimal? Min, decimal? Max);
