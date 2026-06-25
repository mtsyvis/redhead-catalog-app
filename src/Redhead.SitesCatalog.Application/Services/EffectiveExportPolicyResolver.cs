using Redhead.SitesCatalog.Application.Models.Exports;
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
                Source: EffectivePolicySource.SuperAdminFixed,
                DailyUniqueExportedDomainsLimit: null,
                WeeklyUniqueExportedDomainsLimit: null,
                DailyExportOperationsLimit: null,
                WeeklyExportOperationsLimit: null);
        }

        if (string.Equals(userRole, AppRoles.Lite, StringComparison.Ordinal))
        {
            return new EffectiveExportPolicy(
                Mode: ExportLimitMode.Disabled,
                Rows: null,
                IsOverridden: false,
                Source: EffectivePolicySource.Role,
                DailyUniqueExportedDomainsLimit: null,
                WeeklyUniqueExportedDomainsLimit: null,
                DailyExportOperationsLimit: null,
                WeeklyExportOperationsLimit: null);
        }

        var isClient = string.Equals(userRole, AppRoles.Client, StringComparison.Ordinal);
        var hasUsageOverride = isClient && HasUsageLimitOverride(user);

        if (user?.ExportLimitOverrideMode is { } overrideMode)
        {
            var overrideRows = overrideMode == ExportLimitMode.Limited
                ? user.ExportLimitRowsOverride
                : null;

            return new EffectiveExportPolicy(
                Mode: overrideMode,
                Rows: overrideRows,
                IsOverridden: true,
                Source: EffectivePolicySource.UserOverride,
                DailyUniqueExportedDomainsLimit: ResolveClientUsageLimit(
                    isClient,
                    user.DailyUniqueExportedDomainsLimitOverride,
                    roleSettings.DailyUniqueExportedDomainsLimit),
                WeeklyUniqueExportedDomainsLimit: ResolveClientUsageLimit(
                    isClient,
                    user.WeeklyUniqueExportedDomainsLimitOverride,
                    roleSettings.WeeklyUniqueExportedDomainsLimit),
                DailyExportOperationsLimit: ResolveClientUsageLimit(
                    isClient,
                    user.DailyExportOperationsLimitOverride,
                    roleSettings.DailyExportOperationsLimit),
                WeeklyExportOperationsLimit: ResolveClientUsageLimit(
                    isClient,
                    user.WeeklyExportOperationsLimitOverride,
                    roleSettings.WeeklyExportOperationsLimit));
        }

        return new EffectiveExportPolicy(
            Mode: roleSettings.ExportLimitMode,
            Rows: roleSettings.ExportLimitRows,
            IsOverridden: hasUsageOverride,
            Source: hasUsageOverride ? EffectivePolicySource.UserOverride : EffectivePolicySource.Role,
            DailyUniqueExportedDomainsLimit: ResolveClientUsageLimit(
                isClient,
                user?.DailyUniqueExportedDomainsLimitOverride,
                roleSettings.DailyUniqueExportedDomainsLimit),
            WeeklyUniqueExportedDomainsLimit: ResolveClientUsageLimit(
                isClient,
                user?.WeeklyUniqueExportedDomainsLimitOverride,
                roleSettings.WeeklyUniqueExportedDomainsLimit),
            DailyExportOperationsLimit: ResolveClientUsageLimit(
                isClient,
                user?.DailyExportOperationsLimitOverride,
                roleSettings.DailyExportOperationsLimit),
            WeeklyExportOperationsLimit: ResolveClientUsageLimit(
                isClient,
                user?.WeeklyExportOperationsLimitOverride,
                roleSettings.WeeklyExportOperationsLimit));
    }

    private static int? ResolveClientUsageLimit(bool isClient, int? userOverride, int? roleDefault)
        => isClient ? userOverride ?? roleDefault : null;

    private static bool HasUsageLimitOverride(ApplicationUser? user)
        => user?.DailyUniqueExportedDomainsLimitOverride.HasValue == true ||
           user?.WeeklyUniqueExportedDomainsLimitOverride.HasValue == true ||
           user?.DailyExportOperationsLimitOverride.HasValue == true ||
           user?.WeeklyExportOperationsLimitOverride.HasValue == true;
}
