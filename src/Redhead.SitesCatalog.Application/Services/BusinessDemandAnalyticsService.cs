using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services.Analytics;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class BusinessDemandAnalyticsService : IBusinessDemandAnalyticsService
{
    private readonly ApplicationDbContext _context;

    public BusinessDemandAnalyticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BusinessDemandAnalyticsDto> GetBusinessDemandAsync(
        BusinessDemandAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = await ApplyFilters(_context.ExportLogs.AsNoTracking(), query)
            .Select(log => new ExportAnalyticsLogRow(
                log.UserId,
                log.RequestedRowsCount,
                log.ExportedRowsCount,
                log.AnalyticsSnapshot == null ? null : log.AnalyticsSnapshot.FiltersSnapshotJson))
            .ToListAsync(cancellationToken);

        var accumulator = new BusinessDemandAccumulator(
            await LoadLocationLookupsAsync(cancellationToken));
        foreach (var row in rows)
        {
            if (ExportFiltersSnapshotParser.TryParse(row.FiltersSnapshotJson, out var snapshot))
            {
                accumulator.Add(row, snapshot);
            }
        }

        return accumulator.ToDto(rows);
    }

    public async Task<IReadOnlyList<AnalyticsClientOptionDto>> ListClientOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await (
            from user in _context.Users.AsNoTracking()
            join userRole in _context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.Name == AppRoles.Client
            orderby user.NormalizedEmail, user.Id
            select new
            {
                user.Id,
                Email = user.Email ?? string.Empty,
                user.DisplayName
            })
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new AnalyticsClientOptionDto(
                user.Id,
                user.Email,
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName.Trim()))
            .ToArray();
    }

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
        BusinessDemandAnalyticsQuery query)
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

        filtered = query.Status switch
        {
            AnalyticsStatusFilters.Successful => filtered
                .Where(log => log.BlockedReason == null && !log.WasTruncated),
            AnalyticsStatusFilters.Partial => filtered
                .Where(log => log.BlockedReason == null && log.WasTruncated),
            AnalyticsStatusFilters.Blocked => filtered
                .Where(log => log.BlockedReason != null),
            _ => filtered
        };

        return filtered;
    }
}
