using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Application.Services.UpdateImport;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;

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
    private readonly ILocationNormalizer _locationNormalizer;

    public SitesUpdateImportService(
        ApplicationDbContext context,
        ILogger<SitesUpdateImportService> logger,
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
        var warningRowsPayload = new WarningRowsImportArtifactPayload();
        var validRowsByDomain = new Dictionary<string, List<UnmatchedImportRowRecord>>(StringComparer.Ordinal);
        var duplicateDomainOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateDomainsInOrder = new List<string>();

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.SitesUpdateImportBaseColumns,
                         validateHeader: SitesUpdateImportHeaderValidator.ValidateOrThrow,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();
            unmatchedRowsPayload.Headers = session.Header.ToArray();
            var updateHeaderInfo = SitesUpdateImportHeaderValidator.Parse(session.Header);
            var presentColumns = SitesUpdateImportHeaderValidator.BuildPresentColumnSet(session.Header);

            await foreach (var (row, rawValues) in SitesImportCsvRowReader.ReadRowsAsync(session.Csv, session.Header.Length, updateHeaderInfo, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (isEmpty, error, update) = SitesUpdateImportRowValidator.Validate(row, presentColumns);
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

                if (update is null)
                {
                    continue;
                }

                update = update with
                {
                    SourceRowNumber = row.RowNumber,
                    RawValues = rawValues.ToList()
                };

                update = NormalizeLocationIfPresent(update);

                AddUpdateCandidate(updateCandidatesByDomain, update, ref duplicateRowsCount);
                AddValidSourceRow(validRowsByDomain, update.NormalizedDomain, row.RowNumber, rawValues);
            }
        }

        var sitesByDomain = await LoadSitesByDomainAsync(updateCandidatesByDomain.Keys.ToList(), cancellationToken);
        var now = DateTime.UtcNow;
        var auditUser = AuditUserFormatter.Format(userEmail);

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

            foreach (var pricingWarning in CreatePricingWarnings(site, update))
            {
                warningRowsPayload.Rows.Add(pricingWarning);
            }

            SitesUpdateImportApplier.Apply(site, update, now);
            site.UpdatedAtUtc = now;
            site.UpdatedBy = auditUser;
            result.UpdatedCount++;

            var warning = CreateLocationWarning(update);
            if (warning is not null)
            {
                warningRowsPayload.Rows.Add(warning);
            }
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
            warningRowsPayload,
            ImportConstants.ImportArtifactSlugSitesUpdate,
            invalidRowsCount,
            duplicateDomainsInOrder);
        return result;
    }

    private SitesUpdateImportRow NormalizeLocationIfPresent(SitesUpdateImportRow update)
    {
        if (!update.PresentColumns.Contains(ImportConstants.SitesImportColumns.Location))
        {
            return update;
        }

        var locationResult = _locationNormalizer.Normalize(update.Location);

        return update with
        {
            Location = locationResult.RawValue ?? string.Empty,
            LocationKey = locationResult.LocationKey,
            ImportedLocationRaw = locationResult.RawValue,
            LocationWarningDetails = locationResult.Status == LocationNormalizationStatus.Unmapped
                ? "Unmapped location. Site was saved with Location = Other."
                : null
        };
    }

    private async Task<Dictionary<string, Site>> LoadSitesByDomainAsync(
        List<string> domains,
        CancellationToken cancellationToken)
    {
        var sitesByDomain = new Dictionary<string, Site>(StringComparer.Ordinal);

        foreach (var chunk in Chunk(domains, BatchSize))
        {
            var sites = await _context.Sites
                .Include(s => s.PriceOptions)
                .Include(s => s.ServiceAvailabilities)
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

    private static WarningImportRowRecord? CreateLocationWarning(SitesUpdateImportRow update)
    {
        if (string.IsNullOrWhiteSpace(update.LocationWarningDetails))
        {
            return null;
        }

        return new WarningImportRowRecord
        {
            Domain = update.NormalizedDomain,
            Field = ImportConstants.SitesImportColumns.Location,
            RawValue = update.ImportedLocationRaw ?? string.Empty,
            SourceRowNumber = update.SourceRowNumber,
            Warning = update.LocationWarningDetails
        };
    }

    private static IEnumerable<WarningImportRowRecord> CreatePricingWarnings(Site site, SitesUpdateImportRow update)
    {
        var serviceTypesClearedByAvailability = update.AvailabilityOperations
            .Select(operation => operation.ServiceType)
            .ToHashSet();

        foreach (var availabilityOperation in update.AvailabilityOperations)
        {
            if (site.PriceOptions.Any(price => price.PriceType == availabilityOperation.ServiceType))
            {
                yield return new WarningImportRowRecord
                {
                    Domain = update.NormalizedDomain,
                    Field = availabilityOperation.Header,
                    RawValue = availabilityOperation.RawValue ?? string.Empty,
                    SourceRowNumber = update.SourceRowNumber,
                    Warning = $"{FormatPriceTypeLabel(availabilityOperation.ServiceType)} status was set to {FormatAvailabilityStatus(availabilityOperation.Status)} and existing {FormatPriceTypeLabel(availabilityOperation.ServiceType)} prices were cleared."
                };
            }
        }

        foreach (var priceOperation in update.PriceOperations)
        {
            if (priceOperation.AmountUsd.HasValue
                || serviceTypesClearedByAvailability.Contains(priceOperation.PriceType))
            {
                continue;
            }

            if (site.PriceOptions.Any(price =>
                    price.PriceType == priceOperation.PriceType
                    && string.Equals(price.TermKey, priceOperation.TermKey, StringComparison.Ordinal)))
            {
                yield return new WarningImportRowRecord
                {
                    Domain = update.NormalizedDomain,
                    Field = priceOperation.Header,
                    RawValue = priceOperation.RawValue ?? string.Empty,
                    SourceRowNumber = update.SourceRowNumber,
                    Warning = "Existing price was cleared because the imported cell was empty."
                };
            }
        }
    }

    private static string FormatAvailabilityStatus(ServiceAvailabilityStatus status)
        => status switch
        {
            ServiceAvailabilityStatus.Unknown => "Unknown",
            ServiceAvailabilityStatus.AvailableWithUnknownPrice => "AvailableWithUnknownPrice",
            ServiceAvailabilityStatus.NotAvailable => "NotAvailable",
            ServiceAvailabilityStatus.Available => "Available",
            _ => status.ToString()
        };

    private static string FormatPriceTypeLabel(PriceType priceType)
        => priceType switch
        {
            PriceType.Casino => "Casino",
            PriceType.Crypto => "Crypto",
            PriceType.LinkInsertion => "Link Insert",
            PriceType.LinkInsertionCasino => "Link Insert Casino",
            PriceType.Dating => "Dating",
            PriceType.Main => "Main",
            _ => priceType.ToString()
        };

    private void AttachSummaryAndDownloads(
        SitesUpdateImportResult result,
        InvalidRowsImportArtifactPayload invalidRowsPayload,
        UnmatchedRowsImportArtifactPayload unmatchedRowsPayload,
        WarningRowsImportArtifactPayload warningRowsPayload,
        string importType,
        int invalidRowsCount,
        IReadOnlyCollection<string> duplicateDomainsInOrder)
    {
        result.InvalidRowsCount = invalidRowsCount;
        result.UnmatchedRowsCount = unmatchedRowsPayload.Rows.Count;
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
            UnmatchedRows = unmatchedRowsDownload,
            WarningRows = warningRowsDownload
        };
    }
}
