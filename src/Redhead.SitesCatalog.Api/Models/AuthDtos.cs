using System.ComponentModel.DataAnnotations;

namespace Redhead.SitesCatalog.Api.Models;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

public record LoginResponse(
    string Email,
    bool MustChangePassword,
    bool MustCompleteProfile,
    string? FirstName,
    string? LastName,
    string DisplayName,
    IList<string> Roles);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record CompleteAccountSetupRequest(
    string? CurrentPassword,
    string? NewPassword,
    string? FirstName,
    string? LastName);

public record CompleteAccountSetupResponse(
    string Email,
    bool MustChangePassword,
    bool MustCompleteProfile,
    string? FirstName,
    string? LastName,
    string DisplayName,
    IList<string> Roles);

public record MessageResponse(string Message);

public record UserInfoResponse(
    string Id,
    string Email,
    bool MustChangePassword,
    bool MustCompleteProfile,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool IsActive,
    IList<string> Roles,
    bool IsExportDisabled);
