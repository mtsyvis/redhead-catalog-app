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

        if (!IsCsvFile(fileName, contentType))
        {
            _logger.LogWarning(
                "LastPublished import rejected: not CSV. FileName={FileName}, ContentType={ContentType}",
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
        var duplicateInputRowsCount = 0;

        // Phase 1: parse CSV rows and build per-domain update map (last row wins)
        var updates = new Dictionary<string, ParsedUpdate>(StringComparer.Ordinal);

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: RequiredHeaderOrder,
                         requiredHeadersInStrictOrder: RequiredHeaderOrder,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();

            await foreach (var (rowNumber, domain, rawDate, rawValues) in ReadRowsAsync(
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

                if (string.IsNullOrEmpty(rawDate))
                {
                    result.ErrorsCount++;
                    result.Errors.Add(new SitesUpdateImportError
                    {
                        RowNumber = rowNumber,
                        Message = "LastPublishedDate is required and cannot be empty."
                    });
                    AddInvalidRow(invalidRowsPayload, rowNumber, rawValues, "LastPublishedDate is required and cannot be empty.");
                    continue;
                }

                if (!TryParseLastPublishedDate(
                        rawDate.Trim(),
                        out var parsedValue,
                        out var parsedIsMonthOnly,
                        out var parseError))
                {
                    result.ErrorsCount++;
                    result.Errors.Add(new SitesUpdateImportError
                    {
                        RowNumber = rowNumber,
                        Message = parseError
                    });
                    AddInvalidRow(invalidRowsPayload, rowNumber, rawValues, parseError);
                    continue;
                }

                var update = new ParsedUpdate(parsedValue, parsedIsMonthOnly);

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
                result.Unmatched.Add(domain);
                continue;
            }

            site.LastPublishedDate = update.DateUtc;
            site.LastPublishedDateIsMonthOnly = update.IsMonthOnly;
            site.UpdatedAtUtc = now;

            result.Matched++;
        }

        AddImportLog(result, userId, userEmail);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "LastPublished import completed. Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}, UserId={UserId}",
            result.Matched,
            result.Unmatched.Count,
            result.ErrorsCount,
            result.DuplicatesCount,
            userId);

        AttachSummaryAndDownloads(result, invalidRowsPayload, ImportConstants.ImportArtifactSlugLastPublished, duplicateInputRowsCount);
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
        SitesUpdateImportResult result,
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

    private void AttachSummaryAndDownloads(
        SitesUpdateImportResult result,
        InvalidRowsImportArtifactPayload invalidRowsPayload,
        string importType,
        int duplicateInputRowsCount)
    {
        result.UpdatedCount = result.Matched;
        result.SkippedExistingCount = 0;
        result.DuplicateInputRowsCount = duplicateInputRowsCount;
        result.InvalidRowsCount = result.ErrorsCount;

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

        result.Downloads = new ImportDownloadsInfo
        {
            InvalidRows = invalidRowsDownload,
            DuplicateInputRows = null
        };
    }
}
