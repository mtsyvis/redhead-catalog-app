namespace Redhead.SitesCatalog.Domain.Enums;

/// <summary>
/// Stable numeric values must not change once persisted.
/// </summary>
public enum AhrefsSyncRunItemStatus
{
    Succeeded = 1,
    Failed = 2,
    NotReturnedByAhrefs = 3,
    Skipped = 4
}
