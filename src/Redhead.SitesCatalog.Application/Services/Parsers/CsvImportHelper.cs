using System.Globalization;
using CsvHelper.Configuration;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Shared helper for CSV imports (NO Stream logic here):
/// - CSV content checks (filename/content-type)
/// - delimiter detection from HEADER LINE string
/// - CsvConfiguration factory
/// - header normalization + strict ordered header validation
/// </summary>
public static class CsvImportHelper
{
    public const char CommaDelimiter = ',';
    public const char SemicolonDelimiter = ';';

    public static bool IsCsvExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ImportConstants.CsvExtension;
    }

    public static bool IsCsvContentType(string? contentType)
    {
        return !string.IsNullOrEmpty(contentType)
               && contentType.StartsWith(ImportConstants.CsvContentType, StringComparison.OrdinalIgnoreCase);
    }

    public static CsvConfiguration CreateConfiguration(char delimiter)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            HasHeaderRecord = true,

            // We validate header/rows ourselves with user-facing messages.
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,

            TrimOptions = TrimOptions.Trim,

            // Make header matching resilient to whitespace/quotes/BOM.
            PrepareHeaderForMatch = args => NormalizeHeader(args.Header)
        };
    }

    /// <summary>
    /// Detect delimiter from the header line string (only ',' or ';').
    /// Prefer strict match to the expected header order; otherwise fallback by counting separators.
    /// </summary>
    public static char DetectDelimiterFromHeaderLine(string headerLine, string[] expectedHeaderColumnsInOrder)
    {
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return CommaDelimiter;
        }

        if (expectedHeaderColumnsInOrder is { Length: > 0 })
        {
            if (LooksLikeHeader(headerLine, SemicolonDelimiter, expectedHeaderColumnsInOrder))
            {
                return SemicolonDelimiter;
            }

            if (LooksLikeHeader(headerLine, CommaDelimiter, expectedHeaderColumnsInOrder))
            {
                return CommaDelimiter;
            }
        }

        // Fallback: pick the most frequent delimiter in the header line.
        var commaCount = 0;
        var semicolonCount = 0;

        foreach (var ch in headerLine)
        {
            if (ch == CommaDelimiter)
            {
                commaCount++;
            }
            else if (ch == SemicolonDelimiter)
            {
                semicolonCount++;
            }
        }

        return semicolonCount > commaCount ? SemicolonDelimiter : CommaDelimiter;
    }

    /// <summary>
    /// Strict ordered header validation: required columns must exist in EXACT order in the first N columns.
    /// Extra columns after required are allowed.
    /// </summary>
    public static void ValidateHeaderStrictOrThrow(string[] actualHeader, string[] requiredHeaderOrder)
    {
        if (requiredHeaderOrder is null || requiredHeaderOrder.Length == 0)
        {
            throw new ArgumentException("Required header order must be provided.", nameof(requiredHeaderOrder));
        }

        actualHeader ??= Array.Empty<string>();

        if (actualHeader.Length < requiredHeaderOrder.Length)
        {
            throw new ImportHeaderValidationException(
                $"CSV header is invalid. Missing required columns. Expected in order: {string.Join(", ", requiredHeaderOrder)}. " +
                $"Found {actualHeader.Length} column(s).");
        }

        for (var i = 0; i < requiredHeaderOrder.Length; i++)
        {
            var expected = requiredHeaderOrder[i];
            var actual = NormalizeHeader(actualHeader[i]);

            if (string.IsNullOrEmpty(actual))
            {
                throw new ImportHeaderValidationException(
                    $"CSV header is invalid. Column {i + 1} must be '{expected}'. Found empty.");
            }

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException(
                    $"CSV header is invalid. Column {i + 1} must be '{expected}'. Found: '{actual}'.");
            }
        }
    }

    public static string NormalizeHeader(string? value)
    {
        // Also trims UTF-8 BOM char if it appears as the first character in the first header cell.
        return (value ?? string.Empty)
            .Trim()
            .Trim('"')
            .Trim()
            .TrimStart('\uFEFF');
    }

    private static bool LooksLikeHeader(string headerLine, char delimiter, string[] expectedColumnsInOrder)
    {
        var parts = headerLine.Split(delimiter);

        if (parts.Length < expectedColumnsInOrder.Length)
        {
            return false;
        }

        for (var i = 0; i < expectedColumnsInOrder.Length; i++)
        {
            var actual = NormalizeHeader(parts[i]);
            var expected = NormalizeHeader(expectedColumnsInOrder[i]);

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
