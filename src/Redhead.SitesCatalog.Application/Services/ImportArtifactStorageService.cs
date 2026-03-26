using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class ImportArtifactStorageService : IImportArtifactStorageService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;

    public ImportArtifactStorageService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public ImportArtifactHandle StoreInvalidRows(string importType, InvalidRowsImportArtifactPayload payload)
    {
        var token = Guid.NewGuid().ToString("N");
        var fileName = BuildFileName(importType, "invalid-rows");
        var artifact = new CachedImportArtifact
        {
            Kind = ImportArtifactKinds.InvalidRows,
            InvalidRowsPayload = payload,
            FileName = fileName
        };

        _cache.Set(token, artifact, DefaultTtl);

        return new ImportArtifactHandle
        {
            Token = token,
            FileName = fileName
        };
    }

    public ImportArtifactHandle StoreUnmatchedRows(string importType, UnmatchedRowsImportArtifactPayload payload)
    {
        var token = Guid.NewGuid().ToString("N");
        var fileName = BuildFileName(importType, "unmatched-rows");
        var artifact = new CachedImportArtifact
        {
            Kind = ImportArtifactKinds.UnmatchedRows,
            UnmatchedRowsPayload = payload,
            FileName = fileName
        };

        _cache.Set(token, artifact, DefaultTtl);

        return new ImportArtifactHandle
        {
            Token = token,
            FileName = fileName
        };
    }

    public ImportArtifactDownload? GetCsvDownload(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!_cache.TryGetValue(token, out CachedImportArtifact? artifact) || artifact is null)
        {
            return null;
        }

        byte[]? csvContent = artifact.Kind switch
        {
            ImportArtifactKinds.InvalidRows when artifact.InvalidRowsPayload is not null => BuildInvalidRowsCsv(artifact.InvalidRowsPayload),
            ImportArtifactKinds.UnmatchedRows when artifact.UnmatchedRowsPayload is not null => BuildUnmatchedRowsCsv(artifact.UnmatchedRowsPayload),
            _ => null
        };

        if (csvContent is null)
        {
            return null;
        }

        return new ImportArtifactDownload
        {
            FileName = artifact.FileName,
            ContentType = "text/csv",
            Content = csvContent
        };
    }

    private static string BuildFileName(string importType, string suffix)
    {
        var normalizedImportType = string.IsNullOrWhiteSpace(importType)
            ? "import"
            : importType.Trim().ToLowerInvariant();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"{normalizedImportType}-{suffix}-{timestamp}.csv";
    }

    private static byte[] BuildInvalidRowsCsv(InvalidRowsImportArtifactPayload payload)
    {
        var sb = new StringBuilder();
        var exportHeaders = payload.Headers
            .Concat(["Source Row Number", "Error Details"])
            .ToArray();

        AppendCsvRow(sb, exportHeaders);

        foreach (var row in payload.Rows)
        {
            var values = new List<string>(payload.Headers.Length + 2);
            values.AddRange(row.RawValues);
            values.Add(row.SourceRowNumber.ToString());
            values.Add(string.Join("; ", row.Errors));
            AppendCsvRow(sb, values);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildUnmatchedRowsCsv(UnmatchedRowsImportArtifactPayload payload)
    {
        var sb = new StringBuilder();
        var exportHeaders = payload.Headers
            .Concat(["Source Row Number"])
            .ToArray();

        AppendCsvRow(sb, exportHeaders);

        foreach (var row in payload.Rows)
        {
            var values = new List<string>(payload.Headers.Length + 1);
            values.AddRange(row.RawValues);
            values.Add(row.SourceRowNumber.ToString());
            AppendCsvRow(sb, values);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void AppendCsvRow(StringBuilder sb, IEnumerable<string> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append(EscapeCsv(value ?? string.Empty));
        }

        sb.AppendLine();
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed class CachedImportArtifact
    {
        public string Kind { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public InvalidRowsImportArtifactPayload? InvalidRowsPayload { get; init; }
        public UnmatchedRowsImportArtifactPayload? UnmatchedRowsPayload { get; init; }
    }
}
