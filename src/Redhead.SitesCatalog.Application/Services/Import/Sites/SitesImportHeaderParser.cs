using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Application.Services.Import.Csv;

namespace Redhead.SitesCatalog.Application.Services.Import.Sites;

internal sealed record SitesImportHeaderInfo(
    IReadOnlyList<SitesImportPriceColumn> PriceColumns);

internal sealed record SitesImportPriceColumn(
    string Header,
    PriceType PriceType);

internal static class SitesImportHeaderParser
{
    private static readonly IReadOnlyDictionary<string, PriceType> PriceHeaders =
        new Dictionary<string, PriceType>(StringComparer.OrdinalIgnoreCase)
        {
            [ImportConstants.SitesImportColumns.PriceUsd] = PriceType.Main,
            [ImportConstants.SitesImportColumns.PriceCasino] = PriceType.Casino,
            [ImportConstants.SitesImportColumns.PriceCrypto] = PriceType.Crypto,
            [ImportConstants.SitesImportColumns.PriceLinkInsert] = PriceType.LinkInsertion,
            [ImportConstants.SitesImportColumns.PriceLinkInsertCasino] = PriceType.LinkInsertionCasino,
            [ImportConstants.SitesImportColumns.PriceDating] = PriceType.Dating
        };

    public static void ValidateOrThrow(string[] actualHeader)
    {
        _ = Parse(actualHeader);
    }

    public static SitesImportHeaderInfo Parse(string[] actualHeader)
    {
        actualHeader ??= Array.Empty<string>();

        if (actualHeader.Length == 0)
        {
            throw new ImportHeaderValidationException("CSV header row is missing.");
        }

        var seenBaseHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPriceTypes = new HashSet<PriceType>();
        var priceColumns = new List<SitesImportPriceColumn>();
        var hasTermHeader = false;

        for (var i = 0; i < actualHeader.Length; i++)
        {
            var header = CsvImportHelper.NormalizeHeader(actualHeader[i]);
            if (string.IsNullOrEmpty(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Column {i + 1} is empty.");
            }

            if (TryGetPriceType(header, out var priceType))
            {
                if (!seenPriceTypes.Add(priceType))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate pricing column: '{header}'.");
                }

                priceColumns.Add(new SitesImportPriceColumn(header, priceType));
                continue;
            }

            if (string.Equals(header, ImportConstants.SitesImportColumns.Term, StringComparison.OrdinalIgnoreCase))
            {
                if (!seenBaseHeaders.Add(header))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate column: '{header}'.");
                }

                hasTermHeader = true;
                continue;
            }

            if (header.StartsWith("Price", StringComparison.OrdinalIgnoreCase)
                || header.EndsWith("Availability", StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Unknown pricing column: '{header}'.");
            }

            if (ImportConstants.SitesImportRequiredColumns.Contains(header, StringComparer.OrdinalIgnoreCase)
                || ImportConstants.SitesImportOptionalColumns.Contains(header, StringComparer.OrdinalIgnoreCase))
            {
                if (!seenBaseHeaders.Add(header))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate column: '{header}'.");
                }

                continue;
            }

            throw new ImportHeaderValidationException($"CSV header is invalid. Unknown column: '{header}'.");
        }

        var missingRequiredHeaders = ImportConstants.SitesImportRequiredColumns
            .Where(header => !seenBaseHeaders.Contains(header))
            .ToArray();
        if (missingRequiredHeaders.Length > 0)
        {
            throw new ImportHeaderValidationException(
                $"CSV header is invalid. Missing required columns: {string.Join(", ", missingRequiredHeaders)}.");
        }

        if (priceColumns.Count > 0 && !hasTermHeader)
        {
            throw new ImportHeaderValidationException("CSV header is invalid. Term column is required when pricing columns are present.");
        }

        return new SitesImportHeaderInfo(priceColumns);
    }

    internal static bool TryGetPriceType(string header, out PriceType priceType)
    {
        return PriceHeaders.TryGetValue(header, out priceType);
    }

    internal static bool TryParseTermCell(string? rawTerm, out PricingTerm term)
    {
        term = PricingTerm.Unknown;

        var trimmed = rawTerm?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            term = PricingTerm.Unknown;
            return true;
        }

        if (string.Equals(trimmed, "no term", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "unknown term", StringComparison.OrdinalIgnoreCase))
        {
            term = PricingTerm.Unknown;
            return true;
        }

        if (string.Equals(trimmed, "permanent", StringComparison.OrdinalIgnoreCase))
        {
            term = PricingTerm.Permanent;
            return true;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var value) || value <= 0)
        {
            return false;
        }

        if (!string.Equals(parts[1], "year", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parts[1], "years", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        term = PricingTerm.FiniteYears(value);
        return true;
    }
}
