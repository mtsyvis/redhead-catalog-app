using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models;

public record CurrentUserProfileResponse(
    string Email,
    string Role,
    string DisplayName,
    bool MustCompleteProfile,
    GoogleDriveStatusResponse GoogleDrive,
    CurrentUserProfileLimitsResponse Limits);

public record CurrentUserProfileLimitsResponse(
    ExportLimitMode ExportLimitMode,
    int? ExportLimitRows,
    bool IsUnlimited,
    int? DailyUniqueExportedDomainsUsed = null,
    int? DailyUniqueExportedDomainsLimit = null,
    int? WeeklyUniqueExportedDomainsUsed = null,
    int? WeeklyUniqueExportedDomainsLimit = null,
    int? DailyExportOperationsUsed = null,
    int? DailyExportOperationsLimit = null,
    int? WeeklyExportOperationsUsed = null,
    int? WeeklyExportOperationsLimit = null);

public record UpdateCurrentUserProfileRequest(string? DisplayName);
