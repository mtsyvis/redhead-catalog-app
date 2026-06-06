using System.Globalization;

namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class QualityRangeFormatter
{
    public static string? FormatDrRange(RangeValue range)
        => FormatRangeLabel("DR", range.Min, range.Max, FormatPlainNumber);

    public static string? FormatTrafficRange(RangeValue range)
        => FormatRangeLabel("Traffic", range.Min, range.Max, FormatWholeNumber);

    public static string? FormatPriceRange(RangeValue range)
        => FormatRangeLabel("Price", range.Min, range.Max, FormatUsd);

    private static string? FormatRangeLabel(
        string prefix,
        decimal? min,
        decimal? max,
        Func<decimal, string> formatValue)
    {
        return (min, max) switch
        {
            (decimal minValue, decimal maxValue) => $"{prefix} {formatValue(minValue)}-{formatValue(maxValue)}",
            (decimal minValue, null) => $"{prefix} {formatValue(minValue)}+",
            (null, decimal maxValue) => $"{prefix} up to {formatValue(maxValue)}",
            _ => null
        };
    }

    private static string FormatPlainNumber(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatWholeNumber(decimal value)
        => decimal.Round(value, 0).ToString("N0", CultureInfo.GetCultureInfo("en-US"));

    private static string FormatUsd(decimal value)
        => "$" + value.ToString(value % 1 == 0 ? "N0" : "N2", CultureInfo.GetCultureInfo("en-US"));
}
