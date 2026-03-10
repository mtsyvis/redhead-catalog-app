using System.Text;
using CsvHelper;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// CSV import session:
/// - Requires seekable stream (controller/services should provide MemoryStream).
/// - Validates UTF-8 strictly BEFORE delimiter detection.
/// - Detects delimiter from header line (',' or ';').
/// - Creates CsvReader and validates header in strict required order.
/// </summary>
public sealed class CsvImportSession : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly StreamReader _reader;

    public CsvReader Csv { get; }
    public string[] Header { get; }
    public char Delimiter { get; }

    private CsvImportSession(StreamReader reader, CsvReader csv, string[] header, char delimiter)
    {
        _reader = reader;
        Csv = csv;
        Header = header;
        Delimiter = delimiter;
    }

    public static async Task<CsvImportSession> OpenAsync(
        Stream stream,
        string[] expectedHeaderColumnsForDelimiterDetection,
        string[] requiredHeadersInStrictOrder,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        ArgumentNullException.ThrowIfNull(expectedHeaderColumnsForDelimiterDetection);

        ArgumentNullException.ThrowIfNull(requiredHeadersInStrictOrder);

        if (!stream.CanSeek)
        {
            throw new ImportHeaderValidationException("CSV stream must be seekable. Please re-upload the file.");
        }

        // 1) Strict UTF-8 validation + read header line (for delimiter detection)
        stream.Position = 0;
        var headerLine = ReadHeaderLineStrictUtf8OrThrow(stream);

        // 2) Detect delimiter from header line (only ',' or ';')
        var delimiter = CsvImportHelper.DetectDelimiterFromHeaderLine(headerLine, expectedHeaderColumnsForDelimiterDetection);

        // 3) Create CsvReader and validate header in strict order
        stream.Position = 0;

        StreamReader? reader = null;
        CsvReader? csv = null;

        try
        {
            // detectEncodingFromByteOrderMarks = false:
            // - We already validated UTF-8 via the probe reader.
            // - UTF-8 BOM is handled by header normalization (TrimStart('\uFEFF')).
            reader = new StreamReader(stream, Utf8Strict, detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);

            var config = CsvImportHelper.CreateConfiguration(delimiter);
            csv = new CsvReader(reader, config);

            ct.ThrowIfCancellationRequested();

            // CsvHelper requires moving to the first record before ReadHeader()
            if (!await csv.ReadAsync().ConfigureAwait(false))
            {
                throw new ImportHeaderValidationException("CSV file is empty.");
            }

            csv.ReadHeader();

            var header = csv.HeaderRecord ?? Array.Empty<string>();
            if (header.Length == 0)
            {
                throw new ImportHeaderValidationException("CSV header row is missing.");
            }

            CsvImportHelper.ValidateHeaderStrictOrThrow(header, requiredHeadersInStrictOrder);

            return new CsvImportSession(reader, csv, header, delimiter);
        }
        catch (DecoderFallbackException)
        {
            csv?.Dispose();
            reader?.Dispose();
            throw new ImportHeaderValidationException("CSV must be UTF-8 encoded.");
        }
        catch
        {
            csv?.Dispose();
            reader?.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        Csv.Dispose();
        _reader.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string ReadHeaderLineStrictUtf8OrThrow(Stream stream)
    {
        try
        {
            using var probe = new StreamReader(
                stream,
                Utf8Strict,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true);

            _ = probe.Peek();

            if (!string.Equals(probe.CurrentEncoding.WebName, "utf-8", StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException("CSV must be UTF-8 encoded.");
            }

            var headerLine = probe.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                throw new ImportHeaderValidationException("CSV file is empty or header row is missing.");
            }

            // Important: catches UTF-16/UTF-32 without BOM like D\0o\0m\0a\0i\0n\0
            if (headerLine.IndexOf('\0') >= 0)
            {
                throw new ImportHeaderValidationException("CSV must be UTF-8 encoded.");
            }

            return headerLine;
        }
        catch (DecoderFallbackException)
        {
            throw new ImportHeaderValidationException("CSV must be UTF-8 encoded.");
        }
    }
}
