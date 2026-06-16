using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Import.Artifacts;
using Redhead.SitesCatalog.Application.Services.Import.Common;
using Redhead.SitesCatalog.Application.Services.Import.Csv;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Application.Services.Import.Sites;

/// <summary>
/// Add-only sites import from CSV with strict header order.
/// Processing model:
/// 1) Parse rows and build per-domain site map (last valid row wins)
/// 2) Load existing sites only for domains present in the file
/// 3) Insert only new sites in batches inside a single transaction
/// </summary>
public sealed class SitesImportService : ISitesImportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SitesImportService> _logger;
    private readonly IImportArtifactStorageService _importArtifactStorageService;
    private readonly INicheFilterOptionsCache _nicheFilterOptionsCache;
    private readonly ILocationNormalizer _locationNormalizer;

    public SitesImportService(
        ApplicationDbContext context,
        ILogger<SitesImportService> logger,
        IImportArtifactStorageService importArtifactStorageService,
        INicheFilterOptionsCache nicheFilterOptionsCache,
        ILocationNormalizer locationNormalizer)
    {
        _context = context;
        _logger = logger;
        _importArtifactStorageService = importArtifactStorageService;
        _nicheFilterOptionsCache = nicheFilterOptionsCache;
        _locationNormalizer = locationNormalizer;
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
        var locationWarningsByDomain = new Dictionary<string, WarningImportRowRecord>(StringComparer.Ordinal);
        var invalidRowsPayload = new InvalidRowsImportArtifactPayload();
        var warningRowsPayload = new WarningRowsImportArtifactPayload();
        var skippedExistingCount = 0;
        var duplicateRowsCount = 0;
        var invalidRowsCount = 0;
        var duplicateDomainOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateDomainsInOrder = new List<string>();

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.SitesImportRequiredColumns,
                         validateHeader: SitesImportHeaderParser.ValidateOrThrow,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();
            var insertHeaderInfo = SitesImportHeaderParser.Parse(session.Header);

            await foreach (var (row, rawValues) in SitesImportCsvRowReader.ReadRowsAsync(session.Csv, session.Header.Length, insertHeaderInfo, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (isEmpty, error, validatedData) = SitesImportRowValidator.Validate(row);
                if (isEmpty)
                {
                    continue;
                }

                ImportRowTrackingHelper.TrackDuplicateDomain(row.Domain, duplicateDomainOccurrences, duplicateDomainsInOrder);

                if (error is not null)
                {
                    invalidRowsCount++;
                    ImportRowTrackingHelper.AddInvalidRow(invalidRowsPayload, row.RowNumber, rawValues, error.Message);
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

                parsedSitesByDomain[domain] = BuildSiteFromValidatedRow(
                    validatedData,
                    row.RowNumber,
                    userEmail,
                    out var locationWarning);
                if (locationWarning is null)
                {
                    locationWarningsByDomain.Remove(domain);
                }
                else
                {
                    locationWarningsByDomain[domain] = locationWarning;
                }
            }
        }

        // Nothing to insert, but still persist import log for traceability.
        if (parsedSitesByDomain.Count == 0)
        {
            AttachSummaryAndDownloads(
                result,
                invalidRowsPayload,
                warningRowsPayload,
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
            if (locationWarningsByDomain.TryGetValue(domain, out var warning))
            {
                warningRowsPayload.Rows.Add(warning);
            }
        }

        // Phase 3: insert new sites in batches inside a single transaction
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await InsertSitesInBatchesAsync(sitesToInsert, result, cancellationToken);
            AddImportLog(result.InsertedCount, duplicateRowsCount, invalidRowsCount, userId, userEmail);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _nicheFilterOptionsCache.Invalidate();
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
            warningRowsPayload,
            ImportConstants.ImportArtifactSlugSites,
            invalidRowsCount,
            skippedExistingCount,
            duplicateDomainsInOrder);
        return result;
    }

    private async Task<HashSet<string>> LoadExistingDomainsAsync(
        List<string> domains,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chunk in ImportBatchingHelper.Chunk(domains, ImportConstants.SitesImportBatchSize))
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
            foreach (var chunk in ImportBatchingHelper.Chunk(sitesToInsert, ImportConstants.SitesImportBatchSize))
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

    private Site BuildSiteFromValidatedRow(
        SitesImportRowValidator.ValidatedSitesRow data,
        int sourceRowNumber,
        string? userEmail,
        out WarningImportRowRecord? locationWarning)
    {
        var now = DateTime.UtcNow;
        var auditUser = AuditUserFormatter.Format(userEmail);
        var locationResult = _locationNormalizer.Normalize(data.Location);
        locationWarning = CreateLocationWarning(
            data.NormalizedDomain,
            data.Location,
            sourceRowNumber,
            locationResult);

        var site = new Site
        {
            Domain = data.NormalizedDomain,
            DR = data.DR,
            Traffic = data.Traffic,
            Location = locationResult.RawValue ?? string.Empty,
            LocationKey = locationResult.LocationKey,
            ImportedLocationRaw = locationResult.RawValue,
            NumberDFLinks = data.NumberDFLinks,
            Language = data.Language,
            Niche = data.Niche,
            NicheTokens = NicheNormalizer.NormalizeTokens(data.Niche),
            Categories = data.Categories,
            SponsoredTag = data.SponsoredTag,
            IsQuarantined = false,
            QuarantineReason = null,
            QuarantineUpdatedAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedBy = auditUser,
            UpdatedBy = auditUser
        };

        foreach (var priceOption in data.PriceOptions)
        {
            site.PriceOptions.Add(new SitePriceOption
            {
                SiteDomain = data.NormalizedDomain,
                PriceType = priceOption.PriceType,
                TermKey = priceOption.TermKey,
                TermType = priceOption.TermType,
                TermValue = priceOption.TermValue,
                TermUnit = priceOption.TermUnit,
                AmountUsd = priceOption.AmountUsd,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        foreach (var availability in data.ServiceAvailabilities)
        {
            site.ServiceAvailabilities.Add(new SiteServiceAvailability
            {
                SiteDomain = data.NormalizedDomain,
                ServiceType = availability.ServiceType,
                Status = availability.Status,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        return site;
    }

    private static WarningImportRowRecord? CreateLocationWarning(
        string normalizedDomain,
        string rawLocation,
        int sourceRowNumber,
        LocationNormalizationResult locationResult)
    {
        if (locationResult.Status != LocationNormalizationStatus.Unmapped)
        {
            return null;
        }

        return new WarningImportRowRecord
        {
            Domain = normalizedDomain,
            Field = ImportConstants.SitesImportColumns.Location,
            RawValue = rawLocation,
            SourceRowNumber = sourceRowNumber,
            Warning = "Location was saved as Other because the value could not be mapped."
        };
    }

    private void AttachSummaryAndDownloads(
        SitesImportResult result,
        InvalidRowsImportArtifactPayload invalidRowsPayload,
        WarningRowsImportArtifactPayload warningRowsPayload,
        string importType,
        int invalidRowsCount,
        int skippedExistingCount,
        IReadOnlyCollection<string> duplicateDomainsInOrder)
    {
        result.SkippedExistingCount = skippedExistingCount;
        result.InvalidRowsCount = invalidRowsCount;
        result.SavedWithWarningsCount = warningRowsPayload.Rows.Count;
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

        ImportDownloadItem? warningRowsDownload = null;
        if (warningRowsPayload.Rows.Count > 0)
        {
            var handle = _importArtifactStorageService.StoreWarningRows(importType, warningRowsPayload);
            warningRowsDownload = new ImportDownloadItem
            {
                Available = true,
                Token = handle.Token,
                FileName = handle.FileName
            };
        }

        result.Downloads = new ImportDownloadsInfo
        {
            InvalidRows = invalidRowsDownload,
            WarningRows = warningRowsDownload
        };
    }
}
