using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Single source of truth for computing the effective export policy for a user.
/// Precedence: SuperAdmin fixed > user override > role policy.
/// </summary>
public static class EffectiveExportPolicyResolver
{
    public static EffectiveExportPolicy Resolve(
        string userRole,
        RoleSettings roleSettings,
        ApplicationUser? user = null)
    {
        if (string.Equals(userRole, AppRoles.SuperAdmin, StringComparison.Ordinal))
        {
            return new EffectiveExportPolicy(
                Mode: ExportLimitMode.Unlimited,
                Rows: null,
                IsOverridden: false,
                Source: EffectivePolicySource.SuperAdminFixed);
        }

        if (user?.ExportLimitOverrideMode is { } overrideMode)
        {
            var overrideRows = overrideMode == ExportLimitMode.Limited
                ? user.ExportLimitRowsOverride
                : null;

            return new EffectiveExportPolicy(
                Mode: overrideMode,
                Rows: overrideRows,
                IsOverridden: true,
                Source: EffectivePolicySource.UserOverride);
        }

        return new EffectiveExportPolicy(
            Mode: roleSettings.ExportLimitMode,
            Rows: roleSettings.ExportLimitRows,
            IsOverridden: false,
            Source: EffectivePolicySource.Role);
    }
}
