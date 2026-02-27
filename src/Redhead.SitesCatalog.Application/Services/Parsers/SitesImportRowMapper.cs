using System.Globalization;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Maps a row of column values to SitesImportRowDto. Shared by CSV and (future) XLSX parsers.
/// </summary>
public static class SitesImportRowMapper
{
    /// <summary>
    /// Builds a DTO from a getter that returns the raw string value for a column name.
    /// </summary>
    public static SitesImportRowDto Map(Func<string, string?> getValue, int rowNumber)
    {
        string? Get(string name) => getValue(name);

        static decimal? ParseDecimalFlexible(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            return DecimalParsingHelper.TryParseDecimalFlexible(s, out var value) ? value : null;
        }

        static long? ParseLong(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            s = s.Trim();

            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }

            // Some exports may format integers as "123.0". Try double then cast.
            if (double.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            {
                return (long)d;
            }

            return null;
        }

        return new SitesImportRowDto
        {
            RowNumber = rowNumber,
            Domain = Get(ImportConstants.SitesImportColumns.Domain),
            DRRaw = Get(ImportConstants.SitesImportColumns.DR),
            DR = ParseDecimalFlexible(Get(ImportConstants.SitesImportColumns.DR)) is { } dr ? (double?)dr : null,
            Traffic = ParseLong(Get(ImportConstants.SitesImportColumns.Traffic)),
            Location = Get(ImportConstants.SitesImportColumns.Location),
            PriceUsd = ParseDecimalFlexible(Get(ImportConstants.SitesImportColumns.PriceUsd)),
            PriceCasino = ParseDecimalFlexible(Get(ImportConstants.SitesImportColumns.PriceCasino)),
            PriceCrypto = ParseDecimalFlexible(Get(ImportConstants.SitesImportColumns.PriceCrypto)),
            PriceLinkInsert = ParseDecimalFlexible(Get(ImportConstants.SitesImportColumns.PriceLinkInsert)),
            Niche = Get(ImportConstants.SitesImportColumns.Niche),
            Categories = Get(ImportConstants.SitesImportColumns.Categories),
        };
    }
}
