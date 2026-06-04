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

public record UserListItem(
    string Id,
    string Email,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool MustCompleteProfile,
    string Role,
    bool IsActive,
    ExportLimitMode? ExportLimitOverrideMode,
    int? ExportLimitRowsOverride,
    ExportLimitMode? EffectiveExportLimitMode,
    int? EffectiveExportLimitRows,
    bool IsExportLimitOverridden,
    bool IsExportLimitEditable);

public record SuperAdminUserListItem(
    string Id,
    string Email,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool MustCompleteProfile,
    string Role,
    bool IsActive,
    ExportLimitMode? ExportLimitOverrideMode,
    int? ExportLimitRowsOverride,
    ExportLimitMode? EffectiveExportLimitMode,
    int? EffectiveExportLimitRows,
    bool IsExportLimitOverridden,
    bool IsExportLimitEditable,
    string? SuperAdminNote)
    : UserListItem(
        Id,
        Email,
        FirstName,
        LastName,
        DisplayName,
        MustCompleteProfile,
        Role,
        IsActive,
        ExportLimitOverrideMode,
        ExportLimitRowsOverride,
        EffectiveExportLimitMode,
        EffectiveExportLimitRows,
        IsExportLimitOverridden,
        IsExportLimitEditable);

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

public record AdminUserDetailsResponse(
    string Id,
    string Email,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool MustCompleteProfile,
    bool MustChangePassword,
    string Role,
    bool IsActive,
    ExportLimitMode? ExportLimitOverrideMode,
    int? ExportLimitRowsOverride,
    ExportLimitMode? EffectiveExportLimitMode,
    int? EffectiveExportLimitRows,
    bool IsExportLimitOverridden,
    bool IsExportLimitEditable,
    bool GoogleDriveConnected,
    GoogleDriveStatusResponse GoogleDrive);

public record SuperAdminUserDetailsResponse(
    string Id,
    string Email,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool MustCompleteProfile,
    bool MustChangePassword,
    string Role,
    bool IsActive,
    ExportLimitMode? ExportLimitOverrideMode,
    int? ExportLimitRowsOverride,
    ExportLimitMode? EffectiveExportLimitMode,
    int? EffectiveExportLimitRows,
    bool IsExportLimitOverridden,
    bool IsExportLimitEditable,
    bool GoogleDriveConnected,
    GoogleDriveStatusResponse GoogleDrive,
    string? SuperAdminNote)
    : AdminUserDetailsResponse(
        Id,
        Email,
        FirstName,
        LastName,
        DisplayName,
        MustCompleteProfile,
        MustChangePassword,
        Role,
        IsActive,
        ExportLimitOverrideMode,
        ExportLimitRowsOverride,
        EffectiveExportLimitMode,
        EffectiveExportLimitRows,
        IsExportLimitOverridden,
        IsExportLimitEditable,
        GoogleDriveConnected,
        GoogleDrive);

public record ResetPasswordResponse(string TemporaryPassword);

public record UpdateUserRoleRequest(
    [Required, MinLength(1)] string Role);

public record ReactivateUserRequest(
    [Required, MinLength(1)] string Role);

public record ReactivateUserResponse(string TemporaryPassword);

public record UpdateUserExportLimitRequest(
    ExportLimitMode? OverrideMode,
    int? OverrideRows);

public record UpdateUserSuperAdminNoteRequest(string? SuperAdminNote);
