using System.ComponentModel.DataAnnotations;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models;

public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(1)] string Role,
    string? SuperAdminNote = null);

public record CreateUserResponse(
    string Id,
    string Email,
    string Role,
    string TemporaryPassword);

public record UserListItem
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool MustCompleteProfile { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public ExportLimitMode? ExportLimitOverrideMode { get; init; }
    public int? ExportLimitRowsOverride { get; init; }
    public ExportLimitMode? EffectiveExportLimitMode { get; init; }
    public int? EffectiveExportLimitRows { get; init; }
    public bool IsExportLimitOverridden { get; init; }
    public bool IsExportLimitEditable { get; init; }
    public int? DailyUniqueExportedDomainsLimitOverride { get; init; }
    public int? WeeklyUniqueExportedDomainsLimitOverride { get; init; }
    public int? DailyExportOperationsLimitOverride { get; init; }
    public int? WeeklyExportOperationsLimitOverride { get; init; }
    public int? EffectiveDailyUniqueExportedDomainsLimit { get; init; }
    public int? EffectiveWeeklyUniqueExportedDomainsLimit { get; init; }
    public int? EffectiveDailyExportOperationsLimit { get; init; }
    public int? EffectiveWeeklyExportOperationsLimit { get; init; }
}

public sealed record SuperAdminUserListItem : UserListItem
{
    public string? SuperAdminNote { get; init; }
}

public class UserListRequest
{
    public string UserType { get; set; } = AdminUsersListUserTypes.All;
    public int Page { get; set; } = PaginationDefaults.DefaultPage;
    public int PageSize { get; set; } = PaginationDefaults.DefaultPageSize;
}

public record UserListResponse(
    IReadOnlyList<UserListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record SuperAdminUserListResponse(
    IReadOnlyList<SuperAdminUserListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record AdminUserDetailsResponse
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool MustCompleteProfile { get; init; }
    public bool MustChangePassword { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public ExportLimitMode? ExportLimitOverrideMode { get; init; }
    public int? ExportLimitRowsOverride { get; init; }
    public ExportLimitMode? EffectiveExportLimitMode { get; init; }
    public int? EffectiveExportLimitRows { get; init; }
    public bool IsExportLimitOverridden { get; init; }
    public bool IsExportLimitEditable { get; init; }
    public bool GoogleDriveConnected { get; init; }
    public GoogleDriveStatusResponse GoogleDrive { get; init; } = new(false, null, null, null, false, false, false);
    public AdminUserClientExportUsageResponse? ClientExportUsage { get; init; }
    public int? DailyUniqueExportedDomainsLimitOverride { get; init; }
    public int? WeeklyUniqueExportedDomainsLimitOverride { get; init; }
    public int? DailyExportOperationsLimitOverride { get; init; }
    public int? WeeklyExportOperationsLimitOverride { get; init; }
    public int? EffectiveDailyUniqueExportedDomainsLimit { get; init; }
    public int? EffectiveWeeklyUniqueExportedDomainsLimit { get; init; }
    public int? EffectiveDailyExportOperationsLimit { get; init; }
    public int? EffectiveWeeklyExportOperationsLimit { get; init; }
}

public record AdminUserClientExportUsageResponse(
    int? DailyUniqueExportedDomainsUsed,
    int? DailyUniqueExportedDomainsLimit,
    int? WeeklyUniqueExportedDomainsUsed,
    int? WeeklyUniqueExportedDomainsLimit,
    int? DailyExportOperationsUsed,
    int? DailyExportOperationsLimit,
    int? WeeklyExportOperationsUsed,
    int? WeeklyExportOperationsLimit);

public sealed record SuperAdminUserDetailsResponse : AdminUserDetailsResponse
{
    public string? SuperAdminNote { get; init; }
}

public record ResetPasswordResponse(string TemporaryPassword);

public record UpdateUserRoleRequest(
    [Required, MinLength(1)] string Role);

public record ReactivateUserRequest(
    [Required, MinLength(1)] string Role);

public record ReactivateUserResponse(string TemporaryPassword);

public record UpdateUserExportLimitRequest(
    ExportLimitMode? OverrideMode,
    int? OverrideRows,
    ClientExportUsageLimitOverridesRequest? ClientUsageLimitOverrides = null);

public record ClientExportUsageLimitOverridesRequest(
    int? DailyUniqueExportedDomainsLimit,
    int? WeeklyUniqueExportedDomainsLimit,
    int? DailyExportOperationsLimit,
    int? WeeklyExportOperationsLimit);

public record UpdateUserSuperAdminNoteRequest(string? SuperAdminNote);
