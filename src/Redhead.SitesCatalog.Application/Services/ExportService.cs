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
        var stream = _excelExportGenerator.Generate(new SitesExcelExportRequest(
            sites,
            includeNotFound ? notFound : [],
            visibleColumnKeys,
            userRole,
            userEmail,
            userRole,
            requestedRows,
            exportedRows,
            truncated,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            includeNotFound));

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
        if (query.ExcludedLocationKeys is { Count: > 0 }) { return true; }
        if (query.IncludeUnknownLocation || query.IncludeOtherLocation) { return true; }
        if (query.Languages is { Count: > 0 }) { return true; }
        if (NicheNormalizer.NormalizeTokens(query.Niches ?? []).Length > 0) { return true; }
        if (CategorySearchTermParser.NormalizeAndValidate(query.CategorySearchTerms) is { Count: > 0 }) { return true; }
        if (IsAvailabilityFilterActive(query.CasinoAvailability)) { return true; }
        if (IsAvailabilityFilterActive(query.CryptoAvailability)) { return true; }
        if (IsAvailabilityFilterActive(query.LinkInsertAvailability)) { return true; }
        if (IsAvailabilityFilterActive(query.LinkInsertCasinoAvailability)) { return true; }
        if (IsAvailabilityFilterActive(query.DatingAvailability)) { return true; }
        if (!string.IsNullOrEmpty(query.Quarantine) &&
            !string.Equals(query.Quarantine, QuarantineFilterValues.All, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsAvailabilityFilterActive(IReadOnlyCollection<ServiceAvailabilityStatus>? availability)
        => availability is { Count: > 0 };

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
