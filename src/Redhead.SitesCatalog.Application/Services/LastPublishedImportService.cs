using System.Globalization;
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
/// Imports LastPublishedDate updates from CSV with strict header order:
/// Domain, LastPublishedDate
/// Matching rule: exact match by normalized domain.
/// Processing model:
/// 1) Parse rows and build per-domain update map (last row wins)
/// 2) Load Sites in batches
/// 3) Apply updates and persist once
/// </summary>
public sealed class LastPublishedImportService : ILastPublishedImportService
{
    private const int BatchSize = 1000;

    private const string HeaderDomain = "Domain";
    private const string HeaderLastPublishedDate = "LastPublishedDate";

    private static readonly string[] RequiredHeaderOrder =
    {
        HeaderDomain,
        HeaderLastPublishedDate
    };

    private static readonly string[] DayFormats = { "dd.MM.yyyy" };
    private static readonly string[] MonthFormats = { "MMMM yyyy", "MMM yyyy" };

    private readonly ApplicationDbContext _context;
    private readonly ILogger<LastPublishedImportService> _logger;
    private readonly IImportArtifactStorageService _importArtifactStorageService;

    private sealed record ParsedUpdate(DateTime DateUtc, bool IsMonthOnly);

    public LastPublishedImportService(
        ApplicationDbContext context,
        ILogger<LastPublishedImportService> logger,
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
            "LastPublished import started. FileName={FileName}, UserId={UserId}, UserEmail={UserEmail}",
            fileName,
            userId,
            userEmail);

        var result = new SitesUpdateImportResult();
        var invalidRowsCount = 0;
        var duplicateRowsCount = 0;
        var unmatchedDomainsCount = 0;
        var now = DateTime.UtcNow;
        var invalidRowsPayload = new InvalidRowsImportArtifactPayload();
        var unmatchedRowsPayload = new UnmatchedRowsImportArtifactPayload();
        var validRowsByDomain = new Dictionary<string, List<UnmatchedImportRowRecord>>(StringComparer.Ordinal);
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

            await foreach (var (rowNumber, domain, rawDate, rawValues) in ReadRowsAsync(
                               session.Csv,
                               session.Header.Length,
                               cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalizedDomain = DomainNormalizer.Normalize(domain);
                if (string.IsNullOrEmpty(normalizedDomain))
                {
                    invalidRowsCount++;
                    AddInvalidRow(invalidRowsPayload, rowNumber, rawValues, "Domain is required and cannot be empty after normalization.");
                    continue;
                }

                TrackDuplicateDomain(normalizedDomain, duplicateDomainOccurrences, duplicateDomainsInOrder);

                if (string.IsNullOrEmpty(rawDate))
                {
                    invalidRowsCount++;
                    AddInvalidRow(invalidRowsPayload, rowNumber, rawValues, "LastPublishedDate is required and cannot be empty.");
                    continue;
                }

                if (!TryParseLastPublishedDate(
                        rawDate.Trim(),
                        out var parsedValue,
                        out var parsedIsMonthOnly,
                        out var parseError))
                {
                    invalidRowsCount++;
                    AddInvalidRow(invalidRowsPayload, rowNumber, rawValues, parseError);
                    continue;
                }

                var update = new ParsedUpdate(parsedValue, parsedIsMonthOnly);

                if (updates.ContainsKey(normalizedDomain))
                {
                    duplicateRowsCount++;

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
        var domainsForUpdate = updates.Keys.ToList();
        var sitesByDomain = new Dictionary<string, Site>(StringComparer.Ordinal);

        foreach (var chunk in Chunk(domainsForUpdate, BatchSize))
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
                unmatchedDomainsCount++;
                if (validRowsByDomain.TryGetValue(domain, out var unmatchedRowsForDomain))
                {
                    unmatchedRowsPayload.Rows.AddRange(unmatchedRowsForDomain);
                }

                continue;
            }

            site.LastPublishedDate = update.DateUtc;
            site.LastPublishedDateIsMonthOnly = update.IsMonthOnly;
            site.UpdatedAtUtc = now;

            result.UpdatedCount++;
        }

        AddImportLog(result.UpdatedCount, unmatchedDomainsCount, invalidRowsCount, duplicateRowsCount, userId, userEmail);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "LastPublished import completed. Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}, UserId={UserId}",
            result.UpdatedCount,
            unmatchedDomainsCount,
            invalidRowsCount,
            duplicateRowsCount,
            userId);

        AttachSummaryAndDownloads(
            result,
            invalidRowsPayload,
            unmatchedRowsPayload,
            ImportConstants.ImportArtifactSlugLastPublished,
            invalidRowsCount,
            duplicateDomainsInOrder);
        return result;
    }

    public static bool TryParseLastPublishedDate(
        string value,
        out DateTime dateUtc,
        out bool isMonthOnly,
        out string errorMessage)
    {
        dateUtc = default;
        isMonthOnly = false;
        errorMessage = string.Empty;

        if (DateTime.TryParseExact(
                value,
                DayFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fullDate))
        {
            dateUtc = DateTime.SpecifyKind(fullDate.Date, DateTimeKind.Utc);
            isMonthOnly = false;
            return true;
        }

        if (DateTime.TryParseExact(
                value,
                MonthFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var monthDate))
        {
            dateUtc = DateTime.SpecifyKind(
                new DateTime(monthDate.Year, monthDate.Month, 1),
                DateTimeKind.Utc);

            isMonthOnly = true;
            return true;
        }

        errorMessage = "LastPublishedDate could not be parsed. Use a full date 'DD.MM.YYYY' or month+year like 'January 2026' or 'Jan 2026'.";
        return false;
    }

    private static async IAsyncEnumerable<(int RowNumber, string Domain, string RawDate, List<string> RawValues)> ReadRowsAsync(
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
            var rawDate = csv.GetField(1)?.Trim();

            // Skip fully empty rows
            if (string.IsNullOrWhiteSpace(domain) && string.IsNullOrWhiteSpace(rawDate))
            {
                continue;
            }

            yield return (rowNumber, domain ?? string.Empty, rawDate ?? string.Empty, rawValues);
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
            Type = ImportConstants.ImportTypeLastPublished,
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
