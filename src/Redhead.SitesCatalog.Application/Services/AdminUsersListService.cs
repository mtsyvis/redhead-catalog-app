using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class AdminUsersListService : IAdminUsersListService
{
    private readonly ApplicationDbContext _context;
    private readonly IGoogleDriveIntegrationService _googleDriveIntegrationService;
    private readonly IExportUsageLimitService _exportUsageLimitService;

    public AdminUsersListService(
        ApplicationDbContext context,
        IGoogleDriveIntegrationService googleDriveIntegrationService,
        IExportUsageLimitService exportUsageLimitService)
    {
        _context = context;
        _googleDriveIntegrationService = googleDriveIntegrationService;
        _exportUsageLimitService = exportUsageLimitService;
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
                .Select(user => ToListItem(user, BuildEffectivePolicy(user, roleSettingsMap)))
                .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<AdminUserDetailsDto?> GetUserDetailsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var user = await BuildUsersQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var roleSettingsMap = await _context.RoleSettings
            .ToDictionaryAsync(rs => rs.RoleName, cancellationToken);
        var effectivePolicy = BuildEffectivePolicy(user, roleSettingsMap);
        var listItem = ToListItem(user, effectivePolicy);
        var googleDrive = await _googleDriveIntegrationService.GetStatusAsync(id, cancellationToken);
        var clientExportUsage = string.Equals(listItem.Role, AppRoles.Client, StringComparison.Ordinal)
            ? await _exportUsageLimitService.GetUsageAsync(
                listItem.Id,
                listItem.Role,
                effectivePolicy,
                DateTime.UtcNow,
                cancellationToken)
            : null;

        return new AdminUserDetailsDto
        {
            Id = listItem.Id,
            Email = listItem.Email,
            FirstName = listItem.FirstName,
            LastName = listItem.LastName,
            SuperAdminNote = listItem.SuperAdminNote,
            DisplayName = listItem.DisplayName,
            MustCompleteProfile = listItem.MustCompleteProfile,
            MustChangePassword = user.MustChangePassword,
            Role = listItem.Role,
            IsActive = listItem.IsActive,
            ExportLimitOverrideMode = listItem.ExportLimitOverrideMode,
            ExportLimitRowsOverride = listItem.ExportLimitRowsOverride,
            DailyUniqueExportedDomainsLimitOverride = listItem.DailyUniqueExportedDomainsLimitOverride,
            WeeklyUniqueExportedDomainsLimitOverride = listItem.WeeklyUniqueExportedDomainsLimitOverride,
            DailyExportOperationsLimitOverride = listItem.DailyExportOperationsLimitOverride,
            WeeklyExportOperationsLimitOverride = listItem.WeeklyExportOperationsLimitOverride,
            EffectiveExportLimitMode = listItem.EffectiveExportLimitMode,
            EffectiveExportLimitRows = listItem.EffectiveExportLimitRows,
            EffectiveDailyUniqueExportedDomainsLimit = listItem.EffectiveDailyUniqueExportedDomainsLimit,
            EffectiveWeeklyUniqueExportedDomainsLimit = listItem.EffectiveWeeklyUniqueExportedDomainsLimit,
            EffectiveDailyExportOperationsLimit = listItem.EffectiveDailyExportOperationsLimit,
            EffectiveWeeklyExportOperationsLimit = listItem.EffectiveWeeklyExportOperationsLimit,
            IsExportLimitOverridden = listItem.IsExportLimitOverridden,
            IsExportLimitEditable = listItem.IsExportLimitEditable,
            GoogleDriveConnected = googleDrive.Connected,
            GoogleDrive = googleDrive,
            ClientExportUsage = clientExportUsage
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
                FirstName = user.FirstName,
                LastName = user.LastName,
                SuperAdminNote = user.SuperAdminNote,
                Role = role.Name ?? string.Empty,
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                ExportLimitOverrideMode = user.ExportLimitOverrideMode,
                ExportLimitRowsOverride = user.ExportLimitRowsOverride,
                DailyUniqueExportedDomainsLimitOverride = user.DailyUniqueExportedDomainsLimitOverride,
                WeeklyUniqueExportedDomainsLimitOverride = user.WeeklyUniqueExportedDomainsLimitOverride,
                DailyExportOperationsLimitOverride = user.DailyExportOperationsLimitOverride,
                WeeklyExportOperationsLimitOverride = user.WeeklyExportOperationsLimitOverride
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
        EffectiveExportPolicy effectivePolicy)
    {
        var role = user.Role;
        var isSuperAdmin = string.Equals(role, AppRoles.SuperAdmin, StringComparison.Ordinal);

        return new AdminUserListItemDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            SuperAdminNote = user.SuperAdminNote,
            DisplayName = UserProfileNameValidator.GetDisplayName(user.FirstName, user.LastName, user.Email),
            MustCompleteProfile = !UserProfileNameValidator.IsProfileComplete(user.FirstName, user.LastName),
            Role = role,
            IsActive = user.IsActive,
            ExportLimitOverrideMode = isSuperAdmin ? null : user.ExportLimitOverrideMode,
            ExportLimitRowsOverride = isSuperAdmin ? null : user.ExportLimitRowsOverride,
            DailyUniqueExportedDomainsLimitOverride = isSuperAdmin ? null : user.DailyUniqueExportedDomainsLimitOverride,
            WeeklyUniqueExportedDomainsLimitOverride = isSuperAdmin ? null : user.WeeklyUniqueExportedDomainsLimitOverride,
            DailyExportOperationsLimitOverride = isSuperAdmin ? null : user.DailyExportOperationsLimitOverride,
            WeeklyExportOperationsLimitOverride = isSuperAdmin ? null : user.WeeklyExportOperationsLimitOverride,
            EffectiveExportLimitMode = effectivePolicy.Mode,
            EffectiveExportLimitRows = effectivePolicy.Rows,
            EffectiveDailyUniqueExportedDomainsLimit = effectivePolicy.DailyUniqueExportedDomainsLimit,
            EffectiveWeeklyUniqueExportedDomainsLimit = effectivePolicy.WeeklyUniqueExportedDomainsLimit,
            EffectiveDailyExportOperationsLimit = effectivePolicy.DailyExportOperationsLimit,
            EffectiveWeeklyExportOperationsLimit = effectivePolicy.WeeklyExportOperationsLimit,
            IsExportLimitOverridden = effectivePolicy.IsOverridden,
            IsExportLimitEditable = !isSuperAdmin
        };
    }

    private static EffectiveExportPolicy BuildEffectivePolicy(
        UserListQueryItem user,
        IReadOnlyDictionary<string, RoleSettings> roleSettingsMap)
    {
        var roleSettings = roleSettingsMap.TryGetValue(user.Role, out var settings)
            ? settings
            : new RoleSettings { RoleName = user.Role, ExportLimitMode = ExportLimitMode.Disabled };

        return EffectiveExportPolicyResolver.Resolve(
            user.Role,
            roleSettings,
            ToPolicyUser(user));
    }

    private static ApplicationUser ToPolicyUser(UserListQueryItem user)
    {
        return new ApplicationUser
        {
            Id = user.Id,
            Email = user.Email,
            IsActive = user.IsActive,
            ExportLimitOverrideMode = user.ExportLimitOverrideMode,
            ExportLimitRowsOverride = user.ExportLimitRowsOverride,
            DailyUniqueExportedDomainsLimitOverride = user.DailyUniqueExportedDomainsLimitOverride,
            WeeklyUniqueExportedDomainsLimitOverride = user.WeeklyUniqueExportedDomainsLimitOverride,
            DailyExportOperationsLimitOverride = user.DailyExportOperationsLimitOverride,
            WeeklyExportOperationsLimitOverride = user.WeeklyExportOperationsLimitOverride
        };
    }

    private sealed class UserListQueryItem
    {
        public string Id { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string NormalizedEmail { get; init; } = string.Empty;
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? SuperAdminNote { get; init; }
        public string Role { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool MustChangePassword { get; init; }
        public ExportLimitMode? ExportLimitOverrideMode { get; init; }
        public int? ExportLimitRowsOverride { get; init; }
        public int? DailyUniqueExportedDomainsLimitOverride { get; init; }
        public int? WeeklyUniqueExportedDomainsLimitOverride { get; init; }
        public int? DailyExportOperationsLimitOverride { get; init; }
        public int? WeeklyExportOperationsLimitOverride { get; init; }
    }
}
