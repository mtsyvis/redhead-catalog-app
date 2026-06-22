namespace Redhead.SitesCatalog.Domain.Enums;

/// <summary>
/// Stable numeric values must not change once persisted.
/// </summary>
public enum AhrefsSyncRunKind
{
    Scheduled = 1,
    ManualFull = 2,
    ManualLimited = 3,
    DryRun = 4
}
