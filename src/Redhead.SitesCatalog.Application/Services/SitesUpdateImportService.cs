using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Application.Services.UpdateImport;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Mass-update import of existing sites from CSV. Only columns present in the file are updated.
/// Domain is lookup key only (never updated). Last valid row wins for duplicate domains in file.
/// </summary>
public sealed class SitesUpdateImportService : ISitesUpdateImportService
{
    private const int BatchSize = 1000;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SitesUpdateImportService> _logger;
    private readonly IImportArtifactStorageService _importArtifactStorageService;
    private readonly INicheFilterOptionsCache _nicheFilterOptionsCache;

    public SitesUpdateImportService(
        ApplicationDbContext context,
        ILogger<SitesUpdateImportService> logger,
        IImportArtifactStorageService importArtifactStorageService,
        INicheFilterOptionsCache nicheFilterOptionsCache)
    {
        _context = context;
        _logger = logger;
        _importArtifactStorageService = importArtifactStorageService;
        _nicheFilterOptionsCache = nicheFilterOptionsCache;
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
            "Sites update import started. FileName={FileName}, UserId={UserId}, UserEmail={UserEmail}",
            fileName,
            userId,
            userEmail);

        var result = new SitesUpdateImportResult();
        var invalidRowsCount = 0;
        var duplicateRowsCount = 0;
        var unmatchedDomainsCount = 0;

        var updateCandidatesByDomain = new Dictionary<string, List<SitesUpdateImportRow>>(StringComparer.Ordinal);
        var invalidRowsPayload = new InvalidRowsImportArtifactPayload();
        var unmatchedRowsPayload = new UnmatchedRowsImportArtifactPayload();
        var validRowsByDomain = new Dictionary<string, List<UnmatchedImportRowRecord>>(StringComparer.Ordinal);
        var duplicateDomainOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateDomainsInOrder = new List<string>();

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.SitesImportRequiredColumnOrder,
                         validateHeader: SitesUpdateImportHeaderValidator.ValidateOrThrow,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();
            unmatchedRowsPayload.Headers = session.Header.ToArray();
            var presentColumns = SitesUpdateImportHeaderValidator.BuildPresentColumnSet(session.Header);

            await foreach (var (row, rawValues) in ReadRowsAsync(session.Csv, session.Header.Length, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (isEmpty, error, update) = SitesUpdateImportRowValidator.Validate(row, presentColumns);
                if (isEmpty)
                {
                    continue;
                }

                TrackDuplicateDomain(row.Domain, duplicateDomainOccurrences, duplicateDomainsInOrder);

                if (error is not null)
                {
                    invalidRowsCount++;
                    AddInvalidRow(invalidRowsPayload, row.RowNumber, rawValues, error.Message);
                    continue;
                }

                if (update is null)
                {
                    continue;
                }

                update = update with
                {
                    SourceRowNumber = row.RowNumber,
                    RawValues = rawValues.ToList()
                };

                AddUpdateCandidate(updateCandidatesByDomain, update, ref duplicateRowsCount);
                AddValidSourceRow(validRowsByDomain, update.NormalizedDomain, row.RowNumber, rawValues);
            }
        }

        var sitesByDomain = await LoadSitesByDomainAsync(updateCandidatesByDomain.Keys.ToList(), cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var (domain, candidates) in updateCandidatesByDomain)
        {
            if (!sitesByDomain.TryGetValue(domain, out var site))
            {
                unmatchedDomainsCount++;
                if (validRowsByDomain.TryGetValue(domain, out var unmatchedRowsForDomain))
                {
                    unmatchedRowsPayload.Rows.AddRange(unmatchedRowsForDomain);
                }

                continue;
            }

            var update = SelectLastValidCandidate(candidates);
            if (update is null)
            {
                continue;
            }

            SitesUpdateImportApplier.Apply(site, update);
            site.UpdatedAtUtc = now;
            result.UpdatedCount++;
        }

        AddImportLog(result.UpdatedCount, unmatchedDomainsCount, invalidRowsCount, duplicateRowsCount, userId, userEmail);
        await _context.SaveChangesAsync(cancellationToken);
        if (result.UpdatedCount > 0)
        {
            _nicheFilterOptionsCache.Invalidate();
        }

        _logger.LogInformation(
            "Sites update import completed. Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}, UserId={UserId}",
            result.UpdatedCount,
            unmatchedDomainsCount,
            invalidRowsCount,
            duplicateRowsCount,
            userId);

        AttachSummaryAndDownloads(
            result,
            invalidRowsPayload,
            unmatchedRowsPayload,
            ImportConstants.ImportArtifactSlugSitesUpdate,
            invalidRowsCount,
            duplicateDomainsInOrder);
        return result;
    }

    private async Task<Dictionary<string, Site>> LoadSitesByDomainAsync(
        List<string> domains,
        CancellationToken cancellationToken)
    {
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

        return sitesByDomain;
    }

    private static SitesUpdateImportRow? SelectLastValidCandidate(IEnumerable<SitesUpdateImportRow> candidates)
    {
        SitesUpdateImportRow? update = null;
        foreach (var candidate in candidates)
        {
            update = candidate;
        }

        return update;
    }

    private static async IAsyncEnumerable<(SitesImportRowDto Row, List<string> RawValues)> ReadRowsAsync(
        CsvReader csv,
        int headerCount,
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
                rowNumber);

            yield return (mappedRow, rawValues);
        }
    }

    private static void AddUpdateCandidate(
        IDictionary<string, List<SitesUpdateImportRow>> updateCandidatesByDomain,
        SitesUpdateImportRow update,
        ref int duplicateRowsCount)
    {
        if (!updateCandidatesByDomain.TryGetValue(update.NormalizedDomain, out var candidates))
        {
            candidates = new List<SitesUpdateImportRow>();
            updateCandidatesByDomain[update.NormalizedDomain] = candidates;
        }

        if (candidates.Count > 0)
        {
            duplicateRowsCount++;
        }

        candidates.Add(update);
    }

    private static void AddValidSourceRow(
        IDictionary<string, List<UnmatchedImportRowRecord>> validRowsByDomain,
        string domain,
        int rowNumber,
        IReadOnlyCollection<string> rawValues)
    {
        if (!validRowsByDomain.TryGetValue(domain, out var rows))
        {
            rows = new List<UnmatchedImportRowRecord>();
            validRowsByDomain[domain] = rows;
        }

        rows.Add(new UnmatchedImportRowRecord
        {
            SourceRowNumber = rowNumber,
            RawValues = rawValues.ToList()
        });
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private void AddImportLog(
        int updatedCount,
        int unmatchedDomainsCount,
        int invalidRowsCount,
        int duplicateRowsCount,
        string userId,
        string userEmail)
    {
        var log = new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeSitesUpdate,
            TimestampUtc = DateTime.UtcNow,
            Inserted = 0,
            Duplicates = duplicateRowsCount,
            Matched = updatedCount,
            Unmatched = unmatchedDomainsCount,
            ErrorsCount = invalidRowsCount
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
        string? rawDomain,
        IDictionary<string, int> occurrences,
        ICollection<string> duplicateDomainsInOrder)
    {
        var normalizedDomain = DomainNormalizer.Normalize(rawDomain);
        if (string.IsNullOrEmpty(normalizedDomain))
        {
            return;
        }

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

    private void AttachSummaryAndDownloads(
        SitesUpdateImportResult result,
        InvalidRowsImportArtifactPayload invalidRowsPayload,
        UnmatchedRowsImportArtifactPayload unmatchedRowsPayload,
        string importType,
        int invalidRowsCount,
        IReadOnlyCollection<string> duplicateDomainsInOrder)
    {
        result.InvalidRowsCount = invalidRowsCount;
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
