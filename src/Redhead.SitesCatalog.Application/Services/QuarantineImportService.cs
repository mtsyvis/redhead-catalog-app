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
/// Imports quarantine updates from CSV with strict header order:
/// Domain, Reason
/// Matching rule: exact match by normalized domain.
/// Processing model:
/// 1) Parse rows and build per-domain update map (last row wins)
/// 2) Load Sites in batches
/// 3) Apply updates and persist once
/// </summary>
public sealed class QuarantineImportService : IQuarantineImportService
{
    private const int BatchSize = 1000;

    private const string HeaderDomain = "Domain";
    private const string HeaderReason = "Reason";

    private static readonly string[] RequiredHeaderOrder =
    {
        HeaderDomain,
        HeaderReason
    };

    private readonly ApplicationDbContext _context;
    private readonly ILogger<QuarantineImportService> _logger;

    private sealed record ParsedUpdate(string? Reason);

    public QuarantineImportService(
        ApplicationDbContext context,
        ILogger<QuarantineImportService> logger)
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
            fileName,
            userId,
            userEmail);

        if (!IsCsvFile(fileName, contentType))
        {
            _logger.LogWarning(
                "Quarantine import rejected: not CSV. FileName={FileName}, ContentType={ContentType}",
                fileName,
                contentType);

            return new QuarantineImportResult
            {
                ErrorsCount = 1,
                Errors =
                {
                    new QuarantineImportError
                    {
                        RowNumber = 0,
                        Message = "Unsupported file type. Use CSV."
                    }
                }
            };
        }

        var result = new QuarantineImportResult();
        var now = DateTime.UtcNow;

        // Phase 1: parse CSV rows and build per-domain update map (last row wins)
        var updates = new Dictionary<string, ParsedUpdate>(StringComparer.Ordinal);
        var duplicates = 0;

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: RequiredHeaderOrder,
                         requiredHeadersInStrictOrder: RequiredHeaderOrder,
                         ct: cancellationToken))
        {
            await foreach (var (rowNumber, domain, rawReason) in ReadRowsAsync(session.Csv, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalizedDomain = DomainNormalizer.Normalize(domain);
                if (string.IsNullOrEmpty(normalizedDomain))
                {
                    result.ErrorsCount++;
                    result.Errors.Add(new QuarantineImportError
                    {
                        RowNumber = rowNumber,
                        Message = "Domain is required and cannot be empty after normalization."
                    });
                    continue;
                }

                var normalizedReason = string.IsNullOrWhiteSpace(rawReason)
                    ? null
                    : rawReason.Trim();

                var update = new ParsedUpdate(normalizedReason);

                if (updates.ContainsKey(normalizedDomain))
                {
                    duplicates++;
                    updates[normalizedDomain] = update;
                }
                else
                {
                    updates.Add(normalizedDomain, update);
                }
            }
        }

        // Phase 2: load sites in batches
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

        // Phase 3: apply updates and persist once
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
            result.Matched,
            result.Unmatched.Count,
            result.ErrorsCount,
            duplicates,
            userId);

        return result;
    }

    private static async IAsyncEnumerable<(int RowNumber, string Domain, string? RawReason)> ReadRowsAsync(
        CsvHelper.CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Row 1 = header
        var rowNumber = 1;

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var domain = csv.GetField(0)?.Trim();
            var rawReason = csv.GetField(1)?.Trim();

            // Skip fully empty rows
            if (string.IsNullOrWhiteSpace(domain) && string.IsNullOrWhiteSpace(rawReason))
            {
                continue;
            }

            yield return (
                rowNumber,
                domain ?? string.Empty,
                string.IsNullOrWhiteSpace(rawReason) ? null : rawReason);
        }
    }

    private static bool IsCsvFile(string fileName, string? contentType)
        => CsvImportHelper.IsCsvExtension(fileName) || CsvImportHelper.IsCsvContentType(contentType);

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private void AddImportLog(
        QuarantineImportResult result,
        string userId,
        string userEmail,
        int duplicates)
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
