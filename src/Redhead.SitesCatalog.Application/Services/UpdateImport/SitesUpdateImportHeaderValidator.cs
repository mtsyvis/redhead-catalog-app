using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.UpdateImport;

internal static class SitesUpdateImportHeaderValidator
{
    public static void ValidateOrThrow(string[] actualHeader)
    {
        actualHeader ??= Array.Empty<string>();

        if (actualHeader.Length == 0)
        {
            throw new ImportHeaderValidationException("CSV header row is missing.");
        }

        var allowedHeaders = ImportConstants.SitesImportRequiredColumnOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasDomain = false;
        var updateColumnCount = 0;

        for (var i = 0; i < actualHeader.Length; i++)
        {
            var header = CsvImportHelper.NormalizeHeader(actualHeader[i]);
            if (string.IsNullOrEmpty(header))
            {
                throw new ImportHeaderValidationException($"CSV header is invalid. Column {i + 1} is empty.");
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
