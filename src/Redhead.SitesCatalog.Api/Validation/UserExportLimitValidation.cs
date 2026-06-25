using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Validation;

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

        if (string.Equals(targetRole, AppRoles.Lite, StringComparison.Ordinal))
        {
            return "Lite export limit cannot be changed.";
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
            var rowError = request.OverrideRows is not null
                ? "OverrideRows must be null when OverrideMode is null."
                : null;

            return rowError ?? ValidateUsageLimitOverrides(request);
        }

        var modeError = request.OverrideMode.Value switch
        {
            ExportLimitMode.Limited when request.OverrideRows is null or <= 0
                => "OverrideRows must be greater than 0 when mode is Limited.",
            ExportLimitMode.Disabled or ExportLimitMode.Unlimited when request.OverrideRows is not null
                => "OverrideRows must be null when mode is Disabled or Unlimited.",
            _ => null
        };

        return modeError ?? ValidateUsageLimitOverrides(request);
    }

    public static string? ValidateUsageLimitTargetRole(
        string targetRole,
        UpdateUserExportLimitRequest request)
    {
        if (request.ClientUsageLimitOverrides == null)
        {
            return null;
        }

        return string.Equals(targetRole, AppRoles.Client, StringComparison.Ordinal)
            ? null
            : "Client export usage limits can only be changed for Client users.";
    }

    private static string? ValidateUsageLimitOverrides(UpdateUserExportLimitRequest request)
    {
        var overrides = request.ClientUsageLimitOverrides;
        if (overrides == null)
        {
            return null;
        }

        return ValidatePositiveIfProvided(
            overrides.DailyUniqueExportedDomainsLimit,
            nameof(overrides.DailyUniqueExportedDomainsLimit))
            ?? ValidatePositiveIfProvided(
                overrides.WeeklyUniqueExportedDomainsLimit,
                nameof(overrides.WeeklyUniqueExportedDomainsLimit))
            ?? ValidatePositiveIfProvided(
                overrides.DailyExportOperationsLimit,
                nameof(overrides.DailyExportOperationsLimit))
            ?? ValidatePositiveIfProvided(
                overrides.WeeklyExportOperationsLimit,
                nameof(overrides.WeeklyExportOperationsLimit));
    }

    private static string? ValidatePositiveIfProvided(int? value, string fieldName)
        => value is <= 0
            ? $"{fieldName} must be greater than 0 when provided."
            : null;
}
