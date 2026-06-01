namespace Redhead.SitesCatalog.Domain.Enums;

/// <summary>
/// Stable numeric values must not change once persisted.
/// </summary>
public enum SystemJobRunStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3
}
