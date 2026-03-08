using System.Text;
using System.Globalization;
using CsvHelper.Configuration;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Shared helper for CSV imports: delimiter detection and CsvConfiguration.
/// </summary>
public static class CsvImportHelper
{
    private const string CommaDelimiter = ",";
    private const string SemicolonDelimiter = ";";

    /// <summary>
    /// Detects the CSV delimiter from the first line of the stream ("," or ";").
    /// Stream position is restored to its original value on exit.
    /// </summary>
    public static string GetDelimiter(Stream stream, string[]? expectedHeaderColumns = null)
    {
        if (!stream.CanSeek)
        {
            return CommaDelimiter;
        }

        var originalPosition = stream.Position;

        try
        {
            return DetectDelimiterFromFirstLine(stream, expectedHeaderColumns);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// Creates a CsvConfiguration with shared options and the specified delimiter.
    /// </summary>
    public static CsvConfiguration CreateConfiguration(string delimiter)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
        };
    }

    private static string DetectDelimiterFromFirstLine(Stream stream, string[]? expectedHeaderColumns)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        var headerLine = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return CommaDelimiter;
        }

        if (expectedHeaderColumns is { Length: > 0 })
        {
            if (LooksLikeHeader(headerLine, SemicolonDelimiter, expectedHeaderColumns))
            {
                return SemicolonDelimiter;
            }

            if (LooksLikeHeader(headerLine, CommaDelimiter, expectedHeaderColumns))
            {
                return CommaDelimiter;
            }
        }

        var commaCount = headerLine.Count(c => c == CommaDelimiter[0]);
        var semicolonCount = headerLine.Count(c => c == SemicolonDelimiter[0]);

        if (semicolonCount > commaCount)
        {
            return SemicolonDelimiter;
        }

        return CommaDelimiter;
    }

    /// <summary>
    /// Returns true when the file name has the configured CSV extension.
    /// </summary>
    public static bool IsCsvExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ImportConstants.CsvExtension;
    }

    /// <summary>
    /// Returns true when the content-type looks like a CSV content-type.
    /// </summary>
    public static bool IsCsvContentType(string? contentType)
    {
        return !string.IsNullOrEmpty(contentType)
            && contentType.StartsWith(ImportConstants.CsvContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeHeader(string headerLine, string delimiter, string[] requiredColumns)
    {
        var parts = headerLine
            .Split(delimiter)
            .Select(p => p.Trim().Trim('"'))
            .ToArray();

        if (parts.Length < requiredColumns.Length)
        {
            return false;
        }

        for (var i = 0; i < requiredColumns.Length; i++)
        {
            if (!string.Equals(parts[i], requiredColumns[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
