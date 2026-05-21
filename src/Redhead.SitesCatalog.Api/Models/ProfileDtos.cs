using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;

namespace Redhead.SitesCatalog.Api.Models;

public record CurrentUserProfileResponse(
    string Email,
    string Role,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool MustCompleteProfile,
    GoogleDriveStatusResponse GoogleDrive);

public record UpdateCurrentUserProfileRequest(
    string? FirstName,
    string? LastName);
