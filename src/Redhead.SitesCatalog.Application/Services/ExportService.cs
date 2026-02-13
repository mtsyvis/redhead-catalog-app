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
}
