using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Implements quarantine import from CSV (Domain, Reason). Exact match by normalized domain.
/// </summary>
public class QuarantineImportService : IQuarantineImportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QuarantineImportService> _logger;

    private const int BatchSize = 1000;

    private const string HeaderDomain = "Domain";
    private const string HeaderReason = "Reason";

    private sealed record ParsedUpdate(int RowNumber, string? Reason);

    public QuarantineImportService(ApplicationDbContext context, ILogger<QuarantineImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<QuarantineImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Quarantine import started. FileName={FileName}, UserId={UserId}, UserEmail={UserEmail}",
            fileName, userId, userEmail);

        if (!IsCsvFile(fileName, contentType))
        {
            _logger.LogWarning("Quarantine import rejected: not CSV. FileName={FileName}, ContentType={ContentType}", fileName, contentType);
            var unsupportedResult = new QuarantineImportResult
            {
                ErrorsCount = 1,
                Errors = new List<QuarantineImportError> { new() { RowNumber = 0, Message = "Unsupported file type. Use CSV." } }
            };
            return unsupportedResult;
        }

        var result = new QuarantineImportResult();
        var now = DateTime.UtcNow;

        await using var seekableStream = await EnsureSeekableAsync(fileStream, cancellationToken);

        // Phase 1: parse CSV rows and build a per-domain update map (last row wins).
        var updates = new Dictionary<string, ParsedUpdate>(StringComparer.Ordinal);
        var duplicates = 0;

        await foreach (var (rowNumber, domain, reason) in ReadQuarantineRowsAsync(seekableStream, result, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = DomainNormalizer.Normalize(domain);
            if (string.IsNullOrEmpty(normalized))
            {
                result.ErrorsCount++;
                result.Errors.Add(new QuarantineImportError
                {
                    RowNumber = rowNumber,
                    Message = "Domain is required and cannot be empty after normalization."
                });
                continue;
            }

            var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            var update = new ParsedUpdate(rowNumber, trimmedReason);

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

        // Phase 2: load Sites in batches to avoid N+1 queries.
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

        // Phase 3: apply updates and persist once.
        foreach (var (domain, update) in updates)
        {
            if (!sitesByDomain.TryGetValue(domain, out var site))
            {
                result.Unmatched.Add(domain);
                continue;
            }

            site.IsQuarantined = true;
            site.QuarantineReason = update.Reason;
            site.QuarantineUpdatedAtUtc = now;
            site.UpdatedAtUtc = now;
            result.Matched++;
        }

        AddImportLog(result, userId, userEmail, duplicates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Quarantine import completed. Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}, UserId={UserId}",
            result.Matched, result.Unmatched.Count, result.ErrorsCount, duplicates, userId);

        return result;
    }

    private static bool IsCsvFile(string fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (ext == ImportConstants.CsvExtension)
        {
            return true;
        }
        return !string.IsNullOrEmpty(contentType) &&
               contentType.StartsWith(ImportConstants.CsvContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static async IAsyncEnumerable<(int RowNumber, string Domain, string? Reason)> ReadQuarantineRowsAsync(
        Stream stream,
        QuarantineImportResult result,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        });

        if (!await csv.ReadAsync())
        {
            yield break;
        }

        csv.ReadHeader();
        var header = csv.HeaderRecord;
        if (header == null)
        {
            result.ErrorsCount++;
            result.Errors.Add(new QuarantineImportError { RowNumber = 1, Message = "CSV must have a header row." });
            yield break;
        }

        var domainIndex = FindHeaderIndex(header, HeaderDomain);
        var reasonIndex = FindHeaderIndex(header, HeaderReason);
        if (domainIndex < 0)
        {
            result.ErrorsCount++;
            result.Errors.Add(new QuarantineImportError { RowNumber = 1, Message = "Required header 'Domain' not found. Expected headers: Domain, Reason." });
            yield break;
        }

        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var domain = GetField(csv, domainIndex);
            var reason = reasonIndex >= 0 ? GetField(csv, reasonIndex) : null;
            if (string.IsNullOrWhiteSpace(domain) && string.IsNullOrWhiteSpace(reason))
            {
                continue;
            }

            yield return (rowNumber, domain ?? string.Empty, string.IsNullOrWhiteSpace(reason) ? null : reason);
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
        if (index < 0 || index >= csv.HeaderRecord?.Length)
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

    private void AddImportLog(QuarantineImportResult result, string userId, string userEmail, int duplicates)
    {
        var log = new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeQuarantine,
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
