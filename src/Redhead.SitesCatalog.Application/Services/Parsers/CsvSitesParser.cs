using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Parses CSV files for sites import. Isolates CsvHelper usage.
/// </summary>
public class CsvSitesParser : IImportFileParser
{
    public bool CanParse(string fileName, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return IsCsvExtension(fileName) || IsCsvContentType(contentType);
    }

    public async IAsyncEnumerable<SitesImportRowDto> ReadRowsAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureStreamReadyForRead(stream);

        // Excel / regional CSV exports often use ';' instead of ','
        var delimiter = DetectDelimiter(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var csv = CreateCsvReader(reader, delimiter);

        var columnIndex = await ReadAndValidateHeaderAsync(csv, cancellationToken);

        // Row numbers are 1-based; header row is row 1.
        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            yield return SitesImportRowMapper.Map(
                name => GetFieldByColumn(csv, columnIndex, name),
                rowNumber);
        }
    }

    private static bool IsCsvExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ImportConstants.CsvExtension;
    }

    private static bool IsCsvContentType(string? contentType)
    {
        // Browsers/OS can send different CSV content-types; we primarily rely on extension.
        return !string.IsNullOrEmpty(contentType)
            && contentType.StartsWith(ImportConstants.CsvContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static CsvReader CreateCsvReader(StreamReader reader, string delimiter)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
        };

        return new CsvReader(reader, config);
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadAndValidateHeaderAsync(CsvReader csv, CancellationToken cancellationToken)
    {
        try
        {
            // CsvHelper requires moving to the first record before calling ReadHeader().
            if (!await csv.ReadAsync())
            {
                throw new ImportHeaderValidationException("The file is empty. Upload a CSV file with a header row and data.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            csv.ReadHeader();
        }
        catch (ReaderException ex)
        {
            throw new ImportHeaderValidationException(
                ex.Message.Contains("No header", StringComparison.OrdinalIgnoreCase)
                    ? "The file is empty or has no header row. Ensure the CSV has a header row with column names."
                    : ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ImportHeaderValidationException(ex.Message);
        }

        var header = csv.HeaderRecord ?? Array.Empty<string>();
        var validationError = SitesImportHeaderValidator.Validate(header);
        if (validationError != null)
        {
            throw new ImportHeaderValidationException(validationError);
        }

        return BuildColumnIndex(header);
    }

    private static void EnsureStreamReadyForRead(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return;
        }

        if (stream.Length == 0)
        {
            throw new ImportHeaderValidationException("The file is empty. Upload a CSV file with a header row and data.");
        }

        if (stream.Position != 0)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
    }

    private static Dictionary<string, int> BuildColumnIndex(string[] header)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
        {
            var key = (header[i] ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(key))
            {
                index[key] = i;
            }
        }

        return index;
    }

    private static string? GetFieldByColumn(CsvReader csv, IReadOnlyDictionary<string, int> columnIndex, string columnName)
    {
        return columnIndex.TryGetValue(columnName, out var i) ? csv.GetField(i)?.Trim() : null;
    }

    private static string DetectDelimiter(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return ",";
        }

        var pos = stream.Position;
        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return ",";
            }

            // Prefer ';' if it matches the required header better
            if (LooksLikeHeader(headerLine, ";"))
            {
                return ";";
            }

            if (LooksLikeHeader(headerLine, ","))
            {
                return ",";
            }

            // Fallback: choose the delimiter that appears more.
            var commaCount = headerLine.Count(c => c == ',');
            var semicolonCount = headerLine.Count(c => c == ';');
            return semicolonCount > commaCount ? ";" : ",";
        }
        finally
        {
            stream.Seek(pos, SeekOrigin.Begin);
        }
    }

    private static bool LooksLikeHeader(string headerLine, string delimiter)
    {
        var required = ImportConstants.SitesImportRequiredColumnOrder;
        var parts = headerLine
            .Split(delimiter)
            .Select(p => p.Trim().Trim('"'))
            .ToArray();

        if (parts.Length < required.Length)
        {
            return false;
        }

        for (var i = 0; i < required.Length; i++)
        {
            if (!string.Equals(parts[i], required[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
