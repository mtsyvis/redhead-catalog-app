using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain;
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
    private readonly ApplicationDbContext _context;
    private readonly ISitesQueryBuilder _queryBuilder;
    private readonly IEffectiveExportPolicyService _effectiveExportPolicyService;

    public ExportService(
        ApplicationDbContext context,
        ISitesQueryBuilder queryBuilder,
        IEffectiveExportPolicyService effectiveExportPolicyService)
    {
        _context = context;
        _queryBuilder = queryBuilder;
        _effectiveExportPolicyService = effectiveExportPolicyService;
    }

    public async Task<ExportResult> ExportSitesAsExcelAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        CancellationToken cancellationToken = default)
    {
        var exportColumns = SitesExportColumnRegistry.ValidateRequestedColumns(visibleColumnKeys, userRole);
        var policy = await _effectiveExportPolicyService.GetEffectivePolicyAsync(
            userId,
            userRole,
            cancellationToken);

        if (policy.Mode == ExportLimitMode.Disabled)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        var sitesQuery = _queryBuilder.BuildQuery(_context.Sites.Include(site => site.CanonicalLocation), query);

        var requestedRows = await sitesQuery.CountAsync(cancellationToken);

        if (policy.Mode == ExportLimitMode.Limited && policy.Rows.HasValue)
        {
            sitesQuery = sitesQuery.Take(policy.Rows.Value);
        }

        var sites = await sitesQuery.ToListAsync(cancellationToken);

        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;
        var stream = CreateWorkbook(
            sites,
            notFoundDomains: [],
            exportColumns,
            userEmail,
            userRole,
            query,
            requestedRows,
            exportedRows,
            truncated,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            notFoundIncluded: false);

        await LogExportAsync(userId, userEmail, userRole, sites.Count, query, searchContext: null, cancellationToken);

        return new ExportResult(
            FileStream: stream,
            RequestedRows: requestedRows,
            ExportedRows: exportedRows,
            Truncated: truncated,
            LimitRows: policy.Mode == ExportLimitMode.Limited ? policy.Rows : null);
    }

    public Task<ExportResult> ExportSitesAsExcelAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default)
        => ExportSitesAsExcelAsync(
            query,
            userId,
            userEmail,
            userRole,
            SitesExportColumnRegistry.GetDefaultColumnKeysForRole(userRole),
            cancellationToken);

    public async Task<ExportResult> ExportMultiSearchAsExcelAsync(
        string searchText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        CancellationToken cancellationToken = default)
    {
        var exportColumns = SitesExportColumnRegistry.ValidateRequestedColumns(visibleColumnKeys, userRole);
        if (query.StopListDomains is { Count: > 0 })
        {
            throw new RequestValidationException(StopListConstants.MultiSearchNotSupportedMessage);
        }

        var parseResult = MultiSearchParser.Parse(searchText);

        var policy = await _effectiveExportPolicyService.GetEffectivePolicyAsync(
            userId,
            userRole,
            cancellationToken);

        if (policy.Mode == ExportLimitMode.Disabled)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        var isClientRole = string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal);

        IQueryable<Site> baseQuery = _context.Sites
            .Include(site => site.CanonicalLocation)
            .Where(s => parseResult.UniqueDomains.Contains(s.Domain));

        var includeNotFound = !AreFiltersActive(query);

        // "Not found" must mean "not present in DB", not "not included due to export limit".
        // Compute matched domains from the base query BEFORE applying policy limits.
        List<string> notFound = new();
        int? multiSearchFoundCount = null;
        if (includeNotFound || isClientRole)
        {
            var matchedDomains = await baseQuery
                .Select(s => s.Domain)
                .ToListAsync(cancellationToken);
            multiSearchFoundCount = matchedDomains.Count;

            if (includeNotFound)
            {
                var matchedSet = new HashSet<string>(matchedDomains, StringComparer.Ordinal);
                notFound = parseResult.UniqueDomains
                    .Where(d => !matchedSet.Contains(d))
                    .ToList();
            }
        }

        var filteredQuery = _queryBuilder.BuildQuery(baseQuery, query);

        var requestedRows = await filteredQuery.CountAsync(cancellationToken);

        var limitedQuery = policy.Mode == ExportLimitMode.Limited && policy.Rows.HasValue
            ? filteredQuery.Take(policy.Rows.Value)
            : filteredQuery;
        var sites = await limitedQuery.ToListAsync(cancellationToken);

        var rowsReturned = sites.Count + (includeNotFound ? notFound.Count : 0);
        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;
        var stream = CreateWorkbook(
            sites,
            includeNotFound ? notFound : [],
            exportColumns,
            userEmail,
            userRole,
            query,
            requestedRows,
            exportedRows,
            truncated,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            notFoundIncluded: includeNotFound);

        var searchContext = isClientRole
            ? ExportAnalyticsSnapshotBuilder.CreateMultiSearchContext(
                parseResult.InputCount,
                parseResult.UniqueDomains.Count,
                multiSearchFoundCount.GetValueOrDefault())
            : null;

        await LogExportAsync(userId, userEmail, userRole, rowsReturned, query, searchContext, cancellationToken);

        return new ExportResult(
            FileStream: stream,
            RequestedRows: requestedRows,
            ExportedRows: exportedRows,
            Truncated: truncated,
            LimitRows: policy.Mode == ExportLimitMode.Limited ? policy.Rows : null);
    }

    public Task<ExportResult> ExportMultiSearchAsExcelAsync(
        string searchText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default)
        => ExportMultiSearchAsExcelAsync(
            searchText,
            query,
            userId,
            userEmail,
            userRole,
            SitesExportColumnRegistry.GetDefaultColumnKeysForRole(userRole),
            cancellationToken);

    private async Task LogExportAsync(
        string userId,
        string userEmail,
        string userRole,
        int rowsReturned,
        SitesQuery query,
        ExportAnalyticsSearchContext? searchContext,
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

        if (string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal))
        {
            _context.ExportAnalyticsSnapshots.Add(
                ExportAnalyticsSnapshotBuilder.Create(exportLog, query, searchContext));
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// True when any filter differs from defaults (same definition as UI: range, location, availability, quarantine).
    /// </summary>
    private static bool AreFiltersActive(SitesQuery query)
    {
        if (query.DrMin.HasValue || query.DrMax.HasValue) { return true; }
        if (query.TrafficMin.HasValue || query.TrafficMax.HasValue) { return true; }
        if (query.PriceMin.HasValue || query.PriceMax.HasValue) { return true; }
        if (query.Locations is { Count: > 0 }) { return true; }
        if (query.LocationKeys is { Count: > 0 }) { return true; }
        if (query.LocationGroupKeys is { Count: > 0 }) { return true; }
        if (query.IncludeUnknownLocation || query.IncludeOtherLocation) { return true; }
        if (query.Languages is { Count: > 0 }) { return true; }
        if (NicheNormalizer.NormalizeTokens(query.Niches ?? []).Length > 0) { return true; }
        if (CategorySearchTermParser.NormalizeAndValidate(query.CategorySearchTerms) is { Count: > 0 }) { return true; }
        if (query.CasinoAvailability.HasValue)
        {
            if (query.CasinoAvailability.Value != ServiceAvailabilityFilter.All) { return true; }
        }

        if (query.CryptoAvailability.HasValue)
        {
            if (query.CryptoAvailability.Value != ServiceAvailabilityFilter.All) { return true; }
        }

        if (query.LinkInsertAvailability.HasValue)
        {
            if (query.LinkInsertAvailability.Value != ServiceAvailabilityFilter.All) { return true; }
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

    private static MemoryStream CreateWorkbook(
        IReadOnlyList<Site> sites,
        IReadOnlyList<string> notFoundDomains,
        IReadOnlyList<SitesExportColumnDefinition> siteColumns,
        string userEmail,
        string userRole,
        SitesQuery query,
        int requestedRows,
        int exportedRows,
        bool truncated,
        int? limitRows,
        bool notFoundIncluded)
    {
        var sheets = new List<XlsxSheet>
        {
            new(
                "Sites",
                siteColumns.Select(column => column.Header).ToArray(),
                sites.Select(site => CreateSiteRow(site, siteColumns)).ToList(),
                siteColumns.Select(column => column.Width).ToArray())
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

    private static IReadOnlyList<XlsxCell> CreateSiteRow(
        Site site,
        IReadOnlyList<SitesExportColumnDefinition> siteColumns)
        => siteColumns.Select(column => column.CreateCell(site)).ToList();

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
            query.StopListDomains,
            query.DrMin,
            query.DrMax,
            query.TrafficMin,
            query.TrafficMax,
            query.PriceMin,
            query.PriceMax,
            query.Locations,
            query.LocationKeys,
            query.LocationGroupKeys,
            query.IncludeUnknownLocation,
            query.IncludeOtherLocation,
            query.Languages,
            query.Niches,
            query.CategorySearchTerms,
            query.CasinoAvailability,
            query.CryptoAvailability,
            query.LinkInsertAvailability,
            query.LinkInsertCasinoAvailability,
            query.DatingAvailability,
            query.Quarantine,
            query.LastPublishedFrom,
            query.LastPublishedToExclusive
        };

}
