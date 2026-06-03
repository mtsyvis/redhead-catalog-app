using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Exports;
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
    private readonly ISitesExcelExportGenerator _excelExportGenerator;

    public ExportService(
        ApplicationDbContext context,
        ISitesQueryBuilder queryBuilder,
        IEffectiveExportPolicyService effectiveExportPolicyService,
        ISitesExcelExportGenerator excelExportGenerator)
    {
        _context = context;
        _queryBuilder = queryBuilder;
        _effectiveExportPolicyService = effectiveExportPolicyService;
        _excelExportGenerator = excelExportGenerator;
    }

    public async Task<ExportResult> ExportSitesAsExcelAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        CancellationToken cancellationToken = default)
    {
        SitesExportColumnRegistry.ValidateRequestedColumns(visibleColumnKeys, userRole);
        var policy = await _effectiveExportPolicyService.GetEffectivePolicyAsync(
            userId,
            userRole,
            cancellationToken);

        if (policy.Mode == ExportLimitMode.Disabled)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        var sitesQuery = _queryBuilder.BuildQuery(
            _context.Sites.AsNoTracking().Include(site => site.CanonicalLocation),
            query);

        var requestedRows = await sitesQuery.CountAsync(cancellationToken);

        if (policy.Mode == ExportLimitMode.Limited && policy.Rows.HasValue)
        {
            sitesQuery = sitesQuery.Take(policy.Rows.Value);
        }

        var sites = await sitesQuery.ToListAsync(cancellationToken);

        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;
        var stream = _excelExportGenerator.Generate(new SitesExcelExportRequest(
            sites,
            [],
            visibleColumnKeys,
            userRole,
            userEmail,
            userRole,
            requestedRows,
            exportedRows,
            truncated,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            false));

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
        SitesExportColumnRegistry.ValidateRequestedColumns(visibleColumnKeys, userRole);
        if (StopListParser.HasAnyInput(query.StopListDomains))
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
            .AsNoTracking()
            .Include(site => site.CanonicalLocation)
            .Where(s => parseResult.UniqueDomains.Contains(s.Domain));

        // "Not found" must mean "not present in DB", not "not included due to export limit".
        // Compute matched domains from the base query BEFORE applying policy limits.
        var matchedDomains = await baseQuery
            .Select(s => s.Domain)
            .ToListAsync(cancellationToken);
        var multiSearchFoundCount = matchedDomains.Count;
        var matchedSet = new HashSet<string>(matchedDomains, StringComparer.Ordinal);
        var notFound = parseResult.UniqueDomains
            .Where(d => !matchedSet.Contains(d))
            .ToList();

        var filteredQuery = _queryBuilder.BuildQuery(baseQuery, query);

        var requestedRows = await filteredQuery.CountAsync(cancellationToken);

        var sites = await GetMultiSearchExportSitesAsync(
            filteredQuery,
            parseResult.UniqueDomains,
            query,
            policy,
            cancellationToken);

        var rowsReturned = sites.Count + notFound.Count;
        var exportedRows = sites.Count;
        var truncated = policy.Mode == ExportLimitMode.Limited && requestedRows > exportedRows;
        var stream = _excelExportGenerator.Generate(new SitesExcelExportRequest(
            sites,
            notFound,
            visibleColumnKeys,
            userRole,
            userEmail,
            userRole,
            requestedRows,
            exportedRows,
            truncated,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            notFound.Count > 0));

        var searchContext = isClientRole
            ? ExportAnalyticsSnapshotBuilder.CreateMultiSearchContext(
                parseResult.InputCount,
                parseResult.UniqueDomains.Count,
                multiSearchFoundCount)
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

    private static async Task<List<Site>> GetMultiSearchExportSitesAsync(
        IQueryable<Site> filteredQuery,
        IReadOnlyList<string> inputDomains,
        SitesQuery query,
        EffectiveExportPolicy policy,
        CancellationToken cancellationToken)
    {
        if (UsesMultiSearchInputOrder(query))
        {
            var inputOrder = inputDomains
                .Select((domain, index) => new { domain, index })
                .ToDictionary(item => item.domain, item => item.index, StringComparer.Ordinal);

            IEnumerable<Site> orderedSites = (await filteredQuery.ToListAsync(cancellationToken))
                .OrderBy(site => inputOrder.GetValueOrDefault(site.Domain, int.MaxValue));

            if (policy.Mode == ExportLimitMode.Limited && policy.Rows.HasValue)
            {
                orderedSites = orderedSites.Take(policy.Rows.Value);
            }

            return orderedSites.ToList();
        }

        var limitedQuery = policy.Mode == ExportLimitMode.Limited && policy.Rows.HasValue
            ? filteredQuery.Take(policy.Rows.Value)
            : filteredQuery;

        return await limitedQuery.ToListAsync(cancellationToken);
    }

    private static bool UsesMultiSearchInputOrder(SitesQuery query)
        => string.IsNullOrWhiteSpace(query.SortBy) ||
           string.Equals(query.SortBy, SortFields.Domain, StringComparison.OrdinalIgnoreCase);

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
            query.ExcludedLocationKeys,
            query.IncludeUnknownLocation,
            query.IncludeOtherLocation,
            query.Languages,
            query.Niches,
            query.CategorySearchTerms,
            query.TopicFitMode,
            query.ExcludedNiches,
            query.ExcludedCategorySearchTerms,
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
