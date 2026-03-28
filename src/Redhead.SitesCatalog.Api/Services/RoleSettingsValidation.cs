using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Services;

/// <summary>
/// Validation rules for role settings update requests.
/// </summary>
public static class RoleSettingsValidation
{
    private static readonly HashSet<ExportLimitMode> ValidModes =
        [ExportLimitMode.Disabled, ExportLimitMode.Limited, ExportLimitMode.Unlimited];

    /// <summary>
    /// Returns an error message if the item is invalid, or null if valid.
    /// </summary>
    public static string? ValidateUpdateItem(RoleSettingUpdateItemDto item)
    {
        if (string.Equals(item.Role, AppRoles.SuperAdmin, StringComparison.Ordinal))
        {
            return "SuperAdmin role settings cannot be changed.";
        }

        if (!AppRoles.All.Contains(item.Role))
        {
            return $"Invalid role: {item.Role}.";
        }

        if (item.ExportLimitMode is null || !ValidModes.Contains(item.ExportLimitMode.Value))
        {
            return "ExportLimitMode must be Disabled, Limited, or Unlimited.";
        }

        return item.ExportLimitMode.Value switch
        {
            ExportLimitMode.Limited when item.ExportLimitRows is null or <= 0
                => "ExportLimitRows must be greater than 0 when mode is Limited.",
            ExportLimitMode.Disabled or ExportLimitMode.Unlimited when item.ExportLimitRows is not null
                => "ExportLimitRows must be null when mode is Disabled or Unlimited.",
            _ => null
        };
    }
}
