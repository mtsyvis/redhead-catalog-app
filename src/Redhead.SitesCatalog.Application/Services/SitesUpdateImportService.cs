using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Parsers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
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

    private static readonly IReadOnlyDictionary<string, int> ColumnIndexes =
        ImportConstants.SitesImportRequiredColumnOrder
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SitesUpdateImportService> _logger;

    /// <summary>
    /// Parsed update for one domain. Domain is lookup key only.
    /// </summary>
    private sealed record ParsedSiteUpdate(
        string NormalizedDomain,
        double DR,
        long Traffic,
        string Location,
        decimal PriceUsd,
        decimal? PriceCasino,
        decimal? PriceCrypto,
        decimal? PriceLinkInsert,
        string? Niche,
        string? Categories);

    public SitesUpdateImportService(
        ApplicationDbContext context,
        ILogger<SitesUpdateImportService> logger)
    {
        _context = context;
        _logger = logger;
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

        if (!CsvImportHelper.IsCsvExtension(fileName) && !CsvImportHelper.IsCsvContentType(contentType))
        {
            _logger.LogWarning(
                "Sites update import rejected: not CSV. FileName={FileName}, ContentType={ContentType}",
                fileName,
                contentType);

            return new SitesUpdateImportResult
            {
                ErrorsCount = 1,
                Errors =
                {
                    new SitesUpdateImportError { RowNumber = 0, Message = "Unsupported file type. Use CSV." }
                }
            };
        }

        var result = new SitesUpdateImportResult();

        // Phase 1: parse CSV, validate, build per-domain map (only valid rows enter; last valid wins)
        var updatesByDomain = new Dictionary<string, ParsedSiteUpdate>(StringComparer.Ordinal);

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.SitesImportRequiredColumnOrder,
                         requiredHeadersInStrictOrder: ImportConstants.SitesImportRequiredColumnOrder,
                         ct: cancellationToken))
        {
            await foreach (var row in ReadRowsAsync(session.Csv, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (isEmpty, error, validatedData) = SitesImportRowValidationHelper.Validate(row);
                if (error is not null)
                {
                    AddError(result, error);
                    continue;
                }

                if (isEmpty || validatedData is null)
                {
                    continue;
                }

                var domain = validatedData.NormalizedDomain;
                var update = ToParsedSiteUpdate(validatedData);

                if (updatesByDomain.ContainsKey(domain))
                {
                    result.DuplicatesCount++;
                    if (result.Duplicates.Count < ImportConstants.SitesImportMaxDetailDuplicates)
                    {
                        result.Duplicates.Add(domain);
                    }
                }

                updatesByDomain[domain] = update;
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
                result.Unmatched.Add(domain);
                continue;
            }

            site.DR = update.DR;
            site.Traffic = update.Traffic;
            site.Location = update.Location;
            site.PriceUsd = update.PriceUsd;
            site.PriceCasino = update.PriceCasino;
            site.PriceCrypto = update.PriceCrypto;
            site.PriceLinkInsert = update.PriceLinkInsert;
            site.Niche = update.Niche;
            site.Categories = update.Categories;
            site.UpdatedAtUtc = now;

            result.Matched++;
        }

        AddImportLog(result, userId, userEmail);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Sites update import completed. Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}, UserId={UserId}",
            result.Matched,
            result.Unmatched.Count,
            result.ErrorsCount,
            result.DuplicatesCount,
            userId);

        return result;
    }

    private static async IAsyncEnumerable<SitesImportRowDto> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
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

    private static ParsedSiteUpdate ToParsedSiteUpdate(SitesImportRowValidationHelper.ValidatedSitesRow data)
    {
        return new ParsedSiteUpdate(
            data.NormalizedDomain,
            data.DR,
            data.Traffic,
            data.Location,
            data.PriceUsd,
            data.PriceCasino,
            data.PriceCrypto,
            data.PriceLinkInsert,
            data.Niche,
            data.Categories);
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private static void AddError(SitesUpdateImportResult result, SitesImportError error)
    {
        result.ErrorsCount++;
        if (result.Errors.Count < ImportConstants.SitesImportMaxDetailErrors)
        {
            result.Errors.Add(new SitesUpdateImportError
            {
                RowNumber = error.RowNumber,
                Message = error.Message
            });
        }
    }

    private void AddImportLog(SitesUpdateImportResult result, string userId, string userEmail)
    {
        var log = new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeSitesUpdate,
            TimestampUtc = DateTime.UtcNow,
            Inserted = 0,
            Duplicates = result.DuplicatesCount,
            Matched = result.Matched,
            Unmatched = result.Unmatched.Count,
            ErrorsCount = result.ErrorsCount
        };

        _context.ImportLogs.Add(log);
    }
}
