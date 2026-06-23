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
    string DisplayName,
    IList<string> Roles);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record CompleteAccountSetupRequest(
    string? CurrentPassword,
    string? NewPassword,
    string? DisplayName);

public record CompleteAccountSetupResponse(
    string Email,
    bool MustChangePassword,
    bool MustCompleteProfile,
    string DisplayName,
    IList<string> Roles);

public record InvitationStatusResponse(
    string Email,
    DateTime ExpiresAtUtc);

public record ActivateAccountRequest(
    [Required] string Token,
    [Required] string DisplayName,
    [Required, MinLength(8)] string Password);

public record ActivateAccountResponse(
    string Email,
    string DisplayName,
    IList<string> Roles);

public record MessageResponse(string Message);

public record UserInfoResponse(
    string Id,
    string Email,
    bool MustChangePassword,
    bool MustCompleteProfile,
    string DisplayName,
    bool IsActive,
    IList<string> Roles,
    bool IsExportDisabled);
