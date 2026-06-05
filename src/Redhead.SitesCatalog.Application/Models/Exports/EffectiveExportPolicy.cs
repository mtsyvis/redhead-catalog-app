using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models.Exports;

public enum EffectivePolicySource
{
    SuperAdminFixed,
    UserOverride,
    Role
}

public record EffectiveExportPolicy(
    ExportLimitMode Mode,
    int? Rows,
    bool IsOverridden,
    EffectivePolicySource Source,
    int? DailyUniqueExportedDomainsLimit = null,
    int? WeeklyUniqueExportedDomainsLimit = null,
    int? DailyExportOperationsLimit = null,
    int? WeeklyExportOperationsLimit = null);
