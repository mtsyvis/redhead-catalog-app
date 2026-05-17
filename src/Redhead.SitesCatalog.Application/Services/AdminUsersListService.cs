using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class AdminUsersListService : IAdminUsersListService
{
    private readonly ApplicationDbContext _context;

    public AdminUsersListService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminUsersListResult> ListUsersAsync(
        AdminUsersListQuery query,
        CancellationToken cancellationToken = default)
    {
        var usersQuery = ApplyUserTypeFilter(BuildUsersQuery(), query.UserType);

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var totalPages = CalculateTotalPages(totalCount, query.PageSize);
        var skip = (query.Page - 1) * query.PageSize;

        var pagedUsers = await ApplyOrdering(usersQuery)
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var roleSettingsMap = await _context.RoleSettings
            .ToDictionaryAsync(rs => rs.RoleName, cancellationToken);

        return new AdminUsersListResult
        {
            Items = pagedUsers
                .Select(user => ToListItem(user, roleSettingsMap))
                .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    private IQueryable<UserListQueryItem> BuildUsersQuery()
    {
        return
            from user in _context.Users
            join userRole in _context.UserRoles on user.Id equals userRole.UserId into userRoles
            from userRole in userRoles.DefaultIfEmpty()
            join role in _context.Roles on userRole.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new UserListQueryItem
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                NormalizedEmail = user.NormalizedEmail ?? user.Email ?? string.Empty,
                Role = role.Name ?? string.Empty,
                IsActive = user.IsActive,
                ExportLimitOverrideMode = user.ExportLimitOverrideMode,
                ExportLimitRowsOverride = user.ExportLimitRowsOverride
            };
    }

    private static IQueryable<UserListQueryItem> ApplyUserTypeFilter(
        IQueryable<UserListQueryItem> query,
        string userType)
    {
        return userType switch
        {
            AdminUsersListUserTypes.Clients => query.Where(user => user.Role == AppRoles.Client),
            AdminUsersListUserTypes.Internal => query.Where(user => user.Role != AppRoles.Client),
            _ => query
        };
    }

    private static IOrderedQueryable<UserListQueryItem> ApplyOrdering(IQueryable<UserListQueryItem> query)
    {
        return query
            .OrderBy(user => user.IsActive ? 0 : 1)
            .ThenBy(user =>
                user.Role == AppRoles.SuperAdmin ? 0 :
                user.Role == AppRoles.Admin ? 1 :
                user.Role == AppRoles.Internal ? 2 :
                user.Role == AppRoles.Client ? 3 :
                int.MaxValue)
            .ThenBy(user => user.NormalizedEmail)
            .ThenBy(user => user.Id);
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static AdminUserListItemDto ToListItem(
        UserListQueryItem user,
        IReadOnlyDictionary<string, RoleSettings> roleSettingsMap)
    {
        var role = user.Role;
        var isSuperAdmin = string.Equals(role, AppRoles.SuperAdmin, StringComparison.Ordinal);
        var roleSettings = roleSettingsMap.TryGetValue(role, out var settings)
            ? settings
            : new RoleSettings { RoleName = role, ExportLimitMode = ExportLimitMode.Disabled };
        var effectivePolicy = EffectiveExportPolicyResolver.Resolve(
            role,
            roleSettings,
            ToPolicyUser(user));

        return new AdminUserListItemDto
        {
            Id = user.Id,
            Email = user.Email,
            Role = role,
            IsActive = user.IsActive,
            ExportLimitOverrideMode = isSuperAdmin ? null : user.ExportLimitOverrideMode,
            ExportLimitRowsOverride = isSuperAdmin ? null : user.ExportLimitRowsOverride,
            EffectiveExportLimitMode = effectivePolicy.Mode,
            EffectiveExportLimitRows = effectivePolicy.Rows,
            IsExportLimitOverridden = effectivePolicy.IsOverridden,
            IsExportLimitEditable = !isSuperAdmin
        };
    }

    private static ApplicationUser ToPolicyUser(UserListQueryItem user)
    {
        return new ApplicationUser
        {
            Id = user.Id,
            Email = user.Email,
            IsActive = user.IsActive,
            ExportLimitOverrideMode = user.ExportLimitOverrideMode,
            ExportLimitRowsOverride = user.ExportLimitRowsOverride
        };
    }

    private sealed class UserListQueryItem
    {
        public string Id { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string NormalizedEmail { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public ExportLimitMode? ExportLimitOverrideMode { get; init; }
        public int? ExportLimitRowsOverride { get; init; }
    }
}
