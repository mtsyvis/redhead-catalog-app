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
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing export operations
/// </summary>
public class ExportService : IExportService
{
    private static readonly string[] ClientExportHeaders =
    [
        "Domain",
        "DR",
        "Traffic",
        "Location",
        "PriceUsd",
        "PriceCasino",
        "PriceCrypto",
        "PriceLinkInsert",
        "Niche",
        "Categories",
        "LinkType",
        "SponsoredTag",
    ];

    private static readonly string[] NonClientExportHeaders =
    [
        "Domain",
        "DR",
        "Traffic",
        "Location",
        "PriceUsd",
        "PriceCasino",
        "PriceCrypto",
        "PriceLinkInsert",
        "Niche",
        "Categories",
        "LinkType",
        "SponsoredTag",
        "IsQuarantined",
        "QuarantineReason",
        "LastPublishedDate",
        "CreatedAtUtc",
        "UpdatedAtUtc"
    ];

    private readonly ApplicationDbContext _context;
    private readonly ISitesQueryBuilder _queryBuilder;

    public ExportService(ApplicationDbContext context, ISitesQueryBuilder queryBuilder)
    {
        _context = context;
        _queryBuilder = queryBuilder;
    }

    public async Task<ExportResult> ExportSitesAsCsvAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var roleSettings = await _context.RoleSettings
            .FirstOrDefaultAsync(rs => rs.RoleName == userRole, cancellationToken);

        if (roleSettings == null)
        {
            throw new RoleSettingsNotFoundException(userRole);
        }

        var user = await _context.Users.FindAsync(new object?[] { userId }, cancellationToken);
        var policy = EffectiveExportPolicyResolver.Resolve(userRole, roleSettings, user);

        if (policy.Mode == ExportLimitMode.Disabled)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        var sitesQuery = _queryBuilder.BuildQuery(_context.Sites, query);

        var requestedRows = await sitesQuery.CountAsync(cancellationToken);

        if (policy.Mode == ExportLimitMode.Limited && policy.Rows.HasValue)
        {
            sitesQuery = sitesQuery.Take(policy.Rows.Value);
        }

        var sites = await sitesQuery.ToListAsync(cancellationToken);

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, Encoding.UTF8);
        var csvWriter = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        var isClientRole = string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal);
        WriteHeaders(csvWriter, isClientRole);
        csvWriter.NextRecord();
        foreach (var site in sites)
        {
            WriteSiteRow(csvWriter, site, isClientRole);
            csvWriter.NextRecord();
        }
        await csvWriter.FlushAsync();
        await writer.FlushAsync();
        stream.Position = 0;

        await LogExportAsync(userId, userEmail, userRole, sites.Count, query, cancellationToken);

        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;

        return new ExportResult(
            CsvStream: stream,
            RequestedRows: requestedRows,
            ExportedRows: exportedRows,
            Truncated: truncated,
            LimitRows: policy.Mode == ExportLimitMode.Limited ? policy.Rows : null);
    }

    public async Task<ExportResult> ExportMultiSearchAsCsvAsync(
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

        var user = await _context.Users.FindAsync(new object?[] { userId }, cancellationToken);
        var policy = EffectiveExportPolicyResolver.Resolve(userRole, roleSettings, user);

        if (policy.Mode == ExportLimitMode.Disabled)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        IQueryable<Site> baseQuery = _context.Sites
            .Where(s => parseResult.UniqueDomains.Contains(s.Domain));

        var includeNotFound = !AreFiltersActive(query);

        // "Not found" must mean "not present in DB", not "not included due to export limit".
        // Compute matched domains from the base query BEFORE applying policy limits.
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

        var requestedRows = await filteredQuery.CountAsync(cancellationToken);

        var limitedQuery = policy.Mode == ExportLimitMode.Limited && policy.Rows.HasValue
            ? filteredQuery.Take(policy.Rows.Value)
            : filteredQuery;
        var sites = await limitedQuery.ToListAsync(cancellationToken);

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, Encoding.UTF8);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        var csvWriter = new CsvWriter(writer, csvConfig);
        var isClientRole = string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal);

        WriteHeaders(csvWriter, isClientRole);
        csvWriter.NextRecord();
        foreach (var site in sites)
        {
            WriteSiteRow(csvWriter, site, isClientRole);
            csvWriter.NextRecord();
        }

        if (includeNotFound)
        {
            var placeholderColumnCount = GetHeaders(isClientRole).Length - 1;
            foreach (var domain in notFound)
            {
                csvWriter.WriteField(domain);
                for (var i = 0; i < placeholderColumnCount; i++)
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

        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;

        return new ExportResult(
            CsvStream: stream,
            RequestedRows: requestedRows,
            ExportedRows: exportedRows,
            Truncated: truncated,
            LimitRows: policy.Mode == ExportLimitMode.Limited ? policy.Rows : null);
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
            query.CasinoAvailability,
            query.CryptoAvailability,
            query.LinkInsertAvailability,
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
        if (query.CasinoAvailability.HasValue)
        {
            if (query.CasinoAvailability.Value != ServiceAvailabilityFilter.All) { return true; }
        }
        else if (query.CasinoAllowed == true)
        {
            return true;
        }

        if (query.CryptoAvailability.HasValue)
        {
            if (query.CryptoAvailability.Value != ServiceAvailabilityFilter.All) { return true; }
        }
        else if (query.CryptoAllowed == true)
        {
            return true;
        }

        if (query.LinkInsertAvailability.HasValue)
        {
            if (query.LinkInsertAvailability.Value != ServiceAvailabilityFilter.All) { return true; }
        }
        else if (query.LinkInsertAllowed == true)
        {
            return true;
        }
        if (!string.IsNullOrEmpty(query.Quarantine) &&
            !string.Equals(query.Quarantine, QuarantineFilterValues.All, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string[] GetHeaders(bool isClientRole)
    {
        return isClientRole ? ClientExportHeaders : NonClientExportHeaders;
    }

    private static void WriteHeaders(CsvWriter csvWriter, bool isClientRole)
    {
        foreach (var header in GetHeaders(isClientRole))
        {
            csvWriter.WriteField(header);
        }
    }

    private static void WriteSiteRow(CsvWriter csvWriter, Site site, bool isClientRole)
    {
        csvWriter.WriteField(site.Domain);
        csvWriter.WriteField(site.DR);
        csvWriter.WriteField(site.Traffic);
        csvWriter.WriteField(site.Location);
        csvWriter.WriteField(site.PriceUsd.HasValue
            ? site.PriceUsd.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty);
        csvWriter.WriteField(FormatOptionalService(site.PriceCasino, site.PriceCasinoStatus));
        csvWriter.WriteField(FormatOptionalService(site.PriceCrypto, site.PriceCryptoStatus));
        csvWriter.WriteField(FormatOptionalService(site.PriceLinkInsert, site.PriceLinkInsertStatus));
        csvWriter.WriteField(site.Niche);
        csvWriter.WriteField(site.Categories);
        csvWriter.WriteField(site.LinkType);
        csvWriter.WriteField(site.SponsoredTag);

        if (isClientRole)
        {
            return;
        }

        csvWriter.WriteField(site.IsQuarantined);
        csvWriter.WriteField(site.QuarantineReason);
        csvWriter.WriteField(site.LastPublishedDate);
        csvWriter.WriteField(site.CreatedAtUtc);
        csvWriter.WriteField(site.UpdatedAtUtc);
    }

    private static string FormatOptionalService(decimal? price, ServiceAvailabilityStatus status)
    {
        return status switch
        {
            ServiceAvailabilityStatus.Available when price.HasValue => price.Value.ToString(CultureInfo.InvariantCulture),
            ServiceAvailabilityStatus.NotAvailable => "NO",
            _ => string.Empty
        };
    }

}
