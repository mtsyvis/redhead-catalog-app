using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for sites add-only import from CSV.
/// Designed to handle large files (e.g., 60k rows) efficiently.
/// </summary>
public class SitesImportService : ISitesImportService
{
    // Caps are important: returning tens of thousands of errors/duplicates will create huge JSON responses.
    private const int MaxErrorDetails = 200;
    private const int MaxDuplicateDetails = 200;

    private readonly ApplicationDbContext _context;
    private readonly IEnumerable<IImportFileParser> _parsers;
    private readonly ILogger<SitesImportService> _logger;

    public SitesImportService(
        ApplicationDbContext context,
        IEnumerable<IImportFileParser> parsers,
        ILogger<SitesImportService> logger)
    {
        _context = context;
        _parsers = parsers;
        _logger = logger;
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
            fileName, userId, userEmail);

        var parser = _parsers.FirstOrDefault(p => p.CanParse(fileName, contentType));
        if (parser == null)
        {
            _logger.LogWarning(
                "Sites import rejected: unsupported file type. FileName={FileName}, ContentType={ContentType}",
                fileName, contentType);
            return SitesImportResult.UnsupportedFileType();
        }

        var result = new SitesImportResult();
        var batch = new List<Site>(ImportConstants.SitesImportBatchSize);

        var existingDomains = await LoadExistingDomainsAsync(cancellationToken);

        await ProcessRowsAsync(parser, fileStream, result, existingDomains, batch, cancellationToken);

        await FlushRemainingBatchAsync(batch, result, cancellationToken);
        await SaveImportLogAsync(result, userId, userEmail, cancellationToken);

        _logger.LogInformation(
            "Sites import completed. Inserted={Inserted}, Duplicates={Duplicates}, Errors={Errors}, UserId={UserId}",
            result.Inserted, result.DuplicatesCount, result.ErrorsCount, userId);

        return result;
    }

    private async Task<HashSet<string>> LoadExistingDomainsAsync(CancellationToken cancellationToken)
    {
        var rawDomains = await _context.Sites
            .AsNoTracking()
            .Select(s => s.Domain)
            .ToListAsync(cancellationToken);

        return rawDomains
            .Select(DomainNormalizer.Normalize)
            .Where(d => !string.IsNullOrEmpty(d))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task ProcessRowsAsync(
        IImportFileParser parser,
        Stream fileStream,
        SitesImportResult result,
        HashSet<string> existingDomains,
        List<Site> batch,
        CancellationToken cancellationToken)
    {
        await foreach (var row in parser.ReadRowsAsync(fileStream, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validation = ValidateAndMapRow(row);
            if (validation.Error is not null)
            {
                result.ErrorsCount++;

                // Keep only first N details to avoid huge responses.
                if (result.Errors.Count < MaxErrorDetails)
                {
                    result.Errors.Add(validation.Error);
                }

                continue;
            }

            if (validation.Site is null)
            {
                continue;
            }

            var domain = validation.Site.Domain;
            if (existingDomains.Contains(domain))
            {
                result.DuplicatesCount++;

                // Keep only first N details to avoid huge responses.
                if (result.Duplicates.Count < MaxDuplicateDetails)
                {
                    result.Duplicates.Add(domain);
                }

                continue;
            }

            existingDomains.Add(domain);
            batch.Add(validation.Site);

            if (batch.Count >= ImportConstants.SitesImportBatchSize)
            {
                await FlushBatchAndAccumulateAsync(batch, result, cancellationToken);
            }
        }
    }

    private async Task FlushRemainingBatchAsync(
        List<Site> batch,
        SitesImportResult result,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        await FlushBatchAndAccumulateAsync(batch, result, cancellationToken);
    }

    private async Task FlushBatchAndAccumulateAsync(
        List<Site> batch,
        SitesImportResult result,
        CancellationToken cancellationToken)
    {
        // Performance: disable AutoDetectChanges for bulk inserts.
        var prevAutoDetect = _context.ChangeTracker.AutoDetectChangesEnabled;
        _context.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            await _context.Sites.AddRangeAsync(batch, cancellationToken);

            try
            {
                // No retry by design. If this throws, investigate the DB error (e.g., invalid data or concurrent import).
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Sites import batch insert failed. BatchSize={BatchSize}", batch.Count);
                throw;
            }

            result.Inserted += batch.Count;
        }
        finally
        {
            // Critical for large imports: prevent ChangeTracker from growing unbounded.
            _context.ChangeTracker.Clear();
            _context.ChangeTracker.AutoDetectChangesEnabled = prevAutoDetect;
            batch.Clear();
        }
    }

    private async Task SaveImportLogAsync(
        SitesImportResult result,
        string userId,
        string userEmail,
        CancellationToken cancellationToken)
    {
        var log = new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeSites,
            TimestampUtc = DateTime.UtcNow,
            Inserted = result.Inserted,
            Duplicates = result.DuplicatesCount,
            Matched = 0,
            Unmatched = 0,
            ErrorsCount = result.ErrorsCount
        };

        _context.ImportLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        // Keep the context clean for the next request.
        _context.ChangeTracker.Clear();
    }

    private static RowValidationResult ValidateAndMapRow(SitesImportRowDto row)
    {
        if (IsEmptyRow(row))
        {
            return RowValidationResult.Skip();
        }

        if (string.IsNullOrWhiteSpace(row.Domain))
        {
            return RowValidationResult.Fail(new SitesImportError { RowNumber = row.RowNumber, Message = "Domain is required." });
        }

        var domain = DomainNormalizer.Normalize(row.Domain);
        if (string.IsNullOrEmpty(domain))
        {
            return RowValidationResult.Fail(new SitesImportError { RowNumber = row.RowNumber, Message = "Domain could not be normalized." });
        }

        if (row.PriceUsd is null || row.PriceUsd < 0)
        {
            return RowValidationResult.Fail(new SitesImportError { RowNumber = row.RowNumber, Message = "Price USD is required and must be >= 0." });
        }

        if (string.IsNullOrWhiteSpace(row.DRRaw))
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "DR is required and must be between 0 and 100."
            });
        }

        if (row.DR is null)
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "DR",
                RawValue = row.DRRaw,
                Message = "Invalid numeric format for DR."
            });
        }

        if (row.DR is < 0 or > 100)
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "DR",
                RawValue = row.DRRaw,
                Message = "DR must be between 0 and 100."
            });
        }

        if (row.Traffic is null || row.Traffic < 0)
        {
            return RowValidationResult.Fail(new SitesImportError { RowNumber = row.RowNumber, Message = "Traffic is required and must be >= 0." });
        }

        if (row.PriceCasino is not null && row.PriceCasino < 0)
        {
            return RowValidationResult.Fail(new SitesImportError { RowNumber = row.RowNumber, Message = "PriceCasino must be >= 0 or empty." });
        }

        if (row.PriceCrypto is not null && row.PriceCrypto < 0)
        {
            return RowValidationResult.Fail(new SitesImportError { RowNumber = row.RowNumber, Message = "PriceCrypto must be >= 0 or empty." });
        }

        if (row.PriceLinkInsert is not null && row.PriceLinkInsert < 0)
        {
            return RowValidationResult.Fail(new SitesImportError { RowNumber = row.RowNumber, Message = "PriceLinkInsert must be >= 0 or empty." });
        }

        var site = BuildSiteFromRow(row, domain);
        return RowValidationResult.Ok(site);
    }

    private static bool IsEmptyRow(SitesImportRowDto row)
    {
        return string.IsNullOrWhiteSpace(row.Domain)
               && string.IsNullOrWhiteSpace(row.DRRaw)
               && row.Traffic is null
               && string.IsNullOrWhiteSpace(row.Location)
               && row.PriceUsd is null
               && row.PriceCasino is null
               && row.PriceCrypto is null
               && row.PriceLinkInsert is null
               && string.IsNullOrWhiteSpace(row.Niche)
               && string.IsNullOrWhiteSpace(row.Categories);
    }

    private static Site BuildSiteFromRow(SitesImportRowDto row, string domain)
    {
        var now = DateTime.UtcNow;
        return new Site
        {
            Domain = domain,
            DR = row.DR ?? 0,
            Traffic = row.Traffic ?? 0,
            Location = (row.Location ?? string.Empty).Trim(),
            PriceUsd = row.PriceUsd ?? 0,
            PriceCasino = row.PriceCasino,
            PriceCrypto = row.PriceCrypto,
            PriceLinkInsert = row.PriceLinkInsert,
            Niche = string.IsNullOrWhiteSpace(row.Niche) ? null : row.Niche.Trim(),
            Categories = string.IsNullOrWhiteSpace(row.Categories) ? null : row.Categories.Trim(),
            IsQuarantined = false,
            QuarantineReason = null,
            QuarantineUpdatedAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
