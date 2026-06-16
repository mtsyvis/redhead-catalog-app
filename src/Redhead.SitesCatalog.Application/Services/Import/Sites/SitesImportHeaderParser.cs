using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Application.Services.Import.Csv;
using Redhead.SitesCatalog.Application.Services.Import.ValueParsers;

namespace Redhead.SitesCatalog.Application.Services.Import.Sites;

internal sealed record SitesImportHeaderInfo(
    IReadOnlyList<SitesImportPriceColumn> PriceColumns,
    IReadOnlyList<SitesImportAvailabilityColumn> AvailabilityColumns);

internal sealed record SitesImportPriceColumn(
    string Header,
    PriceType PriceType,
    PricingTerm Term);

internal sealed record SitesImportAvailabilityColumn(
    string Header,
    PriceType ServiceType);

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

    public static SitesImportHeaderInfo Parse(string[] actualHeader)
    {
        actualHeader ??= Array.Empty<string>();

        if (actualHeader.Length == 0)
        {
            throw new ImportHeaderValidationException("CSV header row is missing.");
        }

        var seenBaseHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPriceTerms = new HashSet<(PriceType PriceType, string TermKey)>();
        var seenAvailabilityTypes = new HashSet<PriceType>();
        var priceColumns = new List<SitesImportPriceColumn>();
        var availabilityColumns = new List<SitesImportAvailabilityColumn>();

        for (var i = 0; i < actualHeader.Length; i++)
        {
            var header = CsvImportHelper.NormalizeHeader(actualHeader[i]);
            if (string.IsNullOrEmpty(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Column {i + 1} is empty.");
            }

            if (TryParsePriceHeader(header, out var priceType, out var term, out var invalidTerm))
            {
                if (!seenPriceTerms.Add((priceType, term.TermKey)))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate price column for {priceType} and term '{term.TermKey}'.");
                }

                priceColumns.Add(new SitesImportPriceColumn(header, priceType, term));
                continue;
            }

            if (invalidTerm)
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Invalid term header: '{header}'.");
            }

            if (TryGetAvailabilityServiceType(header, out var availabilityServiceType))
            {
                if (!seenAvailabilityTypes.Add(availabilityServiceType))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate availability column for {availabilityServiceType}.");
                }

                availabilityColumns.Add(new SitesImportAvailabilityColumn(header, availabilityServiceType));
                continue;
            }

            if (IsMainAvailabilityHeader(header))
            {
                throw new ImportHeaderValidationException("CSV header is invalid. Main pricing must not include an availability column.");
            }

            if (header.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
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

        return new SitesImportHeaderInfo(priceColumns, availabilityColumns);
    }

    internal static bool TryParsePriceHeader(
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

    internal static bool TryGetAvailabilityServiceType(string header, out PriceType serviceType)
        => AvailabilityHeaders.TryGetValue(header, out serviceType);

    internal static bool IsMainAvailabilityHeader(string header)
        => string.Equals(header, $"{ImportConstants.SitesImportColumns.PriceUsd}Availability", StringComparison.OrdinalIgnoreCase);

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
