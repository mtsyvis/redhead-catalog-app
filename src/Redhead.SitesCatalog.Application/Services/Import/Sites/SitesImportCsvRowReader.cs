using CsvHelper;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Import.Csv;

namespace Redhead.SitesCatalog.Application.Services.Import.Sites;

internal static class SitesImportCsvRowReader
{
    public static async IAsyncEnumerable<(SitesImportRowDto Row, List<string> RawValues)> ReadRowsAsync(
        CsvReader csv,
        int headerCount,
        SitesImportHeaderInfo headerInfo,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var columnIndexes = BuildColumnIndexes(csv.HeaderRecord ?? Array.Empty<string>());
        var rowNumber = 1;

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var rawRecord = csv.Parser.Record ?? Array.Empty<string>();
            var rawValues = new List<string>(headerCount);
            for (var i = 0; i < headerCount; i++)
            {
                rawValues.Add(i < rawRecord.Length ? rawRecord[i] ?? string.Empty : string.Empty);
            }

            var mappedRow = SitesImportRowMapper.Map(
                columnName => columnIndexes.TryGetValue(columnName, out var index) ? csv.GetField(index)?.Trim() : null,
                rowNumber,
                headerInfo);

            yield return (mappedRow, rawValues);
        }
    }

    private static Dictionary<string, int> BuildColumnIndexes(IReadOnlyList<string> header)
    {
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            var normalized = CsvImportHelper.NormalizeHeader(header[i]);
            if (!string.IsNullOrEmpty(normalized) && !indexes.ContainsKey(normalized))
            {
                indexes[normalized] = i;
            }
        }

        return indexes;
    }
}
