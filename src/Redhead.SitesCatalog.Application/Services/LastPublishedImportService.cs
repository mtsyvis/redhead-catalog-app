using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Implements Last Published Date import from CSV (Domain, LastPublishedDate).
/// Matching rule: exact match by normalized domain string.
/// </summary>
public class LastPublishedImportService : ILastPublishedImportService
{
    private const int BatchSize = 1000;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<LastPublishedImportService> _logger;

    private const string HeaderDomain = "Domain";
    private const string HeaderLastPublishedDate = "LastPublishedDate";

    private static readonly string[] DayFormats =
    {
        "dd.MM.yyyy"
    };

    private static readonly string[] MonthFormats =
    {
        "MMMM yyyy",
        "MMM yyyy"
    };

    private sealed record ParsedUpdate(int RowNumber, DateTime? DateUtc, bool IsMonthOnly);

    public LastPublishedImportService(ApplicationDbContext context, ILogger<LastPublishedImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LastPublishedImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Last Published import started. FileName={FileName}, UserId={UserId}, UserEmail={UserEmail}",
            fileName, userId, userEmail);

        if (!IsCsvFile(fileName, contentType))
        {
            _logger.LogWarning(
                "Last Published import rejected: not CSV. FileName={FileName}, ContentType={ContentType}",
                fileName, contentType);

            return new LastPublishedImportResult
            {
                ErrorsCount = 1,
                Errors = new List<LastPublishedImportError>
                {
                    new() { RowNumber = 0, Message = "Unsupported file type. Use CSV." }
                }
            };
        }

        var result = new LastPublishedImportResult();
        var now = DateTime.UtcNow;

        // Make sure we can read the stream from the beginning (some streams are not seekable).
        await using var seekableStream = await EnsureSeekableAsync(fileStream, cancellationToken);

        // Phase 1: parse the CSV into memory (also lets us detect duplicates).
        var updates = new Dictionary<string, ParsedUpdate>(StringComparer.Ordinal);
        var duplicates = 0;

        await foreach (var (rowNumber, domain, dateRaw) in ReadRowsAsync(seekableStream, result, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = DomainNormalizer.Normalize(domain);
            if (string.IsNullOrEmpty(normalized))
            {
                result.ErrorsCount++;
                result.Errors.Add(new LastPublishedImportError
                {
                    RowNumber = rowNumber,
                    Message = "Domain is required and cannot be empty after normalization."
                });
                continue;
            }

            DateTime? parsedDate = null;
            bool isMonthOnly = false;

            if (!string.IsNullOrWhiteSpace(dateRaw))
            {
                if (!TryParseLastPublishedDate(dateRaw.Trim(), out var parsedValue, out var parsedIsMonthOnly, out var parseError))
                {
                    result.ErrorsCount++;
                    result.Errors.Add(new LastPublishedImportError { RowNumber = rowNumber, Message = parseError });
                    continue;
                }

                parsedDate = parsedValue;
                isMonthOnly = parsedIsMonthOnly;
            }

            var update = new ParsedUpdate(rowNumber, parsedDate, isMonthOnly);

            // If the same domain appears multiple times, keep the last occurrence.
            if (updates.ContainsKey(normalized))
            {
                duplicates++;
                updates[normalized] = update;
            }
            else
            {
                updates.Add(normalized, update);
            }
        }

        // Phase 2: load Sites in batches (avoid N+1 queries).
        var domains = updates.Keys.ToList();
        var sitesByDomain = new Dictionary<string, Site>(StringComparer.Ordinal);

        foreach (var chunk in Chunk(domains, BatchSize))
        {
            var sites = await _context.Sites
                .Where(s => chunk.Contains(s.Domain))
                .ToListAsync(cancellationToken);

            foreach (var site in sites)
            {
                sitesByDomain[site.Domain] = site;
            }
        }

        // Phase 3: apply updates in memory and persist once.
        foreach (var (domain, update) in updates)
        {
            if (!sitesByDomain.TryGetValue(domain, out var site))
            {
                result.Unmatched.Add(domain);
                continue;
            }

            site.LastPublishedDate = update.DateUtc;
            site.LastPublishedDateIsMonthOnly = update.DateUtc.HasValue && update.IsMonthOnly;
            site.UpdatedAtUtc = now;

            result.Matched++;
        }

        AddImportLog(result, userId, userEmail, duplicates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Last Published import completed. Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}, UserId={UserId}",
            result.Matched, result.Unmatched.Count, result.ErrorsCount, duplicates, userId);

        return result;
    }

    /// <summary>
    /// Tries to parse a LastPublishedDate value.
    /// Supported formats:
    /// - Full date (day precision): "DD.MM.YYYY"
    /// - Month + year (month precision, English, case-insensitive): "January 2026", "Jan 2026"
    /// For month precision we store the first day of the month (YYYY-MM-01) and return IsMonthOnly=true,
    /// so the caller can persist the precision separately.
    /// </summary>
    internal static bool TryParseLastPublishedDate(
        string value,
        out DateTime dateUtc,
        out bool isMonthOnly,
        out string errorMessage)
    {
        dateUtc = default;
        isMonthOnly = false;
        errorMessage = string.Empty;

        // Day precision: only DD.MM.YYYY.
        if (DateTime.TryParseExact(value, DayFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayDate))
        {
            dateUtc = DateTime.SpecifyKind(dayDate.Date, DateTimeKind.Utc);
            isMonthOnly = false;
            return true;
        }

        // Month precision: English month name + year, e.g. "January 2026", "Jan 2026" (case-insensitive).
        if (DateTime.TryParseExact(value, MonthFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
        {
            dateUtc = DateTime.SpecifyKind(new DateTime(monthDate.Year, monthDate.Month, 1), DateTimeKind.Utc);
            isMonthOnly = true;
            return true;
        }

        errorMessage =
            "LastPublishedDate could not be parsed. Use a full date 'DD.MM.YYYY' or month+year like 'January 2026' or 'Jan 2026'.";
        return false;
    }

    private static bool IsCsvFile(string fileName, string? contentType)
    {
        return CsvImportHelper.IsCsvExtension(fileName)
            || CsvImportHelper.IsCsvContentType(contentType);
    }

    private static async Task<Stream> EnsureSeekableAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
            return stream;
        }

        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private static async IAsyncEnumerable<(int RowNumber, string Domain, string? LastPublishedDate)> ReadRowsAsync(
        Stream stream,
        LastPublishedImportResult result,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var delimiter = CsvImportHelper.GetDelimiter(stream, new[] { HeaderDomain, HeaderLastPublishedDate });

        using var streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csvReader = new CsvReader(streamReader, CsvImportHelper.CreateConfiguration(delimiter));

        if (!await csvReader.ReadAsync())
        {
            yield break;
        }

        csvReader.ReadHeader();
        var header = csvReader.HeaderRecord;
        if (header == null)
        {
            result.ErrorsCount++;
            result.Errors.Add(new LastPublishedImportError
            {
                RowNumber = 1,
                Message = "CSV must have a header row."
            });
            yield break;
        }

        var domainIndex = FindHeaderIndex(header, HeaderDomain);
        var dateIndex = FindHeaderIndex(header, HeaderLastPublishedDate);

        if (domainIndex < 0)
        {
            result.ErrorsCount++;
            result.Errors.Add(new LastPublishedImportError
            {
                RowNumber = 1,
                Message = "Required header 'Domain' not found. Expected headers: Domain, LastPublishedDate."
            });
            yield break;
        }

        if (dateIndex < 0)
        {
            result.ErrorsCount++;
            result.Errors.Add(new LastPublishedImportError
            {
                RowNumber = 1,
                Message = "Required header 'LastPublishedDate' not found. Expected headers: Domain, LastPublishedDate."
            });
            yield break;
        }

        var rowNumber = 1;
        while (await csvReader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var domain = GetField(csvReader, domainIndex);
            var dateRaw = GetField(csvReader, dateIndex);

            // Skip completely empty rows.
            if (string.IsNullOrWhiteSpace(domain) && string.IsNullOrWhiteSpace(dateRaw))
            {
                continue;
            }

            yield return (rowNumber, domain ?? string.Empty, string.IsNullOrWhiteSpace(dateRaw) ? null : dateRaw);
        }
    }

    private static int FindHeaderIndex(string[] header, string name)
    {
        for (var i = 0; i < header.Length; i++)
        {
            if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private static string? GetField(CsvReader csv, int index)
    {
        if (index < 0 || csv.HeaderRecord == null || index >= csv.HeaderRecord.Length)
        {
            return null;
        }

        try
        {
            return csv.GetField(index);
        }
        catch
        {
            return null;
        }
    }

    private void AddImportLog(LastPublishedImportResult result, string userId, string userEmail, int duplicates)
    {
        var log = new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeLastPublished,
            TimestampUtc = DateTime.UtcNow,
            Inserted = 0,
            Duplicates = duplicates,
            Matched = result.Matched,
            Unmatched = result.Unmatched.Count,
            ErrorsCount = result.ErrorsCount
        };

        _context.ImportLogs.Add(log);
    }
}
