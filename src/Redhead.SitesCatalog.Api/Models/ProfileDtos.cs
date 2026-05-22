using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models;

public record CurrentUserProfileResponse(
    string Email,
    string Role,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool MustCompleteProfile,
    GoogleDriveStatusResponse GoogleDrive,
    CurrentUserProfileLimitsResponse Limits);

public record CurrentUserProfileLimitsResponse(
    ExportLimitMode ExportLimitMode,
    int? ExportLimitRows,
    bool IsUnlimited);

public record UpdateCurrentUserProfileRequest(
    string? FirstName,
    string? LastName);
