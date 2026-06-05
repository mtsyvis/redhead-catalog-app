using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Domain;
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
    private readonly IExportUsageLimitService _exportUsageLimitService;
    private readonly ISitesExcelExportGenerator _excelExportGenerator;

    public ExportService(
        ApplicationDbContext context,
        ISitesQueryBuilder queryBuilder,
        IEffectiveExportPolicyService effectiveExportPolicyService,
        IExportUsageLimitService exportUsageLimitService,
        ISitesExcelExportGenerator excelExportGenerator)
    {
        _context = context;
        _queryBuilder = queryBuilder;
        _effectiveExportPolicyService = effectiveExportPolicyService;
        _exportUsageLimitService = exportUsageLimitService;
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
        var preparedExport = await PrepareSitesExportAsync(
            query,
            userId,
            userEmail,
            userRole,
            visibleColumnKeys,
            ExportConstants.DestinationDownload,
            cancellationToken);

        return await CompletePreparedExportAsync(preparedExport, cancellationToken);
    }

    public async Task<PreparedExportResult> PrepareSitesExportAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        string destination,
        CancellationToken cancellationToken = default)
    {
        var visibleExportColumns = SitesExportColumnRegistry.ValidateRequestedColumns(visibleColumnKeys, userRole);
        var policy = await GetEnabledPolicyAsync(userId, userRole, cancellationToken);

        var sitesQuery = _queryBuilder.BuildQuery(
            _context.Sites.AsNoTracking().Include(site => site.CanonicalLocation),
            query);

        var requestedRows = await sitesQuery.CountAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;

        var candidateSites = await ApplyRowsPerExportLimit(sitesQuery, policy)
            .ToListAsync(cancellationToken);
        var usageEvaluation = await EvaluateUsageLimitsAsync(
            userId,
            userRole,
            policy,
            candidateSites,
            nowUtc,
            cancellationToken);

        await ThrowIfUsageLimitBlockedAsync(
            userId: userId,
            userEmail: userEmail,
            userRole: userRole,
            requestedRows: requestedRows,
            query: query,
            visibleExportColumns: visibleExportColumns,
            policy: policy,
            usageEvaluation: usageEvaluation,
            destination: destination,
            exportMode: ExportConstants.ExportModeSites,
            searchContext: null,
            timestampUtc: nowUtc,
            cancellationToken: cancellationToken);

        var sites = ApplyAllowedDomains(candidateSites, usageEvaluation);
        return CreatePreparedExportResult(
            sites: sites,
            notFoundDomains: [],
            visibleColumnKeys: visibleColumnKeys,
            userId: userId,
            userEmail: userEmail,
            userRole: userRole,
            rowsReturned: sites.Count,
            requestedRows: requestedRows,
            truncated: requestedRows > sites.Count,
            policy: policy,
            usageEvaluation: usageEvaluation,
            destination: destination,
            exportMode: ExportConstants.ExportModeSites,
            query: query,
            visibleExportColumns: visibleExportColumns,
            searchContext: null,
            timestampUtc: nowUtc);
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
        var preparedExport = await PrepareMultiSearchExportAsync(
            searchText,
            query,
            userId,
            userEmail,
            userRole,
            visibleColumnKeys,
            ExportConstants.DestinationDownload,
            cancellationToken);

        return await CompletePreparedExportAsync(preparedExport, cancellationToken);
    }

    public async Task<PreparedExportResult> PrepareMultiSearchExportAsync(
        string searchText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        string destination,
        CancellationToken cancellationToken = default)
    {
        var visibleExportColumns = SitesExportColumnRegistry.ValidateRequestedColumns(visibleColumnKeys, userRole);
        if (StopListParser.HasAnyInput(query.StopListDomains))
        {
            throw new RequestValidationException(StopListConstants.MultiSearchNotSupportedMessage);
        }

        var parseResult = MultiSearchParser.Parse(searchText);
        var policy = await GetEnabledPolicyAsync(userId, userRole, cancellationToken);

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
        var nowUtc = DateTime.UtcNow;

        var candidateSites = await GetMultiSearchExportSitesAsync(
            filteredQuery,
            parseResult.UniqueDomains,
            query,
            policy,
            cancellationToken);

        var usageEvaluation = await EvaluateUsageLimitsAsync(
            userId,
            userRole,
            policy,
            candidateSites,
            nowUtc,
            cancellationToken);

        var searchContext = isClientRole
            ? ExportAnalyticsSnapshotBuilder.CreateMultiSearchContext(
                parseResult.InputCount,
                parseResult.UniqueDomains.Count,
                multiSearchFoundCount)
            : null;

        await ThrowIfUsageLimitBlockedAsync(
            userId: userId,
            userEmail: userEmail,
            userRole: userRole,
            requestedRows: requestedRows,
            query: query,
            visibleExportColumns: visibleExportColumns,
            policy: policy,
            usageEvaluation: usageEvaluation,
            destination: destination,
            exportMode: ExportConstants.ExportModeMultiSearch,
            searchContext: searchContext,
            timestampUtc: nowUtc,
            cancellationToken: cancellationToken);

        var sites = ApplyAllowedDomains(candidateSites, usageEvaluation);
        return CreatePreparedExportResult(
            sites: sites,
            notFoundDomains: notFound,
            visibleColumnKeys: visibleColumnKeys,
            userId: userId,
            userEmail: userEmail,
            userRole: userRole,
            rowsReturned: sites.Count + notFound.Count,
            requestedRows: requestedRows,
            truncated: requestedRows > sites.Count,
            policy: policy,
            usageEvaluation: usageEvaluation,
            destination: destination,
            exportMode: ExportConstants.ExportModeMultiSearch,
            query: query,
            visibleExportColumns: visibleExportColumns,
            searchContext: searchContext,
            timestampUtc: nowUtc);
    }

    private PreparedExportResult CreatePreparedExportResult(
        IReadOnlyList<Site> sites,
        IReadOnlyList<string> notFoundDomains,
        IReadOnlyList<string> visibleColumnKeys,
        string userId,
        string userEmail,
        string userRole,
        int rowsReturned,
        int requestedRows,
        bool truncated,
        EffectiveExportPolicy policy,
        ExportUsageLimitEvaluation usageEvaluation,
        string destination,
        string exportMode,
        SitesQuery query,
        IReadOnlyList<SitesExportColumnDefinition> visibleExportColumns,
        ExportAnalyticsSearchContext? searchContext,
        DateTime timestampUtc)
    {
        var exportedRows = sites.Count;
        var stream = GenerateWorkbook(
            sites,
            notFoundDomains,
            visibleColumnKeys,
            userEmail,
            userRole,
            requestedRows,
            exportedRows,
            truncated,
            policy,
            usageEvaluation);

        var pendingLog = CreatePendingExportLog(
            userId,
            userEmail,
            userRole,
            rowsReturned,
            requestedRows,
            exportedRows,
            truncated,
            policy,
            usageEvaluation,
            destination,
            exportMode,
            query,
            visibleExportColumns,
            searchContext,
            sites.Select(site => site.Domain).ToArray(),
            timestampUtc);

        return new PreparedExportResult(
            FileStream: stream,
            RequestedRows: requestedRows,
            ExportedRows: exportedRows,
            Truncated: truncated,
            LimitRows: GetRowsPerExportLimit(policy),
            TruncationReason: usageEvaluation.TruncationReason,
            ExportLog: pendingLog.ExportLog,
            AnalyticsSnapshot: pendingLog.AnalyticsSnapshot,
            ExportedDomainAccesses: pendingLog.ExportedDomainAccesses);
    }

    private MemoryStream GenerateWorkbook(
        IReadOnlyList<Site> sites,
        IReadOnlyList<string> notFoundDomains,
        IReadOnlyList<string> visibleColumnKeys,
        string userEmail,
        string userRole,
        int requestedRows,
        int exportedRows,
        bool truncated,
        EffectiveExportPolicy policy,
        ExportUsageLimitEvaluation usageEvaluation)
    {
        return _excelExportGenerator.Generate(new SitesExcelExportRequest(
            sites,
            notFoundDomains,
            visibleColumnKeys,
            userRole,
            userEmail,
            userRole,
            requestedRows,
            exportedRows,
            truncated,
            GetRowsPerExportLimit(policy),
            notFoundDomains.Count > 0,
            usageEvaluation.TruncationReason));
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

    private async Task<EffectiveExportPolicy> GetEnabledPolicyAsync(
        string userId,
        string userRole,
        CancellationToken cancellationToken)
    {
        var policy = await _effectiveExportPolicyService.GetEffectivePolicyAsync(
            userId,
            userRole,
            cancellationToken);

        if (policy.Mode == ExportLimitMode.Disabled)
        {
            throw new ExportDisabledException(userRole, ExportConstants.ExportDisabledMessage);
        }

        return policy;
    }

    private async Task<ExportUsageLimitEvaluation> EvaluateUsageLimitsAsync(
        string userId,
        string userRole,
        EffectiveExportPolicy policy,
        IReadOnlyList<Site> candidateSites,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        return await _exportUsageLimitService.EvaluateAsync(
            userId,
            userRole,
            policy,
            candidateSites.Select(site => site.Domain).ToArray(),
            nowUtc,
            cancellationToken);
    }

    private async Task ThrowIfUsageLimitBlockedAsync(
        string userId,
        string userEmail,
        string userRole,
        int requestedRows,
        SitesQuery query,
        IReadOnlyList<SitesExportColumnDefinition> visibleExportColumns,
        EffectiveExportPolicy policy,
        ExportUsageLimitEvaluation usageEvaluation,
        string destination,
        string exportMode,
        ExportAnalyticsSearchContext? searchContext,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        if (!usageEvaluation.IsBlocked)
        {
            return;
        }

        await LogBlockedExportAsync(
            userId,
            userEmail,
            userRole,
            requestedRows,
            query,
            visibleExportColumns,
            policy,
            usageEvaluation,
            destination,
            exportMode,
            searchContext,
            timestampUtc,
            cancellationToken);

        throw new ExportUsageLimitExceededException(usageEvaluation.BlockedReason!);
    }

    private static IQueryable<Site> ApplyRowsPerExportLimit(
        IQueryable<Site> query,
        EffectiveExportPolicy policy)
    {
        var limitRows = GetRowsPerExportLimit(policy);
        return limitRows.HasValue
            ? query.Take(limitRows.Value)
            : query;
    }

    private static int? GetRowsPerExportLimit(EffectiveExportPolicy policy)
        => policy.Mode == ExportLimitMode.Limited ? policy.Rows : null;

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
            var limitRows = GetRowsPerExportLimit(policy);

            IEnumerable<Site> orderedSites = (await filteredQuery.ToListAsync(cancellationToken))
                .OrderBy(site => inputOrder.GetValueOrDefault(site.Domain, int.MaxValue));

            if (limitRows.HasValue)
            {
                orderedSites = orderedSites.Take(limitRows.Value);
            }

            return orderedSites.ToList();
        }

        return await ApplyRowsPerExportLimit(filteredQuery, policy)
            .ToListAsync(cancellationToken);
    }

    private static bool UsesMultiSearchInputOrder(SitesQuery query)
        => string.IsNullOrWhiteSpace(query.SortBy) ||
           string.Equals(query.SortBy, SortFields.Domain, StringComparison.OrdinalIgnoreCase);

    public async Task<ExportResult> CompletePreparedExportAsync(
        PreparedExportResult preparedExport,
        CancellationToken cancellationToken = default)
    {
        AddPreparedExportArtifacts(preparedExport);

        await _context.SaveChangesAsync(cancellationToken);

        return preparedExport.ToExportResult();
    }

    private void AddPreparedExportArtifacts(PreparedExportResult preparedExport)
    {
        _context.ExportLogs.Add(preparedExport.ExportLog);

        if (preparedExport.AnalyticsSnapshot != null)
        {
            _context.ExportAnalyticsSnapshots.Add(preparedExport.AnalyticsSnapshot);
        }

        if (preparedExport.ExportedDomainAccesses.Count > 0)
        {
            _context.ExportedDomainAccesses.AddRange(preparedExport.ExportedDomainAccesses);
        }
    }

    private static List<Site> ApplyAllowedDomains(
        List<Site> candidateSites,
        ExportUsageLimitEvaluation usageEvaluation)
    {
        if (!usageEvaluation.Applies)
        {
            return candidateSites;
        }

        var allowedDomains = usageEvaluation.AllowedDomains.ToHashSet(StringComparer.Ordinal);
        return candidateSites
            .Where(site => allowedDomains.Contains(site.Domain))
            .ToList();
    }

    private async Task LogBlockedExportAsync(
        string userId,
        string userEmail,
        string userRole,
        int requestedRows,
        SitesQuery query,
        IReadOnlyList<SitesExportColumnDefinition> visibleExportColumns,
        EffectiveExportPolicy policy,
        ExportUsageLimitEvaluation usageEvaluation,
        string destination,
        string exportMode,
        ExportAnalyticsSearchContext? searchContext,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        var pendingLog = CreatePendingExportLog(
            userId,
            userEmail,
            userRole,
            rowsReturned: 0,
            requestedRows,
            exportedRows: 0,
            truncated: false,
            policy,
            usageEvaluation,
            destination,
            exportMode,
            query,
            visibleExportColumns,
            searchContext,
            exportedDomains: [],
            timestampUtc);

        pendingLog.ExportLog.BlockedReason = usageEvaluation.BlockedReason;

        AddExportLogDraft(pendingLog);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private void AddExportLogDraft(PendingExportLog pendingLog)
    {
        _context.ExportLogs.Add(pendingLog.ExportLog);

        if (pendingLog.AnalyticsSnapshot != null)
        {
            _context.ExportAnalyticsSnapshots.Add(pendingLog.AnalyticsSnapshot);
        }
    }

    private static PendingExportLog CreatePendingExportLog(
        string userId,
        string userEmail,
        string userRole,
        int rowsReturned,
        int requestedRows,
        int exportedRows,
        bool truncated,
        EffectiveExportPolicy policy,
        ExportUsageLimitEvaluation usageEvaluation,
        string destination,
        string exportMode,
        SitesQuery query,
        IReadOnlyList<SitesExportColumnDefinition> visibleExportColumns,
        ExportAnalyticsSearchContext? searchContext,
        IReadOnlyList<string> exportedDomains,
        DateTime timestampUtc)
    {
        var exportLog = CreateExportLog(
            userId,
            userEmail,
            userRole,
            rowsReturned,
            requestedRows,
            exportedRows,
            truncated,
            policy,
            usageEvaluation,
            destination,
            exportMode,
            query,
            visibleExportColumns,
            timestampUtc);

        return new PendingExportLog(
            exportLog,
            CreateAnalyticsSnapshot(exportLog, userRole, query, searchContext),
            CreateExportedDomainAccesses(exportLog.Id, userId, exportedDomains, timestampUtc));
    }

    private static ExportLog CreateExportLog(
        string userId,
        string userEmail,
        string userRole,
        int rowsReturned,
        int requestedRows,
        int exportedRows,
        bool truncated,
        EffectiveExportPolicy policy,
        ExportUsageLimitEvaluation usageEvaluation,
        string destination,
        string exportMode,
        SitesQuery query,
        IReadOnlyList<SitesExportColumnDefinition> visibleExportColumns,
        DateTime timestampUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Role = userRole,
            TimestampUtc = timestampUtc,
            RowsReturned = rowsReturned,
            RequestedRowsCount = requestedRows,
            ExportedRowsCount = exportedRows,
            WasTruncated = truncated,
            ExportLimitRows = GetRowsPerExportLimit(policy),
            DailyUniqueExportedDomainsLimit = usageEvaluation.Usage.DailyUniqueExportedDomainsLimit,
            WeeklyUniqueExportedDomainsLimit = usageEvaluation.Usage.WeeklyUniqueExportedDomainsLimit,
            DailyExportOperationsLimit = usageEvaluation.Usage.DailyExportOperationsLimit,
            WeeklyExportOperationsLimit = usageEvaluation.Usage.WeeklyExportOperationsLimit,
            Destination = destination,
            ExportMode = exportMode
        };

    private static ExportAnalyticsSnapshot? CreateAnalyticsSnapshot(
        ExportLog exportLog,
        string userRole,
        SitesQuery query,
        ExportAnalyticsSearchContext? searchContext)
        => string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal)
            ? ExportAnalyticsSnapshotBuilder.Create(exportLog, query, searchContext)
            : null;

    private static IReadOnlyList<ExportedDomainAccess> CreateExportedDomainAccesses(
        Guid exportLogId,
        string userId,
        IReadOnlyList<string> exportedDomains,
        DateTime timestampUtc)
        => exportedDomains
            .Select(domain => new ExportedDomainAccess
            {
                Id = Guid.NewGuid(),
                ExportLogId = exportLogId,
                UserId = userId,
                Domain = domain,
                ExportedAtUtc = timestampUtc
            })
            .ToArray();

    private sealed record PendingExportLog(
        ExportLog ExportLog,
        ExportAnalyticsSnapshot? AnalyticsSnapshot,
        IReadOnlyList<ExportedDomainAccess> ExportedDomainAccesses);
}
