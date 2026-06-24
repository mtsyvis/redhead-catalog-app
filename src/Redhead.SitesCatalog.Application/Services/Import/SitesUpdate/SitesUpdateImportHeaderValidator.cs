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
        var seenPriceTypes = new HashSet<PriceType>();
        var priceColumns = new List<SitesImportPriceColumn>();
        var hasDomain = false;
        var hasDrHeader = false;
        var hasTrafficHeader = false;
        var hasTermHeader = false;
        var updateColumnCount = 0;

        for (var i = 0; i < actualHeader.Length; i++)
        {
            var header = CsvImportHelper.NormalizeHeader(actualHeader[i]);
            if (string.IsNullOrEmpty(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Column {i + 1} is empty.");
            }

            if (SitesImportHeaderParser.TryGetPriceType(header, out var priceType))
            {
                if (!seenPriceTypes.Add(priceType))
                {
                    throw new ImportHeaderValidationException($"CSV header is invalid. Duplicate pricing column: '{header}'.");
                }

                priceColumns.Add(new SitesImportPriceColumn(header, priceType));
                updateColumnCount++;
                continue;
            }

            if (string.Equals(header, ImportConstants.SitesImportColumns.Term, StringComparison.OrdinalIgnoreCase))
            {
                if (!seenHeaders.Add(header))
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
                hasDrHeader |= string.Equals(header, ImportConstants.SitesImportColumns.DR, StringComparison.OrdinalIgnoreCase);
                hasTrafficHeader |= string.Equals(header, ImportConstants.SitesImportColumns.Traffic, StringComparison.OrdinalIgnoreCase);
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

        if (priceColumns.Count > 0 && !hasTermHeader)
        {
            throw new ImportHeaderValidationException("CSV header is invalid. Term column is required when pricing columns are present.");
        }

        return new SitesImportHeaderInfo(priceColumns);
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
