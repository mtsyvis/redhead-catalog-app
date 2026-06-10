using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

internal sealed record SitesInsertImportHeaderInfo(
    IReadOnlyList<SitesInsertImportPriceColumn> PriceColumns,
    IReadOnlyList<SitesInsertImportAvailabilityColumn> AvailabilityColumns);

internal sealed record SitesInsertImportPriceColumn(
    string Header,
    PriceType PriceType,
    PricingTerm Term);

internal sealed record SitesInsertImportAvailabilityColumn(
    string Header,
    PriceType ServiceType);

internal static class SitesInsertImportHeaderParser
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

    private static readonly IReadOnlyDictionary<string, PriceType> AvailabilityHeaders =
        new Dictionary<string, PriceType>(StringComparer.OrdinalIgnoreCase)
        {
            [ImportConstants.SitesImportColumns.PriceCasinoAvailability] = PriceType.Casino,
            [ImportConstants.SitesImportColumns.PriceCryptoAvailability] = PriceType.Crypto,
            [ImportConstants.SitesImportColumns.PriceLinkInsertAvailability] = PriceType.LinkInsertion,
            [ImportConstants.SitesImportColumns.PriceLinkInsertCasinoAvailability] = PriceType.LinkInsertionCasino,
            [ImportConstants.SitesImportColumns.PriceDatingAvailability] = PriceType.Dating
        };

    public static void ValidateOrThrow(string[] actualHeader)
    {
        _ = Parse(actualHeader);
    }

    public static SitesInsertImportHeaderInfo Parse(string[] actualHeader)
    {
        CsvImportHelper.ValidateHeaderStrictOrThrow(actualHeader, ImportConstants.SitesImportRequiredColumnOrder);

        var seenHeaders = new HashSet<string>(StringComparer.Ordinal);
        var seenPriceTerms = new HashSet<(PriceType PriceType, string TermKey)>();
        var seenAvailabilityTypes = new HashSet<PriceType>();
        var priceColumns = new List<SitesInsertImportPriceColumn>();
        var availabilityColumns = new List<SitesInsertImportAvailabilityColumn>();

        for (var i = 0; i < actualHeader.Length; i++)
        {
            var header = CsvImportHelper.NormalizeHeader(actualHeader[i]);
            if (string.IsNullOrEmpty(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Column {i + 1} is empty.");
            }

            if (!seenHeaders.Add(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate column: '{header}'.");
            }

            if (i < ImportConstants.SitesImportRequiredColumnOrder.Length)
            {
                continue;
            }

            if (ImportConstants.SitesImportRequiredColumnOrder.Contains(header, StringComparer.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate column: '{header}'.");
            }

            if (ImportConstants.SitesImportLegacyPricingHeaders.Contains(header, StringComparer.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Legacy pricing column is not supported: '{header}'.");
            }

            if (AvailabilityHeaders.TryGetValue(header, out var availabilityServiceType))
            {
                if (!seenAvailabilityTypes.Add(availabilityServiceType))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate availability column for {availabilityServiceType}.");
                }

                availabilityColumns.Add(new SitesInsertImportAvailabilityColumn(header, availabilityServiceType));
                continue;
            }

            if (string.Equals(header, $"{ImportConstants.SitesImportColumns.PriceUsd}Availability", StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException("CSV header is invalid. Main pricing must not include an availability column.");
            }

            if (TryParsePriceHeader(header, out var priceType, out var term, out var invalidTerm))
            {
                if (!seenPriceTerms.Add((priceType, term.TermKey)))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate price column for {priceType} and term '{term.TermKey}'.");
                }

                priceColumns.Add(new SitesInsertImportPriceColumn(header, priceType, term));
                continue;
            }

            if (invalidTerm)
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Invalid term header: '{header}'.");
            }

            if (header.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Unknown pricing column: '{header}'.");
            }

            throw new ImportHeaderValidationException($"CSV header is invalid. Unknown column: '{header}'.");
        }

        return new SitesInsertImportHeaderInfo(priceColumns, availabilityColumns);
    }

    private static bool TryParsePriceHeader(
        string header,
        out PriceType priceType,
        out PricingTerm term,
        out bool invalidTerm)
    {
        priceType = default;
        term = PricingTerm.Unknown;
        invalidTerm = false;

        var openBracketIndex = header.IndexOf('[', StringComparison.Ordinal);
        var closeBracketIndex = header.LastIndexOf(']');
        if (openBracketIndex < 0 && closeBracketIndex < 0)
        {
            return false;
        }

        if (openBracketIndex <= 0 || closeBracketIndex != header.Length - 1 || closeBracketIndex <= openBracketIndex)
        {
            invalidTerm = header.StartsWith("Price", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        var priceHeader = header[..openBracketIndex].Trim();
        if (!PriceHeaders.TryGetValue(priceHeader, out priceType))
        {
            return false;
        }

        var rawTerm = header[(openBracketIndex + 1)..closeBracketIndex];
        if (!TryParseTerm(rawTerm, out term))
        {
            invalidTerm = true;
            return false;
        }

        return true;
    }

    private static bool TryParseTerm(string rawTerm, out PricingTerm term)
    {
        term = PricingTerm.Unknown;

        var trimmed = rawTerm.Trim();
        if (string.Equals(trimmed, "unknown term", StringComparison.OrdinalIgnoreCase))
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
