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
    private const int MaxErrorDetails = 200;
    private const int MaxDuplicateDetails = 200;

    private static readonly IReadOnlyDictionary<string, int> ColumnIndexes =
        ImportConstants.SitesImportRequiredColumnOrder
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SitesImportService> _logger;

    public SitesImportService(
        ApplicationDbContext context,
        ILogger<SitesImportService> logger)
    {
        _context = context;
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
            fileName,
            userId,
            userEmail);

        if (!IsCsvFile(fileName, contentType))
        {
            _logger.LogWarning(
                "Sites import rejected: unsupported file type. FileName={FileName}, ContentType={ContentType}",
                fileName,
                contentType);

            return SitesImportResult.UnsupportedFileType();
        }

        var result = new SitesImportResult();

        // Phase 1: parse CSV rows and build per-domain map (last valid row wins)
        var parsedSitesByDomain = new Dictionary<string, Site>(StringComparer.Ordinal);

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.SitesImportRequiredColumnOrder,
                         requiredHeadersInStrictOrder: ImportConstants.SitesImportRequiredColumnOrder,
                         ct: cancellationToken))
        {
            await foreach (var row in ReadRowsAsync(session.Csv, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var validation = ValidateAndMapRow(row);
                if (validation.Error is not null)
                {
                    AddError(result, validation.Error);
                    continue;
                }

                if (validation.Site is null)
                {
                    continue;
                }

                var domain = validation.Site.Domain;

                if (parsedSitesByDomain.ContainsKey(domain))
                {
                    AddDuplicate(result, domain);
                }

                parsedSitesByDomain[domain] = validation.Site;
            }
        }

        // Nothing to insert, but still persist import log for traceability.
        if (parsedSitesByDomain.Count == 0)
        {
            await SaveImportLogOnlyAsync(result, userId, userEmail, cancellationToken);

            _logger.LogInformation(
                "Sites import completed with no valid rows. Inserted={Inserted}, Duplicates={Duplicates}, Errors={Errors}, UserId={UserId}",
                result.Inserted,
                result.DuplicatesCount,
                result.ErrorsCount,
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
                AddDuplicate(result, domain);
                continue;
            }

            sitesToInsert.Add(site);
        }

        // Phase 3: insert new sites in batches inside a single transaction
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await InsertSitesInBatchesAsync(sitesToInsert, result, cancellationToken);
            AddImportLog(result, userId, userEmail);

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
            result.Inserted,
            result.DuplicatesCount,
            result.ErrorsCount,
            userId);

        return result;
    }

    private static bool IsCsvFile(string fileName, string? contentType)
        => CsvImportHelper.IsCsvExtension(fileName) || CsvImportHelper.IsCsvContentType(contentType);

    private static async IAsyncEnumerable<SitesImportRowDto> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Row 1 = header
        var rowNumber = 1;

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            yield return SitesImportRowMapper.Map(
                columnName => csv.GetField(ColumnIndexes[columnName])?.Trim(),
                rowNumber);
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

                result.Inserted += chunk.Count;

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
        string userId,
        string userEmail,
        CancellationToken cancellationToken)
    {
        AddImportLog(result, userId, userEmail);
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    private void AddImportLog(
        SitesImportResult result,
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
            Inserted = result.Inserted,
            Duplicates = result.DuplicatesCount,
            Matched = 0,
            Unmatched = 0,
            ErrorsCount = result.ErrorsCount
        };

        _context.ImportLogs.Add(log);
    }

    private static RowValidationResult ValidateAndMapRow(SitesImportRowDto row)
    {
        if (IsEmptyRow(row))
        {
            return RowValidationResult.Skip();
        }

        if (string.IsNullOrWhiteSpace(row.Domain))
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Domain is required."
            });
        }

        var domain = DomainNormalizer.Normalize(row.Domain);
        if (string.IsNullOrEmpty(domain))
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Domain could not be normalized."
            });
        }

        if (row.PriceUsd is null || row.PriceUsd < 0)
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Price USD is required and must be >= 0."
            });
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
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Traffic is required and must be >= 0."
            });
        }

        if (row.PriceCasino is not null && row.PriceCasino < 0)
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "PriceCasino must be >= 0 or empty."
            });
        }

        if (row.PriceCrypto is not null && row.PriceCrypto < 0)
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "PriceCrypto must be >= 0 or empty."
            });
        }

        if (row.PriceLinkInsert is not null && row.PriceLinkInsert < 0)
        {
            return RowValidationResult.Fail(new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "PriceLinkInsert must be >= 0 or empty."
            });
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

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private static void AddError(SitesImportResult result, SitesImportError error)
    {
        result.ErrorsCount++;

        if (result.Errors.Count < MaxErrorDetails)
        {
            result.Errors.Add(error);
        }
    }

    private static void AddDuplicate(SitesImportResult result, string domain)
    {
        result.DuplicatesCount++;

        if (result.Duplicates.Count < MaxDuplicateDetails)
        {
            result.Duplicates.Add(domain);
        }
    }

    private sealed record RowValidationResult(Site? Site, SitesImportError? Error)
    {
        public static RowValidationResult Ok(Site site) => new(site, null);
        public static RowValidationResult Fail(SitesImportError error) => new(null, error);
        public static RowValidationResult Skip() => new(null, null);
    }
}
