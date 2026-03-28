using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services;

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
    EffectivePolicySource Source);
