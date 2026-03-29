using System.ComponentModel.DataAnnotations;
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

public record ResetPasswordResponse(string TemporaryPassword);

public record UpdateUserExportLimitRequest(
    ExportLimitMode? OverrideMode,
    int? OverrideRows);
