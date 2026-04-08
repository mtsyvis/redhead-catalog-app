using System.Globalization;
using CsvHelper;
using CsvHelper.TypeConversion;
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
        var domain = TryGetValue(getValue, ImportConstants.SitesImportColumns.Domain);
        var drRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.DR);
        var trafficRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.Traffic);
        var location = TryGetValue(getValue, ImportConstants.SitesImportColumns.Location);
        var priceUsdRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.PriceUsd);
        var priceCasinoRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.PriceCasino);
        var priceCryptoRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.PriceCrypto);
        var priceLinkInsertRaw = TryGetValue(getValue, ImportConstants.SitesImportColumns.PriceLinkInsert);
        var niche = TryGetValue(getValue, ImportConstants.SitesImportColumns.Niche);
        var categories = TryGetValue(getValue, ImportConstants.SitesImportColumns.Categories);
        var linkType = TryGetValue(getValue, ImportConstants.SitesImportColumns.LinkType);
        var sponsoredTag = TryGetValue(getValue, ImportConstants.SitesImportColumns.SponsoredTag);

        return new SitesImportRowDto
        {
            RowNumber = rowNumber,
            Domain = domain,
            DRRaw = drRaw,
            DR = ParseNullableDouble(drRaw),
            Traffic = ParseNullableLong(trafficRaw),
            Location = location,
            PriceUsdRaw = priceUsdRaw,
            PriceUsd = ParseNullableDecimal(priceUsdRaw),
            PriceCasinoRaw = priceCasinoRaw,
            PriceCryptoRaw = priceCryptoRaw,
            PriceLinkInsertRaw = priceLinkInsertRaw,
            Niche = niche,
            Categories = categories,
            LinkType = linkType,
            SponsoredTag = sponsoredTag
        };
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
}
