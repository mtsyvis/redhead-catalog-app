using Redhead.SitesCatalog.Application.Services.Import.Csv;
using Redhead.SitesCatalog.Application.Services.Import.Sites;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.Import.SitesUpdate;

internal static class SitesUpdateImportHeaderValidator
{
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

        var allowedHeaders = ImportConstants.SitesUpdateImportBaseColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPriceTerms = new HashSet<(PriceType PriceType, string TermKey)>();
        var seenAvailabilityTypes = new HashSet<PriceType>();
        var priceColumns = new List<SitesImportPriceColumn>();
        var availabilityColumns = new List<SitesImportAvailabilityColumn>();
        var hasDomain = false;
        var updateColumnCount = 0;

        for (var i = 0; i < actualHeader.Length; i++)
        {
            var header = CsvImportHelper.NormalizeHeader(actualHeader[i]);
            if (string.IsNullOrEmpty(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Column {i + 1} is empty.");
            }

            if (SitesImportHeaderParser.TryParsePriceHeader(header, out var priceType, out var term, out var invalidTerm))
            {
                if (!seenPriceTerms.Add((priceType, term.TermKey)))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate price column for {priceType} and term '{term.TermKey}'.");
                }

                priceColumns.Add(new SitesImportPriceColumn(header, priceType, term));
                updateColumnCount++;
                continue;
            }

            if (invalidTerm)
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Invalid term header: '{header}'.");
            }

            if (SitesImportHeaderParser.TryGetAvailabilityServiceType(header, out var availabilityServiceType))
            {
                if (!seenAvailabilityTypes.Add(availabilityServiceType))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate availability column for {availabilityServiceType}.");
                }

                availabilityColumns.Add(new SitesImportAvailabilityColumn(header, availabilityServiceType));
                updateColumnCount++;
                continue;
            }

            if (SitesImportHeaderParser.IsMainAvailabilityHeader(header))
            {
                throw new ImportHeaderValidationException("CSV header is invalid. Main pricing must not include an availability column.");
            }

            if (header.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Unknown pricing column: '{header}'.");
            }

            if (!allowedHeaders.Contains(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Unknown column: '{header}'.");
            }

            if (!seenHeaders.Add(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate column: '{header}'.");
            }

            if (string.Equals(header, ImportConstants.SitesImportColumns.Domain, StringComparison.OrdinalIgnoreCase))
            {
                hasDomain = true;
            }
            else
            {
                updateColumnCount++;
            }
        }

        if (!hasDomain)
        {
            throw new ImportHeaderValidationException("CSV header is invalid. Domain column is required.");
        }

        if (updateColumnCount == 0)
        {
            throw new ImportHeaderValidationException("CSV header is invalid. At least one update column besides Domain is required.");
        }

        return new SitesImportHeaderInfo(priceColumns, availabilityColumns);
    }

    public static HashSet<string> BuildPresentColumnSet(IReadOnlyList<string> header)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawHeader in header)
        {
            columns.Add(CsvImportHelper.NormalizeHeader(rawHeader));
        }

        return columns;
    }
}
