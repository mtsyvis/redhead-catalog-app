namespace Redhead.SitesCatalog.Domain.Enums;

/// <summary>
/// Determines how the export row limit is applied for a role or user override.
/// Stable numeric values must not change once persisted.
/// </summary>
public enum ExportLimitMode
{
    Disabled = 1,
    Limited = 2,
    Unlimited = 3
}
