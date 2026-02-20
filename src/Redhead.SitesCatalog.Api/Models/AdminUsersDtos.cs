using System.ComponentModel.DataAnnotations;

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
    bool IsActive);

public record ResetPasswordResponse(string TemporaryPassword);
