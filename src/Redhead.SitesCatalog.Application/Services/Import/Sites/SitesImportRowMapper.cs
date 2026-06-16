using System.Globalization;
using CsvHelper;
using CsvHelper.TypeConversion;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Import.ValueParsers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Import.Sites;

/// <summary>
/// Provides functionality to map imported site data rows to strongly typed data transfer objects for further
/// processing.
/// </summary>
/// <remarks>This class is intended for use when importing site data from external sources, such as CSV or
/// spreadsheet files. It extracts and parses column values using provided delegates and maps them to a structured
/// format. All members are static and thread safe.</remarks>
public static class SitesImportRowMapper
{
    internal static SitesImportRowDto Map(
        Func<string, string?> getValue,
        int rowNumber,
        SitesImportHeaderInfo? insertHeaderInfo = null)
    {
        var domain = TryGetValue(getValue, ImportConstants.SitesImportColumns.Domain);
        var drRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.DR);
        var trafficRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.Traffic);
        var location = TryGetValue(getValue, ImportConstants.SitesImportColumns.Location);
        var numberDFLinksRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.NumberDFLinks);
        var language = TryGetValue(getValue, ImportConstants.SitesImportColumns.Language);
        var niche = TryGetValue(getValue, ImportConstants.SitesImportColumns.Niche);
        var categories = TryGetValue(getValue, ImportConstants.SitesImportColumns.Categories);
        var sponsoredTag = TryGetValue(getValue, ImportConstants.SitesImportColumns.SponsoredTag);

        var row = new SitesImportRowDto
        {
            RowNumber = rowNumber,
            Domain = domain,
            DRRaw = drRaw,
            DR = ParseNullableDouble(drRaw),
            TrafficRaw = trafficRaw,
            Traffic = ParseNullableLong(trafficRaw),
            Location = location,
            NumberDFLinksRaw = numberDFLinksRaw,
            NumberDFLinks = ParseNullableIntStrict(numberDFLinksRaw),
            Language = language,
            Niche = niche,
            Categories = categories,
            SponsoredTag = sponsoredTag
        };

        if (insertHeaderInfo is null)
        {
            return row;
        }

        foreach (var priceColumn in insertHeaderInfo.PriceColumns)
        {
            row.PriceCells.Add(new SitesImportPriceCell(
                priceColumn.Header,
                priceColumn.PriceType,
                priceColumn.Term.TermKey,
                priceColumn.Term.TermType,
                priceColumn.Term.TermValue,
                priceColumn.Term.TermUnit,
                TryGetValue(getValue, priceColumn.Header)));
        }

        foreach (var availabilityColumn in insertHeaderInfo.AvailabilityColumns)
        {
            row.AvailabilityCells.Add(new SitesImportAvailabilityCell(
                availabilityColumn.Header,
                availabilityColumn.ServiceType,
                TryGetValue(getValue, availabilityColumn.Header)));
        }

        return row;
    }

    private static string? TryGetValue(Func<string, string?> getValue, string columnName)
    {
        try
        {
            return getValue(columnName);
        }
        catch (CsvHelper.MissingFieldException)
        {
            return null;
        }
        catch (TypeConverterException)
        {
            return null;
        }
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DecimalParsingHelper.TryParseDecimalFlexible(value, out var parsed)
            ? parsed
            : null;
    }

    private static double? ParseNullableDouble(string? value)
    {
        var parsed = ParseNullableDecimal(value);
        return parsed is null ? null : (double)parsed.Value;
    }

    private static long? ParseNullableLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue)
            && decimalValue % 1 == 0
            && decimalValue >= long.MinValue
            && decimalValue <= long.MaxValue)
        {
            return (long)decimalValue;
        }

        return null;
    }

    private static int? ParseNullableIntStrict(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
