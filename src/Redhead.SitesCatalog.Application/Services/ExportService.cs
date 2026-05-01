using System.Globalization;
using System.Text.Json;
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
        "PriceLinkInsertCasino",
        "PriceDating",
        "Niche",
        "Categories",
        "NumberDFLinks",
        "SponsoredTag",
        "Term",
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
        "PriceLinkInsertCasino",
        "PriceDating",
        "Niche",
        "Categories",
        "NumberDFLinks",
        "SponsoredTag",
        "Term",
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

    public async Task<ExportResult> ExportSitesAsExcelAsync(
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

        var isClientRole = string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal);
        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;
        var stream = CreateWorkbook(
            sites,
            notFoundDomains: [],
            isClientRole,
            userEmail,
            userRole,
            query,
            requestedRows,
            exportedRows,
            truncated,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            notFoundIncluded: false);

        await LogExportAsync(userId, userEmail, userRole, sites.Count, query, cancellationToken);

        return new ExportResult(
            FileStream: stream,
            RequestedRows: requestedRows,
            ExportedRows: exportedRows,
            Truncated: truncated,
            LimitRows: policy.Mode == ExportLimitMode.Limited ? policy.Rows : null);
    }

    public async Task<ExportResult> ExportMultiSearchAsExcelAsync(
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

        var isClientRole = string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal);
        var rowsReturned = sites.Count + (includeNotFound ? notFound.Count : 0);
        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;
        var stream = CreateWorkbook(
            sites,
            includeNotFound ? notFound : [],
            isClientRole,
            userEmail,
            userRole,
            query,
            requestedRows,
            exportedRows,
            truncated,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            notFoundIncluded: includeNotFound);

        await LogExportAsync(userId, userEmail, userRole, rowsReturned, query, cancellationToken);

        return new ExportResult(
            FileStream: stream,
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
        var exportLog = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Role = userRole,
            TimestampUtc = DateTime.UtcNow,
            RowsReturned = rowsReturned,
            FilterSummaryJson = JsonSerializer.Serialize(CreateFilterSummary(query))
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
        if (query.LinkInsertCasinoAvailability.HasValue &&
            query.LinkInsertCasinoAvailability.Value != ServiceAvailabilityFilter.All)
        {
            return true;
        }
        if (query.DatingAvailability.HasValue &&
            query.DatingAvailability.Value != ServiceAvailabilityFilter.All)
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

    private static MemoryStream CreateWorkbook(
        IReadOnlyList<Site> sites,
        IReadOnlyList<string> notFoundDomains,
        bool isClientRole,
        string userEmail,
        string userRole,
        SitesQuery query,
        int requestedRows,
        int exportedRows,
        bool truncated,
        int? limitRows,
        bool notFoundIncluded)
    {
        var siteHeaders = GetHeaders(isClientRole);
        var sheets = new List<XlsxSheet>
        {
            new(
                "Sites",
                siteHeaders,
                sites.Select(site => CreateSiteRow(site, isClientRole)).ToList(),
                GetSiteColumnWidths(isClientRole))
        };

        if (notFoundDomains.Count > 0)
        {
            sheets.Add(new XlsxSheet(
                "Not found",
                ["Domain"],
                notFoundDomains.Select(domain => (IReadOnlyList<XlsxCell>)[XlsxCell.Text(domain)]).ToList(),
                [32d]));
        }

        sheets.Add(
            new(
                "Export info",
                ["Property", "Value"],
                CreateExportInfoRows(
                    userEmail,
                    userRole,
                    requestedRows,
                    exportedRows,
                    truncated,
                    limitRows,
                    notFoundDomains.Count,
                    notFoundIncluded),
                [28d, 80d],
                FreezeHeader: false,
                AutoFilter: false));

        return XlsxWorkbookWriter.CreateWorkbook(sheets);
    }

    private static IReadOnlyList<XlsxCell> CreateSiteRow(Site site, bool isClientRole)
    {
        var row = new List<XlsxCell>
        {
            XlsxCell.Text(site.Domain),
            XlsxCell.Number(Convert.ToDecimal(site.DR, CultureInfo.InvariantCulture), XlsxCellStyle.Integer),
            XlsxCell.Number(site.Traffic, XlsxCellStyle.Integer),
            XlsxCell.Text(site.Location),
            XlsxCell.Number(site.PriceUsd, XlsxCellStyle.Decimal),
            FormatOptionalService(site.PriceCasino, site.PriceCasinoStatus),
            FormatOptionalService(site.PriceCrypto, site.PriceCryptoStatus),
            FormatOptionalService(site.PriceLinkInsert, site.PriceLinkInsertStatus),
            FormatOptionalService(site.PriceLinkInsertCasino, site.PriceLinkInsertCasinoStatus),
            FormatOptionalService(site.PriceDating, site.PriceDatingStatus),
            XlsxCell.Text(site.Niche),
            XlsxCell.Text(site.Categories),
            XlsxCell.Number(site.NumberDFLinks, XlsxCellStyle.Integer),
            XlsxCell.Text(site.SponsoredTag),
            XlsxCell.Text(FormatTerm(site.TermType, site.TermValue, site.TermUnit))
        };

        if (isClientRole)
        {
            return row;
        }

        row.Add(XlsxCell.Boolean(site.IsQuarantined));
        row.Add(XlsxCell.Text(site.QuarantineReason));
        row.Add(XlsxCell.Date(site.LastPublishedDate));
        row.Add(XlsxCell.DateTime(site.CreatedAtUtc));
        row.Add(XlsxCell.DateTime(site.UpdatedAtUtc));
        return row;
    }

    private static XlsxCell FormatOptionalService(decimal? price, ServiceAvailabilityStatus status)
    {
        return status switch
        {
            ServiceAvailabilityStatus.Available when price.HasValue => XlsxCell.Number(price, XlsxCellStyle.Decimal),
            ServiceAvailabilityStatus.NotAvailable => XlsxCell.Text("NO"),
            _ => XlsxCell.Blank()
        };
    }

    private static IReadOnlyList<double> GetSiteColumnWidths(bool isClientRole)
    {
        var widths = new List<double>
        {
            28,
            8,
            14,
            14,
            12,
            16,
            16,
            18,
            24,
            16,
            20,
            28,
            16,
            16,
            16
        };

        if (!isClientRole)
        {
            widths.AddRange([14, 28, 18, 20, 20]);
        }

        return widths;
    }

    private static IReadOnlyList<IReadOnlyList<XlsxCell>> CreateExportInfoRows(
        string userEmail,
        string userRole,
        int requestedRows,
        int exportedRows,
        bool truncated,
        int? limitRows,
        int notFoundRows,
        bool notFoundIncluded)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            InfoRow("GeneratedAtUtc", XlsxCell.DateTime(DateTime.UtcNow)),
            InfoRow("GeneratedBy", XlsxCell.Text(userEmail)),
            InfoRow("Role", XlsxCell.Text(userRole)),
            InfoRow("Rows matching export request", XlsxCell.Number(requestedRows, XlsxCellStyle.Integer)),
            InfoRow("Rows in Sites sheet", XlsxCell.Number(exportedRows, XlsxCellStyle.Integer)),
            InfoRow("Export truncated by limit", XlsxCell.Boolean(truncated)),
            InfoRow("Export limit rows", limitRows.HasValue
                ? XlsxCell.Number(limitRows.Value, XlsxCellStyle.Integer)
                : XlsxCell.Text("Unlimited"))
        };

        if (notFoundRows > 0)
        {
            rows.Add(InfoRow("Not found sheet rows", XlsxCell.Number(notFoundRows, XlsxCellStyle.Integer)));
            rows.Add(InfoRow("Not found included", XlsxCell.Text(notFoundIncluded ? "Yes" : "No")));
        }

        return rows;
    }

    private static IReadOnlyList<XlsxCell> InfoRow(string label, XlsxCell value)
        => [XlsxCell.InfoLabel(label), value];

    private static object CreateFilterSummary(SitesQuery query)
        => new
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
            query.LinkInsertCasinoAvailability,
            query.DatingAvailability,
            query.Quarantine,
            query.LastPublishedFrom,
            query.LastPublishedToExclusive
        };

    private static string FormatTerm(TermType? termType, int? termValue, TermUnit? termUnit)
    {
        if (termType is null)
        {
            return string.Empty;
        }

        if (termType == TermType.Permanent)
        {
            return "permanent";
        }

        if (termType == TermType.Finite && termValue.HasValue && termUnit == TermUnit.Year)
        {
            return termValue.Value == 1 ? "1 year" : $"{termValue.Value} years";
        }

        return string.Empty;
    }

}
