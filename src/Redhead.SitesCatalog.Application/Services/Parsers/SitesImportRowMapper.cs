using System.Globalization;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Provides functionality to map imported site data rows to strongly typed data transfer objects for further
/// processing.
/// </summary>
/// <remarks>This class is intended for use when importing site data from external sources, such as CSV or
/// spreadsheet files. It extracts and parses column values using provided delegates and maps them to a structured
/// format. All members are static and thread safe.</remarks>
public static class SitesImportRowMapper
{
    public static SitesImportRowDto Map(Func<string, string?> getValue, int rowNumber)
    {
        var domain = getValue(ImportConstants.SitesImportColumns.Domain);
        var drRaw = getValue(ImportConstants.SitesImportColumns.DR);
        var trafficRaw = getValue(ImportConstants.SitesImportColumns.Traffic);
        var location = getValue(ImportConstants.SitesImportColumns.Location);
        var priceUsdRaw = getValue(ImportConstants.SitesImportColumns.PriceUsd);
        var priceCasinoRaw = getValue(ImportConstants.SitesImportColumns.PriceCasino);
        var priceCryptoRaw = getValue(ImportConstants.SitesImportColumns.PriceCrypto);
        var priceLinkInsertRaw = getValue(ImportConstants.SitesImportColumns.PriceLinkInsert);
        var niche = getValue(ImportConstants.SitesImportColumns.Niche);
        var categories = getValue(ImportConstants.SitesImportColumns.Categories);

        return new SitesImportRowDto
        {
            RowNumber = rowNumber,
            Domain = domain,
            DRRaw = drRaw,
            DR = ParseNullableDouble(drRaw),
            Traffic = ParseNullableLong(trafficRaw),
            Location = location,
            PriceUsd = ParseNullableDecimal(priceUsdRaw),
            PriceCasinoRaw = priceCasinoRaw,
            PriceCryptoRaw = priceCryptoRaw,
            PriceLinkInsertRaw = priceLinkInsertRaw,
            Niche = niche,
            Categories = categories
        };
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
}
