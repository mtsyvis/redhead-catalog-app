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

public sealed class AdminUserListItemDto
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
}
