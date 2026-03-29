using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Services;

/// <summary>
/// Validation rules for per-user export limit override requests.
/// </summary>
public static class UserExportLimitValidation
{
    /// <summary>
    /// Returns an error message if the target user's role cannot have its export limit changed, or null if allowed.
    /// </summary>
    public static string? ValidateTargetRole(string targetRole)
    {
        if (string.Equals(targetRole, AppRoles.SuperAdmin, StringComparison.Ordinal))
        {
            return "SuperAdmin export limit cannot be changed.";
        }

        return null;
    }

    /// <summary>
    /// Returns an error message if the override request payload is invalid, or null if valid.
    /// </summary>
    public static string? ValidateOverride(UpdateUserExportLimitRequest request)
    {
        if (request.OverrideMode is null)
        {
            return request.OverrideRows is not null
                ? "OverrideRows must be null when OverrideMode is null."
                : null;
        }

        return request.OverrideMode.Value switch
        {
            ExportLimitMode.Limited when request.OverrideRows is null or <= 0
                => "OverrideRows must be greater than 0 when mode is Limited.",
            ExportLimitMode.Disabled or ExportLimitMode.Unlimited when request.OverrideRows is not null
                => "OverrideRows must be null when mode is Disabled or Unlimited.",
            _ => null
        };
    }
}
