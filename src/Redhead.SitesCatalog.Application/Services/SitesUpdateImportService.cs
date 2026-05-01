using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Mass-update import of existing sites from CSV. Same columns as sites import.
/// Domain is lookup key only (never updated). Last valid row wins for duplicate domains in file.
/// Processing model:
/// 1) Parse rows, validate, build per-domain update map (last valid row wins)
/// 2) Load matching sites from DB in batches
/// 3) Apply updates and persist once
/// </summary>
public sealed class SitesUpdateImportService : ISitesUpdateImportService
{
    private const int BatchSize = 1000;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SitesUpdateImportService> _logger;
    private readonly IImportArtifactStorageService _importArtifactStorageService;

    /// <summary>
    /// Parsed update for one domain. Domain is lookup key only.
    /// </summary>
    private sealed record ParsedSiteUpdate(
        string NormalizedDomain,
        double DR,
        long Traffic,
        string Location,
        decimal? PriceUsd,
        decimal? PriceCasino,
        ServiceAvailabilityStatus PriceCasinoStatus,
        decimal? PriceCrypto,
        ServiceAvailabilityStatus PriceCryptoStatus,
        decimal? PriceLinkInsert,
        ServiceAvailabilityStatus PriceLinkInsertStatus,
        decimal? PriceLinkInsertCasino,
        ServiceAvailabilityStatus PriceLinkInsertCasinoStatus,
        decimal? PriceDating,
        ServiceAvailabilityStatus PriceDatingStatus,
        int? NumberDFLinks,
        TermType? TermType,
        int? TermValue,
        TermUnit? TermUnit,
        string? Niche,
        string? Categories,
        string? LinkType,
        string? SponsoredTag);

    public SitesUpdateImportService(
        ApplicationDbContext context,
        ILogger<SitesUpdateImportService> logger,
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
            "Sites update import started. FileName={FileName}, UserId={UserId}, UserEmail={UserEmail}",
            fileName,
            userId,
            userEmail);

        var result = new SitesUpdateImportResult();
        var invalidRowsCount = 0;
        var duplicateRowsCount = 0;
        var unmatchedDomainsCount = 0;

        // Phase 1: parse CSV, validate, build per-domain map (only valid rows enter; last valid wins)
        var updatesByDomain = new Dictionary<string, ParsedSiteUpdate>(StringComparer.Ordinal);
        var invalidRowsPayload = new InvalidRowsImportArtifactPayload();
        var unmatchedRowsPayload = new UnmatchedRowsImportArtifactPayload();
        var validRowsByDomain = new Dictionary<string, List<UnmatchedImportRowRecord>>(StringComparer.Ordinal);
        var duplicateDomainOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateDomainsInOrder = new List<string>();

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.SitesImportRequiredColumnOrder,
                         requiredHeadersInStrictOrder: ImportConstants.SitesImportRequiredColumnOrder,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();
            unmatchedRowsPayload.Headers = session.Header.ToArray();

            await foreach (var (row, rawValues) in ReadRowsAsync(session.Csv, session.Header.Length, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (isEmpty, error, validatedData) = SitesImportRowValidationHelper.Validate(row);
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

                if (validatedData is null)
                {
                    continue;
                }

                var domain = validatedData.NormalizedDomain;
                var update = ToParsedSiteUpdate(validatedData);

                if (updatesByDomain.ContainsKey(domain))
                {
                    duplicateRowsCount++;
                }

                updatesByDomain[domain] = update;

                if (!validRowsByDomain.TryGetValue(domain, out var rows))
                {
                    rows = new List<UnmatchedImportRowRecord>();
                    validRowsByDomain[domain] = rows;
                }

                rows.Add(new UnmatchedImportRowRecord
                {
                    SourceRowNumber = row.RowNumber,
                    RawValues = rawValues.ToList()
                });
            }
        }

        // Phase 2: load matching sites in batches
        var domains = updatesByDomain.Keys.ToList();
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

        // Phase 3: apply updates, then save once
        var now = DateTime.UtcNow;

        foreach (var (domain, update) in updatesByDomain)
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

            site.DR = update.DR;
            site.Traffic = update.Traffic;
            site.Location = update.Location;
            site.PriceUsd = update.PriceUsd;
            site.PriceCasino = update.PriceCasino;
            site.PriceCasinoStatus = update.PriceCasinoStatus;
            site.PriceCrypto = update.PriceCrypto;
            site.PriceCryptoStatus = update.PriceCryptoStatus;
            site.PriceLinkInsert = update.PriceLinkInsert;
            site.PriceLinkInsertStatus = update.PriceLinkInsertStatus;
            site.PriceLinkInsertCasino = update.PriceLinkInsertCasino;
            site.PriceLinkInsertCasinoStatus = update.PriceLinkInsertCasinoStatus;
            site.PriceDating = update.PriceDating;
            site.PriceDatingStatus = update.PriceDatingStatus;
            site.NumberDFLinks = update.NumberDFLinks;
            site.TermType = update.TermType;
            site.TermValue = update.TermValue;
            site.TermUnit = update.TermUnit;
            site.Niche = update.Niche;
            site.Categories = update.Categories;
            site.LinkType = update.LinkType;
            site.SponsoredTag = update.SponsoredTag;
            site.UpdatedAtUtc = now;

            result.UpdatedCount++;
        }

        AddImportLog(result.UpdatedCount, unmatchedDomainsCount, invalidRowsCount, duplicateRowsCount, userId, userEmail);
        await _context.SaveChangesAsync(cancellationToken);

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

    private static ParsedSiteUpdate ToParsedSiteUpdate(SitesImportRowValidationHelper.ValidatedSitesRow data)
    {
        return new ParsedSiteUpdate(
            data.NormalizedDomain,
            data.DR,
            data.Traffic,
            data.Location,
            data.PriceUsd,
            data.PriceCasino,
            data.PriceCasinoStatus,
            data.PriceCrypto,
            data.PriceCryptoStatus,
            data.PriceLinkInsert,
            data.PriceLinkInsertStatus,
            data.PriceLinkInsertCasino,
            data.PriceLinkInsertCasinoStatus,
            data.PriceDating,
            data.PriceDatingStatus,
            data.NumberDFLinks,
            data.TermType,
            data.TermValue,
            data.TermUnit,
            data.Niche,
            data.Categories,
            data.LinkType,
            data.SponsoredTag);
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
