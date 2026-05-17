using System.ComponentModel.DataAnnotations;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models;

public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(1)] string Role);

public record CreateUserResponse(
    string Id,
    string Email,
    string Role,
    string TemporaryPassword);

public record UserListItem(
    string Id,
    string Email,
    string Role,
    bool IsActive,
    ExportLimitMode? ExportLimitOverrideMode,
    int? ExportLimitRowsOverride,
    ExportLimitMode? EffectiveExportLimitMode,
    int? EffectiveExportLimitRows,
    bool IsExportLimitOverridden,
    bool IsExportLimitEditable);

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

public record ResetPasswordResponse(string TemporaryPassword);

public record UpdateUserExportLimitRequest(
    ExportLimitMode? OverrideMode,
    int? OverrideRows);
