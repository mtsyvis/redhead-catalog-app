using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing export operations
/// </summary>
public class ExportService : IExportService
{
    /// <summary>Number of CSV columns after Domain for not-found rows (placeholder "-").</summary>
    private const int NotFoundPlaceholderColumnCount = 14;

    private readonly ApplicationDbContext _context;
    private readonly ISitesQueryBuilder _queryBuilder;

    public ExportService(ApplicationDbContext context, ISitesQueryBuilder queryBuilder)
    {
        _context = context;
        _queryBuilder = queryBuilder;
    }

    public async Task<Stream> ExportSitesAsCsvAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        // Get role settings to enforce export limit
        var roleSettings = await _context.RoleSettings
            .FirstOrDefaultAsync(rs => rs.RoleName == userRole, cancellationToken);

        if (roleSettings == null)
        {
            throw new RoleSettingsNotFoundException(userRole);
        }

        // Check if export is disabled for this role
        if (roleSettings.ExportLimitRows == ExportConstants.DisabledLimit)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        // Build filtered and sorted query using the query builder
        var sitesQuery = _queryBuilder.BuildQuery(_context.Sites, query);

        // Apply role-based limit
        sitesQuery = sitesQuery.Take(roleSettings.ExportLimitRows);

        // Execute query
        var sites = await sitesQuery.ToListAsync(cancellationToken);

        // Generate CSV
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, Encoding.UTF8);
        var csvWriter = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        // Write CSV headers and data
        csvWriter.WriteRecords(sites);
        await csvWriter.FlushAsync();
        await writer.FlushAsync();
        stream.Position = 0;

        // Log export operation
        await LogExportAsync(userId, userEmail, userRole, sites.Count, query, cancellationToken);

        return stream;
    }

    public async Task<Stream> ExportMultiSearchAsCsvAsync(
        string queryText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var parseResult = MultiSearchParser.Parse(queryText);

        var roleSettings = await _context.RoleSettings
            .FirstOrDefaultAsync(rs => rs.RoleName == userRole, cancellationToken);

        if (roleSettings == null)
        {
            throw new RoleSettingsNotFoundException(userRole);
        }

        if (roleSettings.ExportLimitRows == ExportConstants.DisabledLimit)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        var includeNotFound = !AreFiltersActive(query);

        IQueryable<Site> baseQuery = _context.Sites
            .Where(s => parseResult.UniqueDomains.Contains(s.Domain));

        // IMPORTANT:
        // "Not found" must mean "not present in DB", not "not included due to export limit".
        // So we compute matched domains from the base query (domain IN list) BEFORE applying role limits.
        List<string> notFound = new();
        if (includeNotFound)
        {
            var matchedDomains = await baseQuery
                .Select(s => s.Domain)
                .ToListAsync(cancellationToken);
            var matchedSet = new HashSet<string>(matchedDomains, StringComparer.Ordinal);
            notFound = parseResult.UniqueDomains
                .Where(d => !matchedSet.Contains(d))
                .ToList();
        }

        var filteredQuery = _queryBuilder.BuildQuery(baseQuery, query);
        var limitedQuery = filteredQuery.Take(roleSettings.ExportLimitRows);
        var sites = await limitedQuery.ToListAsync(cancellationToken);

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, Encoding.UTF8);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        var csvWriter = new CsvWriter(writer, csvConfig);

        csvWriter.WriteRecords(sites);

        if (includeNotFound)
        {
            foreach (var domain in notFound)
            {
                csvWriter.WriteField(domain);
                for (var i = 0; i < NotFoundPlaceholderColumnCount; i++)
                {
                    csvWriter.WriteField("-");
                }
                csvWriter.NextRecord();
            }
        }

        await csvWriter.FlushAsync();
        await writer.FlushAsync();
        stream.Position = 0;

        var rowsReturned = sites.Count + (includeNotFound ? notFound.Count : 0);
        await LogExportAsync(userId, userEmail, userRole, rowsReturned, query, cancellationToken);

        return stream;
    }

    private async Task LogExportAsync(
        string userId,
        string userEmail,
        string userRole,
        int rowsReturned,
        SitesQuery query,
        CancellationToken cancellationToken)
    {
        var filterSummary = new
        {
            query.Search,
            query.DrMin,
            query.DrMax,
            query.TrafficMin,
            query.TrafficMax,
            query.PriceMin,
            query.PriceMax,
            query.Locations,
            query.CasinoAllowed,
            query.CryptoAllowed,
            query.LinkInsertAllowed,
            query.Quarantine
        };

        var exportLog = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Role = userRole,
            TimestampUtc = DateTime.UtcNow,
            RowsReturned = rowsReturned,
            FilterSummaryJson = JsonSerializer.Serialize(filterSummary)
        };

        _context.ExportLogs.Add(exportLog);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// True when any filter differs from defaults (same definition as UI: range, location, allowed flags, quarantine).
    /// </summary>
    private static bool AreFiltersActive(SitesQuery query)
    {
        if (query.DrMin.HasValue || query.DrMax.HasValue) { return true; }
        if (query.TrafficMin.HasValue || query.TrafficMax.HasValue) { return true; }
        if (query.PriceMin.HasValue || query.PriceMax.HasValue) { return true; }
        if (query.Locations is { Count: > 0 }) { return true; }
        if (query.CasinoAllowed == true || query.CryptoAllowed == true || query.LinkInsertAllowed == true) { return true; }
        if (!string.IsNullOrEmpty(query.Quarantine) &&
            !string.Equals(query.Quarantine, QuarantineFilterValues.All, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
