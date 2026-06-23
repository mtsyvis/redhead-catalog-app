using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models;

public static class AdminUsersListUserTypes
{
    public const string All = "all";
    public const string Internal = "internal";
    public const string Clients = "clients";
}

public sealed class AdminUsersListQuery
{
    public string UserType { get; set; } = AdminUsersListUserTypes.All;
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class AdminUsersListResult
{
    public IReadOnlyList<AdminUserListItemDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public class AdminUserListItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? SuperAdminNote { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool MustCompleteProfile { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string AccountStatus { get; init; } = string.Empty;
    public DateTime? InvitationExpiresAtUtc { get; init; }
    public ExportLimitMode? ExportLimitOverrideMode { get; init; }
    public int? ExportLimitRowsOverride { get; init; }
    public int? DailyUniqueExportedDomainsLimitOverride { get; init; }
    public int? WeeklyUniqueExportedDomainsLimitOverride { get; init; }
    public int? DailyExportOperationsLimitOverride { get; init; }
    public int? WeeklyExportOperationsLimitOverride { get; init; }
    public ExportLimitMode? EffectiveExportLimitMode { get; init; }
    public int? EffectiveExportLimitRows { get; init; }
    public int? EffectiveDailyUniqueExportedDomainsLimit { get; init; }
    public int? EffectiveWeeklyUniqueExportedDomainsLimit { get; init; }
    public int? EffectiveDailyExportOperationsLimit { get; init; }
    public int? EffectiveWeeklyExportOperationsLimit { get; init; }
    public bool IsExportLimitOverridden { get; init; }
    public bool IsExportLimitEditable { get; init; }
}

public sealed class AdminUserDetailsDto : AdminUserListItemDto
{
    public bool MustChangePassword { get; init; }
    public DateTime? ActivatedAtUtc { get; init; }
    public bool GoogleDriveConnected { get; init; }
    public GoogleDriveStatusResponse GoogleDrive { get; init; } = null!;
    public ExportUsageSummary? ClientExportUsage { get; init; }
}
