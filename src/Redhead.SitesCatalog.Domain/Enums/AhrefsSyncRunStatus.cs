namespace Redhead.SitesCatalog.Domain.Enums;

/// <summary>
/// Stable numeric values must not change once persisted.
/// </summary>
public enum AhrefsSyncRunStatus
{
    Running = 1,
    Succeeded = 2,
    SucceededPartial = 3,
    Failed = 4,
    SkippedAlreadyCompleted = 5,
    SkippedInsufficientUnits = 6,
    StoppedInsufficientUnits = 7,
    Cancelled = 8
}
