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
    private readonly IImportArtifactStorageService _importArtifactStorageService;

    private sealed record ParsedUpdate(string? Reason);

    public QuarantineImportService(
        ApplicationDbContext context,
        ILogger<QuarantineImportService> logger,
        IImportArtifactStorageService importArtifactStorageService)
    {
        _context = context;
        _logger = logger;
        _importArtifactStorageService = importArtifactStorageService;
    }

    public async Task<SitesUpdateImportResult> ImportAsync(
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

            return new SitesUpdateImportResult
            {
                ErrorsCount = 1,
                Errors =
                {
                    new SitesUpdateImportError
                    {
                        RowNumber = 0,
                        Message = "Unsupported file type. Use CSV."
                    }
                }
            };
        }

        var result = new SitesUpdateImportResult();
        var now = DateTime.UtcNow;
        var invalidRowsPayload = new InvalidRowsImportArtifactPayload();
        var unmatchedRowsPayload = new UnmatchedRowsImportArtifactPayload();
        var validRowsByDomain = new Dictionary<string, List<UnmatchedImportRowRecord>>(StringComparer.Ordinal);
        var duplicateInputRowsCount = 0;
        var duplicateDomainOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateDomainsInOrder = new List<string>();

        // Phase 1: parse CSV rows and build per-domain update map (last row wins)
        var updates = new Dictionary<string, ParsedUpdate>(StringComparer.Ordinal);

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: RequiredHeaderOrder,
                         requiredHeadersInStrictOrder: RequiredHeaderOrder,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();
            unmatchedRowsPayload.Headers = session.Header.ToArray();

            await foreach (var (rowNumber, domain, rawReason, rawValues) in ReadRowsAsync(
                               session.Csv,
                               session.Header.Length,
                               cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalizedDomain = DomainNormalizer.Normalize(domain);
                if (string.IsNullOrEmpty(normalizedDomain))
                {
                    result.ErrorsCount++;
                    result.Errors.Add(new SitesUpdateImportError
                    {
                        RowNumber = rowNumber,
                        Message = "Domain is required and cannot be empty after normalization."
                    });
                    AddInvalidRow(invalidRowsPayload, rowNumber, rawValues, "Domain is required and cannot be empty after normalization.");
                    continue;
                }

                TrackDuplicateDomain(normalizedDomain, duplicateDomainOccurrences, duplicateDomainsInOrder);

                var normalizedReason = string.IsNullOrWhiteSpace(rawReason)
                    ? null
                    : rawReason.Trim();

                var update = new ParsedUpdate(normalizedReason);

                if (updates.ContainsKey(normalizedDomain))
                {
                    duplicateInputRowsCount++;
                    result.DuplicatesCount++;
                    if (result.Duplicates.Count < ImportConstants.SitesImportMaxDetailDuplicates)
                    {
                        result.Duplicates.Add(normalizedDomain);
                    }

                    updates[normalizedDomain] = update;
                }
                else
                {
                    updates.Add(normalizedDomain, update);
                }

                if (!validRowsByDomain.TryGetValue(normalizedDomain, out var rows))
                {
                    rows = new List<UnmatchedImportRowRecord>();
                    validRowsByDomain[normalizedDomain] = rows;
                }

                rows.Add(new UnmatchedImportRowRecord
                {
                    SourceRowNumber = rowNumber,
                    RawValues = rawValues.ToList()
                });
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
                if (validRowsByDomain.TryGetValue(domain, out var unmatchedRowsForDomain))
                {
                    unmatchedRowsPayload.Rows.AddRange(unmatchedRowsForDomain);
                }

                continue;
            }

            site.IsQuarantined = true;
            site.QuarantineReason = update.Reason;
            site.QuarantineUpdatedAtUtc = now;
            site.UpdatedAtUtc = now;

            result.Matched++;
        }

        AddImportLog(result, userId, userEmail);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Quarantine import completed. Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}, UserId={UserId}",
            result.Matched,
            result.Unmatched.Count,
            result.ErrorsCount,
            result.DuplicatesCount,
            userId);

        AttachSummaryAndDownloads(
            result,
            invalidRowsPayload,
            unmatchedRowsPayload,
            ImportConstants.ImportArtifactSlugQuarantine,
            duplicateInputRowsCount,
            duplicateDomainsInOrder);
        return result;
    }

    private static async IAsyncEnumerable<(int RowNumber, string Domain, string? RawReason, List<string> RawValues)> ReadRowsAsync(
        CsvHelper.CsvReader csv,
        int headerCount,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Row 1 = header
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
                string.IsNullOrWhiteSpace(rawReason) ? null : rawReason,
                rawValues);
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
        SitesUpdateImportResult result,
        string userId,
        string userEmail)
    {
        var log = new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeQuarantine,
            TimestampUtc = DateTime.UtcNow,
            Inserted = 0,
            Duplicates = result.DuplicatesCount,
            Matched = result.Matched,
            Unmatched = result.Unmatched.Count,
            ErrorsCount = result.ErrorsCount
        };

        _context.ImportLogs.Add(log);
    }

    private static void AddInvalidRow(
        InvalidRowsImportArtifactPayload payload,
        int sourceRowNumber,
        IReadOnlyCollection<string> rawValues,
        string errorMessage)
    {
        payload.Rows.Add(new InvalidImportRowRecord
        {
            SourceRowNumber = sourceRowNumber,
            RawValues = rawValues.ToList(),
            Errors = new List<string> { errorMessage }
        });
    }

    private static void TrackDuplicateDomain(
        string normalizedDomain,
        IDictionary<string, int> occurrences,
        ICollection<string> duplicateDomainsInOrder)
    {
        if (occurrences.TryGetValue(normalizedDomain, out var count))
        {
            var nextCount = count + 1;
            occurrences[normalizedDomain] = nextCount;
            if (nextCount == 2)
            {
                duplicateDomainsInOrder.Add(normalizedDomain);
            }

            return;
        }

        occurrences[normalizedDomain] = 1;
    }

    private void AttachSummaryAndDownloads(
        SitesUpdateImportResult result,
        InvalidRowsImportArtifactPayload invalidRowsPayload,
        UnmatchedRowsImportArtifactPayload unmatchedRowsPayload,
        string importType,
        int duplicateInputRowsCount,
        IReadOnlyCollection<string> duplicateDomainsInOrder)
    {
        result.UpdatedCount = result.Matched;
        result.SkippedExistingCount = 0;
        result.DuplicateInputRowsCount = duplicateInputRowsCount;
        result.InvalidRowsCount = result.ErrorsCount;
        result.UnmatchedRowsCount = unmatchedRowsPayload.Rows.Count;
        result.DuplicateDomainsCount = duplicateDomainsInOrder.Count;
        result.DuplicateDomainsPreview = duplicateDomainsInOrder
            .Take(ImportConstants.DuplicateDomainsPreviewLimit)
            .ToList();

        ImportDownloadItem? invalidRowsDownload = null;
        if (invalidRowsPayload.Rows.Count > 0)
        {
            var handle = _importArtifactStorageService.StoreInvalidRows(importType, invalidRowsPayload);
            invalidRowsDownload = new ImportDownloadItem
            {
                Available = true,
                Token = handle.Token,
                FileName = handle.FileName
            };
        }

        ImportDownloadItem? unmatchedRowsDownload = null;
        if (unmatchedRowsPayload.Rows.Count > 0)
        {
            var handle = _importArtifactStorageService.StoreUnmatchedRows(importType, unmatchedRowsPayload);
            unmatchedRowsDownload = new ImportDownloadItem
            {
                Available = true,
                Token = handle.Token,
                FileName = handle.FileName
            };
        }

        result.Downloads = new ImportDownloadsInfo
        {
            InvalidRows = invalidRowsDownload,
            UnmatchedRows = unmatchedRowsDownload
        };
    }
}
