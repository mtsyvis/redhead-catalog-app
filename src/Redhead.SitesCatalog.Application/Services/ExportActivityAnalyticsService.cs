using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services.Analytics;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class ExportActivityAnalyticsService : IExportActivityAnalyticsService
{
    private readonly ApplicationDbContext _context;

    public ExportActivityAnalyticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExportActivityAnalyticsDto> GetExportActivityAsync(
        ExportActivityAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var filteredLogs = ApplyFilters(_context.ExportLogs.AsNoTracking(), query);
        var locationLookups = await LoadLocationLookupsAsync(cancellationToken);
        var clientUsers = await GetClientUsersAsync(cancellationToken);

        return new ExportActivityAnalyticsDto(
            Summary: await GetSummaryAsync(query, filteredLogs, cancellationToken),
            ExportsOverTime: await GetExportsOverTimeAsync(query, filteredLogs, cancellationToken),
            ClientSummaries: await GetClientSummariesAsync(filteredLogs, clientUsers, cancellationToken),
            RecentExports: await GetRecentExportsAsync(
                query,
                filteredLogs,
                locationLookups,
                clientUsers,
                cancellationToken));
    }

    public async Task<ExportLogDetailsDto?> GetExportLogDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.ExportLogs
            .AsNoTracking()
            .Where(log => log.Role == AppRoles.Client)
            .Where(log => log.Id == id)
            .Select(log => new ExportLogDetailsSource(
                log.Id,
                log.TimestampUtc,
                log.UserId,
                log.UserEmail,
                log.Destination,
                log.RequestedRowsCount,
                log.ExportedRowsCount,
                log.WasTruncated,
                log.ExportLimitRows,
                log.BlockedReason,
                log.ExportMode,
                log.AnalyticsSnapshot == null ? null : log.AnalyticsSnapshot.FiltersSnapshotJson,
                log.AnalyticsSnapshot == null ? null : log.AnalyticsSnapshot.SortSnapshotJson,
                log.AnalyticsSnapshot == null ? null : log.AnalyticsSnapshot.SearchSnapshotJson))
            .SingleOrDefaultAsync(cancellationToken);
        if (row == null)
        {
            return null;
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == row.UserId)
            .Select(candidate => new
            {
                candidate.Email,
                candidate.DisplayName
            })
            .SingleOrDefaultAsync(cancellationToken);
        var displayName = user == null
            ? null
            : string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName.Trim();

        return ExportLogDetailsMapper.Map(
            row,
            displayName,
            await LoadLocationLookupsAsync(cancellationToken));
    }

    private async Task<ExportActivitySummaryDto> GetSummaryAsync(
        ExportActivityAnalyticsQuery query,
        IQueryable<ExportLog> filteredLogs,
        CancellationToken cancellationToken)
    {
        var summary = await filteredLogs
            .GroupBy(_ => 1)
            .Select(group => new
            {
                CompletedExports = group.Count(log => log.BlockedReason == null),
                PartialExports = group.Count(log => log.BlockedReason == null && log.WasTruncated),
                BlockedExports = group.Count(log => log.BlockedReason != null),
                RequestedRows = group.Sum(log => (long)log.RequestedRowsCount),
                ExportedRows = group.Sum(log => (long)log.ExportedRowsCount)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var uniqueExportedDomains = await ApplyAccessFilters(
                _context.ExportedDomainAccesses.AsNoTracking(),
                query)
            .Select(access => access.Domain)
            .Distinct()
            .CountAsync(cancellationToken);

        return new ExportActivitySummaryDto(
            CompletedExports: summary?.CompletedExports ?? 0,
            PartialExports: summary?.PartialExports ?? 0,
            BlockedExports: summary?.BlockedExports ?? 0,
            UniqueExportedDomains: uniqueExportedDomains,
            RequestedRows: summary?.RequestedRows ?? 0,
            ExportedRows: summary?.ExportedRows ?? 0);
    }

    private async Task<IReadOnlyList<ExportActivityOverTimeDto>> GetExportsOverTimeAsync(
        ExportActivityAnalyticsQuery query,
        IQueryable<ExportLog> filteredLogs,
        CancellationToken cancellationToken)
    {
        var exportCounts = await filteredLogs
            .GroupBy(log => log.TimestampUtc.Date)
            .Select(group => new ExportActivityDailyLogCounts(
                group.Key,
                group.Count(log => log.BlockedReason == null && !log.WasTruncated),
                group.Count(log => log.BlockedReason == null && log.WasTruncated),
                group.Count(log => log.BlockedReason != null)))
            .ToListAsync(cancellationToken);

        var exportedDomainCounts = await ApplyAccessFilters(
                _context.ExportedDomainAccesses.AsNoTracking(),
                query)
            .GroupBy(access => access.ExportLog.TimestampUtc.Date)
            .Select(group => new ExportActivityDailyDomainCounts(
                group.Key,
                group.Select(access => access.Domain).Distinct().Count()))
            .ToListAsync(cancellationToken);

        var domainCountsByDate = exportedDomainCounts.ToDictionary(
            item => item.Date,
            item => item.ExportedDomains);

        return exportCounts
            .OrderBy(item => item.Date)
            .Select(item => new ExportActivityOverTimeDto(
                Date: item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                SuccessfulExports: item.SuccessfulExports,
                PartialExports: item.PartialExports,
                BlockedExports: item.BlockedExports,
                ExportedDomains: domainCountsByDate.GetValueOrDefault(item.Date)))
            .ToArray();
    }

    private async Task<IReadOnlyList<ExportActivityClientSummaryDto>> GetClientSummariesAsync(
        IQueryable<ExportLog> filteredLogs,
        IReadOnlyList<ApplicationUser> clientUsers,
        CancellationToken cancellationToken)
    {
        if (clientUsers.Count == 0)
        {
            return [];
        }

        var selectedSummaries = await filteredLogs
            .GroupBy(log => log.UserId)
            .Select(group => new ExportActivitySelectedClientSummary(
                group.Key,
                group.Count(log => log.BlockedReason == null && !log.WasTruncated),
                group.Count(log => log.BlockedReason == null && log.WasTruncated),
                group.Count(log => log.BlockedReason != null),
                group.Sum(log => (long)log.RequestedRowsCount),
                group.Sum(log => (long)log.ExportedRowsCount),
                group.Max(log => (DateTime?)log.TimestampUtc)))
            .ToListAsync(cancellationToken);
        var selectedSummariesByUserId = selectedSummaries.ToDictionary(item => item.UserId, StringComparer.Ordinal);

        var rows = new List<ExportActivityClientSummaryDto>();
        foreach (var user in clientUsers)
        {
            if (!selectedSummariesByUserId.TryGetValue(user.Id, out var selected))
            {
                continue;
            }

            rows.Add(new ExportActivityClientSummaryDto(
                UserId: user.Id,
                Email: user.Email ?? string.Empty,
                DisplayName: user.EffectiveDisplayName,
                SuccessfulExports: selected.SuccessfulExports,
                PartialExports: selected.PartialExports,
                BlockedExports: selected.BlockedExports,
                RequestedRows: selected.RequestedRows,
                ExportedRows: selected.ExportedRows,
                LastExportAtUtc: selected.LastExportAtUtc));
        }

        return rows
            .OrderByDescending(row => row.LastExportAtUtc)
            .ThenBy(row => row.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<ExportActivityRecentExportsDto> GetRecentExportsAsync(
        ExportActivityAnalyticsQuery query,
        IQueryable<ExportLog> filteredLogs,
        BusinessDemandLocationLookups locationLookups,
        IReadOnlyList<ApplicationUser> clientUsers,
        CancellationToken cancellationToken)
    {
        var totalCount = await filteredLogs.CountAsync(cancellationToken);
        var skip = (query.RecentExportsPage - 1) * query.RecentExportsPageSize;
        var rows = await filteredLogs
            .OrderByDescending(log => log.TimestampUtc)
            .ThenByDescending(log => log.Id)
            .Skip(skip)
            .Take(query.RecentExportsPageSize)
            .Select(log => new RecentExportLogRow(
                log.Id,
                log.TimestampUtc,
                log.UserId,
                log.UserEmail,
                log.Destination,
                log.BlockedReason != null
                    ? AnalyticsExportStatusLabels.Blocked
                    : log.WasTruncated
                        ? AnalyticsExportStatusLabels.Partial
                        : AnalyticsExportStatusLabels.Successful,
                log.RequestedRowsCount,
                log.ExportedRowsCount,
                log.BlockedReason,
                log.WasTruncated,
                log.ExportLimitRows,
                log.AnalyticsSnapshot == null ? null : log.AnalyticsSnapshot.FiltersSnapshotJson,
                log.AnalyticsSnapshot == null ? null : log.AnalyticsSnapshot.SortSnapshotJson,
                log.AnalyticsSnapshot == null ? null : log.AnalyticsSnapshot.SearchSnapshotJson))
            .ToListAsync(cancellationToken);

        var userProfilesById = clientUsers.ToDictionary(user => user.Id, StringComparer.Ordinal);

        var items = rows
            .Select(row =>
            {
                userProfilesById.TryGetValue(row.UserId, out var user);
                return new ExportActivityRecentExportDto(
                    Id: row.Id,
                    TimestampUtc: row.TimestampUtc,
                    UserId: row.UserId,
                    Email: string.IsNullOrWhiteSpace(row.UserEmail)
                        ? user?.Email ?? string.Empty
                        : row.UserEmail,
                    DisplayName: user == null
                        ? null
                        : user.EffectiveDisplayName,
                    Destination: row.Destination,
                    Status: row.Status,
                    RequestedRows: row.RequestedRows,
                    ExportedRows: row.ExportedRows,
                    BlockedReason: row.BlockedReason,
                    Reason: ExportActivityReasonFormatter.Format(
                        row.BlockedReason,
                        row.WasTruncated,
                        row.ExportLimitRows,
                        row.ExportedRows),
                    FiltersSummary: ExportActivitySnapshotSummaryFormatter.FormatFilters(
                        row.FiltersSnapshotJson,
                        row.SearchSnapshotJson,
                        locationLookups),
                    SortSummary: ExportActivitySnapshotSummaryFormatter.FormatSort(row.SortSnapshotJson));
            })
            .ToArray();

        return new ExportActivityRecentExportsDto(items, totalCount);
    }

    private async Task<IReadOnlyList<ApplicationUser>> GetClientUsersAsync(
        CancellationToken cancellationToken)
        => await (
            from user in _context.Users.AsNoTracking()
            join userRole in _context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.Name == AppRoles.Client
            orderby user.NormalizedEmail, user.Id
            select user)
            .ToListAsync(cancellationToken);

    private async Task<BusinessDemandLocationLookups> LoadLocationLookupsAsync(
        CancellationToken cancellationToken)
    {
        var locationNamesByKey = await _context.CanonicalLocations
            .AsNoTracking()
            .ToDictionaryAsync(
                location => location.Key,
                location => location.DisplayName,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        var groupLocationRows = await _context.LocationGroupItems
            .AsNoTracking()
            .Select(item => new { item.GroupKey, item.LocationKey })
            .ToListAsync(cancellationToken);
        var groupLocationKeys = groupLocationRows
            .GroupBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.LocationKey).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new BusinessDemandLocationLookups(locationNamesByKey, groupLocationKeys);
    }

    private static IQueryable<ExportLog> ApplyFilters(
        IQueryable<ExportLog> logs,
        ExportActivityAnalyticsQuery query)
    {
        var filtered = logs
            .Where(log => log.Role == AppRoles.Client)
            .Where(log => log.TimestampUtc >= query.FromUtc && log.TimestampUtc < query.ToUtc);

        if (!string.IsNullOrWhiteSpace(query.ClientId))
        {
            filtered = filtered.Where(log => log.UserId == query.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(query.Destination))
        {
            filtered = filtered.Where(log => log.Destination == query.Destination);
        }

        return ApplyStatusFilter(filtered, query.Status);
    }

    private static IQueryable<ExportedDomainAccess> ApplyAccessFilters(
        IQueryable<ExportedDomainAccess> accesses,
        ExportActivityAnalyticsQuery query)
    {
        var filtered = accesses
            .Where(access => access.ExportLog.Role == AppRoles.Client)
            .Where(access => access.ExportLog.TimestampUtc >= query.FromUtc &&
                             access.ExportLog.TimestampUtc < query.ToUtc);

        if (!string.IsNullOrWhiteSpace(query.ClientId))
        {
            filtered = filtered.Where(access => access.UserId == query.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(query.Destination))
        {
            filtered = filtered.Where(access => access.ExportLog.Destination == query.Destination);
        }

        return query.Status switch
        {
            AnalyticsStatusFilters.Successful => filtered
                .Where(access => access.ExportLog.BlockedReason == null && !access.ExportLog.WasTruncated),
            AnalyticsStatusFilters.Partial => filtered
                .Where(access => access.ExportLog.BlockedReason == null && access.ExportLog.WasTruncated),
            AnalyticsStatusFilters.Blocked => filtered
                .Where(access => access.ExportLog.BlockedReason != null),
            _ => filtered
        };
    }

    private static IQueryable<ExportLog> ApplyStatusFilter(
        IQueryable<ExportLog> logs,
        string? status)
        => status switch
        {
            AnalyticsStatusFilters.Successful => logs
                .Where(log => log.BlockedReason == null && !log.WasTruncated),
            AnalyticsStatusFilters.Partial => logs
                .Where(log => log.BlockedReason == null && log.WasTruncated),
            AnalyticsStatusFilters.Blocked => logs
                .Where(log => log.BlockedReason != null),
            _ => logs
        };

    private sealed record ExportActivityDailyLogCounts(
        DateTime Date,
        int SuccessfulExports,
        int PartialExports,
        int BlockedExports);

    private sealed record ExportActivityDailyDomainCounts(
        DateTime Date,
        int ExportedDomains);

    private sealed record ExportActivitySelectedClientSummary(
        string UserId,
        int SuccessfulExports,
        int PartialExports,
        int BlockedExports,
        long RequestedRows,
        long ExportedRows,
        DateTime? LastExportAtUtc);

    private sealed record RecentExportLogRow(
        Guid Id,
        DateTime TimestampUtc,
        string UserId,
        string UserEmail,
        string Destination,
        string Status,
        int RequestedRows,
        int ExportedRows,
        string? BlockedReason,
        bool WasTruncated,
        int? ExportLimitRows,
        string? FiltersSnapshotJson,
        string? SortSnapshotJson,
        string? SearchSnapshotJson);
}
