using System.ComponentModel.DataAnnotations;

namespace Redhead.SitesCatalog.Api.Models;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

public record LoginResponse(
    string Email,
    bool MustChangePassword,
    IList<string> Roles);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record MessageResponse(string Message);

public record UserInfoResponse(
    string Id,
    string Email,
    bool MustChangePassword,
    bool IsActive,
    IList<string> Roles);
