using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Add-only sites import from CSV with strict header order.
/// Processing model:
/// 1) Parse rows and build per-domain site map (last valid row wins)
/// 2) Load existing sites only for domains present in the file
/// 3) Insert only new sites in batches inside a single transaction
/// </summary>
public sealed class SitesImportService : ISitesImportService
{
    private static readonly IReadOnlyDictionary<string, int> ColumnIndexes =
        ImportConstants.SitesImportRequiredColumnOrder
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SitesImportService> _logger;
    private readonly IImportArtifactStorageService _importArtifactStorageService;

    public SitesImportService(
        ApplicationDbContext context,
        ILogger<SitesImportService> logger,
        IImportArtifactStorageService importArtifactStorageService)
    {
        _context = context;
        _logger = logger;
        _importArtifactStorageService = importArtifactStorageService;
    }

    public async Task<SitesImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sites import started. FileName={FileName}, UserId={UserId}, UserEmail={UserEmail}",
            fileName,
            userId,
            userEmail);

        var result = new SitesImportResult();

        // Phase 1: parse CSV rows and build per-domain map (last valid row wins)
        var parsedSitesByDomain = new Dictionary<string, Site>(StringComparer.Ordinal);
        var invalidRowsPayload = new InvalidRowsImportArtifactPayload();
        var skippedExistingCount = 0;
        var duplicateRowsCount = 0;
        var invalidRowsCount = 0;
        var duplicateDomainOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateDomainsInOrder = new List<string>();

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.SitesImportRequiredColumnOrder,
                         requiredHeadersInStrictOrder: ImportConstants.SitesImportRequiredColumnOrder,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();

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

                if (parsedSitesByDomain.ContainsKey(domain))
                {
                    duplicateRowsCount++;
                }

                parsedSitesByDomain[domain] = BuildSiteFromValidatedRow(validatedData);
            }
        }

        // Nothing to insert, but still persist import log for traceability.
        if (parsedSitesByDomain.Count == 0)
        {
            AttachSummaryAndDownloads(
                result,
                invalidRowsPayload,
                ImportConstants.ImportArtifactSlugSites,
                invalidRowsCount,
                skippedExistingCount,
                duplicateDomainsInOrder);
            await SaveImportLogOnlyAsync(result, duplicateRowsCount, invalidRowsCount, userId, userEmail, cancellationToken);

            _logger.LogInformation(
                "Sites import completed with no valid rows. Inserted={Inserted}, Duplicates={Duplicates}, Errors={Errors}, UserId={UserId}",
                result.InsertedCount,
                duplicateRowsCount,
                invalidRowsCount,
                userId);

            return result;
        }

        // Phase 2: load existing domains only for domains present in the file
        var inputDomains = parsedSitesByDomain.Keys.ToList();
        var existingDomainsInDb = await LoadExistingDomainsAsync(inputDomains, cancellationToken);

        var sitesToInsert = new List<Site>(parsedSitesByDomain.Count);

        foreach (var (domain, site) in parsedSitesByDomain)
        {
            if (existingDomainsInDb.Contains(domain))
            {
                skippedExistingCount++;
                duplicateRowsCount++;
                continue;
            }

            sitesToInsert.Add(site);
        }

        // Phase 3: insert new sites in batches inside a single transaction
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await InsertSitesInBatchesAsync(sitesToInsert, result, cancellationToken);
            AddImportLog(result.InsertedCount, duplicateRowsCount, invalidRowsCount, userId, userEmail);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Sites import failed due to DB update error. FileName={FileName}, UserId={UserId}", fileName, userId);

            // This is the clean behavior for rare concurrent imports / unique constraint races.
            throw new ImportConcurrencyException("Sites import could not be completed because the data was modified concurrently. Please try again later.");
        }

        _logger.LogInformation(
            "Sites import completed. Inserted={Inserted}, Duplicates={Duplicates}, Errors={Errors}, UserId={UserId}",
            result.InsertedCount,
            duplicateRowsCount,
            invalidRowsCount,
            userId);

        AttachSummaryAndDownloads(
            result,
            invalidRowsPayload,
            ImportConstants.ImportArtifactSlugSites,
            invalidRowsCount,
            skippedExistingCount,
            duplicateDomainsInOrder);
        return result;
    }

    private static async IAsyncEnumerable<(SitesImportRowDto Row, List<string> RawValues)> ReadRowsAsync(
        CsvReader csv,
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

            var mappedRow = SitesImportRowMapper.Map(
                columnName => csv.GetField(ColumnIndexes[columnName])?.Trim(),
                rowNumber);

            yield return (mappedRow, rawValues);
        }
    }

    private async Task<HashSet<string>> LoadExistingDomainsAsync(
        List<string> domains,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chunk in Chunk(domains, ImportConstants.SitesImportBatchSize))
        {
            var existing = await _context.Sites
                .AsNoTracking()
                .Where(s => chunk.Contains(s.Domain))
                .Select(s => s.Domain)
                .ToListAsync(cancellationToken);

            foreach (var domain in existing)
            {
                result.Add(domain);
            }
        }

        return result;
    }

    private async Task InsertSitesInBatchesAsync(
        List<Site> sitesToInsert,
        SitesImportResult result,
        CancellationToken cancellationToken)
    {
        if (sitesToInsert.Count == 0)
        {
            return;
        }

        var previousAutoDetectChanges = _context.ChangeTracker.AutoDetectChangesEnabled;
        _context.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            foreach (var chunk in Chunk(sitesToInsert, ImportConstants.SitesImportBatchSize))
            {
                await _context.Sites.AddRangeAsync(chunk, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                result.InsertedCount += chunk.Count;

                // Important for large imports to avoid growing the tracker across batches.
                _context.ChangeTracker.Clear();
            }
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = previousAutoDetectChanges;
        }
    }

    private async Task SaveImportLogOnlyAsync(
        SitesImportResult result,
        int duplicateRowsCount,
        int invalidRowsCount,
        string userId,
        string userEmail,
        CancellationToken cancellationToken)
    {
        AddImportLog(result.InsertedCount, duplicateRowsCount, invalidRowsCount, userId, userEmail);
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    private void AddImportLog(
        int insertedCount,
        int duplicateRowsCount,
        int invalidRowsCount,
        string userId,
        string userEmail)
    {
        var log = new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeSites,
            TimestampUtc = DateTime.UtcNow,
            Inserted = insertedCount,
            Duplicates = duplicateRowsCount,
            Matched = 0,
            Unmatched = 0,
            ErrorsCount = invalidRowsCount
        };

        _context.ImportLogs.Add(log);
    }

    private static Site BuildSiteFromValidatedRow(SitesImportRowValidationHelper.ValidatedSitesRow data)
    {
        var now = DateTime.UtcNow;

        return new Site
        {
            Domain = data.NormalizedDomain,
            DR = data.DR,
            Traffic = data.Traffic,
            Location = data.Location,
            PriceUsd = data.PriceUsd,
            PriceCasino = data.PriceCasino,
            PriceCasinoStatus = data.PriceCasinoStatus,
            PriceCrypto = data.PriceCrypto,
            PriceCryptoStatus = data.PriceCryptoStatus,
            PriceLinkInsert = data.PriceLinkInsert,
            PriceLinkInsertStatus = data.PriceLinkInsertStatus,
            Niche = data.Niche,
            Categories = data.Categories,
            LinkType = data.LinkType,
            SponsoredTag = data.SponsoredTag,
            IsQuarantined = false,
            QuarantineReason = null,
            QuarantineUpdatedAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
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

    private void AttachSummaryAndDownloads(
        SitesImportResult result,
        InvalidRowsImportArtifactPayload invalidRowsPayload,
        string importType,
        int invalidRowsCount,
        int skippedExistingCount,
        IReadOnlyCollection<string> duplicateDomainsInOrder)
    {
        result.SkippedExistingCount = skippedExistingCount;
        result.InvalidRowsCount = invalidRowsCount;
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

        result.Downloads = new ImportDownloadsInfo
        {
            InvalidRows = invalidRowsDownload,
        };
    }
}
